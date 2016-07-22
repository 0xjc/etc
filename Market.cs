using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace etc
{
	public enum Direction
	{
		BUY,
		SELL
	};

	public class HelloEventArgs : EventArgs
	{
		public int cash;
		public Dictionary<string, int> positions; // key = symbol, value = position
	}

	public class OpenEventArgs : EventArgs
	{
		public List<string> symbols;
	}

	public class CloseEventArgs : EventArgs
	{
		public List<string> symbols;
	}

	public class ErrorEventArgs : EventArgs
	{
		public string message;
	}

	public class BookEventArgs : EventArgs
	{
		public string symbol;
		public SortedDictionary<int, int> buys; // key = price, value = quantity
		public SortedDictionary<int, int> sells;
	}

	public class TradeEventArgs : EventArgs
	{
		public string symbol;
		public int price;
		public int size;
	}

	public class AckEventArgs : EventArgs
	{
		public int id;
	}

	public class RejectEventArgs : EventArgs
	{
		public int id;
		public string message;
	}

	public class FillEventArgs : EventArgs
	{
		public int id;
		public string symbol;
		public Direction dir;
		public int price;
		public int size;
	}

	public class OutEventArgs : EventArgs
	{
		public int id;
	}

	class Market
	{
		public delegate void HelloEventHandler(object sender, HelloEventArgs e);
		public delegate void OpenEventHandler(object sender, OpenEventArgs e);
		public delegate void CloseEventHandler(object sender, CloseEventArgs e);
		public delegate void ErrorEventHandler(object sender, ErrorEventArgs e);
		public delegate void BookEventHandler(object sender, BookEventArgs e);
		public delegate void TradeEventHandler(object sender, TradeEventArgs e);
		public delegate void AckEventHandler(object sender, AckEventArgs e);
		public delegate void RejectEventHandler(object sender, RejectEventArgs e);
		public delegate void FillEventHandler(object sender, FillEventArgs e);
		public delegate void OutEventHandler(object sender, OutEventArgs e);

		public event HelloEventHandler GotHello;
		public event OpenEventHandler Open;
		public event CloseEventHandler Close;
		public event ErrorEventHandler Error;
		public event BookEventHandler Book;
		public event TradeEventHandler Trade;
		public event AckEventHandler Ack;
		public event RejectEventHandler Reject;
		public event FillEventHandler Fill;
		public event OutEventHandler Out;

		class ConvertOrder
		{
			public string symbol;
			public Direction dir;
			public int size;
			public ConvertOrder(string symbol_, Direction dir_, int size_) { symbol = symbol_; dir = dir_; size = size_; }
		}

		private NetworkStream stream;
		private StreamReader reader;
		private StreamWriter writer;
		private StreamWriter posDumpFile;

		public const int INVALID_ID = -1;
		private object thisLock = new object();
		private int currentID = 0;

		private int cash;
		private Dictionary<string, int> positions;
		private DateTime lastPositionsDump;

		private Dictionary<int, ConvertOrder> pendingConverts;

		public Market(NetworkStream stream_, string posDumpFilename)
		{
			stream = stream_;
			reader = new StreamReader(stream, Encoding.ASCII);
			writer = new StreamWriter(stream, Encoding.ASCII);
			cash = 0;
			positions = new Dictionary<string, int>();
			lastPositionsDump = DateTime.Now;
			pendingConverts = new Dictionary<int, ConvertOrder>();
			posDumpFile = File.CreateText(posDumpFilename);
		}

		private void LogSend(string msg)
		{
			Console.WriteLine("\x1b[36mSEND: " + msg + "\x1b[0m");
		}

		private void LogReceive(string msg)
		{
			Console.WriteLine("RECV: " + msg);
		}

		private void LogError(string msg)
		{
			Console.Error.WriteLine("ERROR: " + msg);
		}

		public Direction ParseDirection(string s)
		{
			return (Direction)Enum.Parse(typeof(Direction), s);
		}

		public int GetCash() { return cash; }

		public int GetPosition(string symbol)
		{
			if (positions.ContainsKey(symbol)) return positions[symbol];
			else return 0;
		}

		public void DumpCashAndPositions()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("POSITIONS: ");
			lock (thisLock)
			{
				sb.Append(string.Format("[Cash={0}]", cash));
				foreach (var kvp in positions)
				{
					sb.Append(string.Format(" [{0}={1}]", kvp.Key, kvp.Value));
				}
			}
			sb.AppendLine();

			string dumpText = sb.ToString();
			posDumpFile.Write(dumpText);
			posDumpFile.Flush();
			Console.Write(dumpText);

			lastPositionsDump = DateTime.Now;
		}

		public void ProcessMessage(string msg)
		{
			string[] toks = msg.Split(' ');
			var tok0 = toks[0].ToUpper();

			if (tok0 == "HELLO")
			{
				LogReceive(msg);
				var args = new HelloEventArgs();
				args.cash = int.Parse(toks[1]);
				args.positions = new Dictionary<string, int>();
				lock (thisLock)
				{
					cash = args.cash;
					for (int i = 2; i < toks.Length; ++i)
					{
						string[] symAndPosn = toks[i].Split(':');
						string sym = symAndPosn[0];
						int pos = int.Parse(symAndPosn[1]);
						args.positions.Add(sym, pos);
						positions[sym] = pos;
					}
				}
				var handler = GotHello;
				if (handler != null) handler(this, args);
				DumpCashAndPositions();
			}
			else if (tok0 == "OPEN")
			{
				LogReceive(msg);
				var args = new OpenEventArgs();
				args.symbols = new List<string>();
				for (int i = 1; i < toks.Length; ++i)
				{
					args.symbols.Add(toks[i]);
				}
				var handler = Open;
				if (handler != null) handler(this, args);
			}
			else if (tok0 == "CLOSE")
			{
				LogReceive(msg);
				var args = new CloseEventArgs();
				args.symbols = new List<string>();
				for (int i = 1; i < toks.Length; ++i)
				{
					args.symbols.Add(toks[i]);
				}
				var handler = Close;
				if (handler != null) handler(this, args);
			}
			else if (tok0 == "ERROR")
			{
				LogReceive(msg);
				var args = new ErrorEventArgs();
				args.message = msg.Substring(6);
				var handler = Error;
				if (handler != null) handler(this, args);
			}
			else if (tok0 == "BOOK")
			{
				var args = new BookEventArgs();
				args.symbol = toks[1];
				args.buys = new SortedDictionary<int, int>();
				args.sells = new SortedDictionary<int, int>();
				if (toks[2] != "BUY") throw new Exception("toks[2] is not BUY");
				int i;
				for (i = 3; i < toks.Length; ++i)
				{
					if (toks[i].ToUpper() == "SELL") { break; }
					string[] priceAndSize = toks[i].Split(':');
					args.buys.Add(int.Parse(priceAndSize[0]), int.Parse(priceAndSize[1]));
				}
				++i;
				for (; i < toks.Length; ++i)
				{
					string[] priceAndSize = toks[i].Split(':');
					args.sells.Add(int.Parse(priceAndSize[0]), int.Parse(priceAndSize[1]));
				}
				var handler = Book;
				if (handler != null) handler(this, args);
			}
			else if (tok0 == "TRADE")
			{
				var args = new TradeEventArgs();
				args.symbol = toks[1];
				args.price = int.Parse(toks[2]);
				args.size = int.Parse(toks[3]);
				var handler = Trade;
				if (handler != null) handler(this, args);
			}
			else if (tok0 == "ACK")
			{
				LogReceive(msg);
				var args = new AckEventArgs();
				args.id = int.Parse(toks[1]);
				ConvertOrder conv;
				lock (thisLock)
				{
					if (pendingConverts.TryGetValue(args.id, out conv))
					{
						pendingConverts.Remove(args.id);

						int sign = (conv.dir == Direction.BUY) ? 1 : -1;
						int num = sign * conv.size / 20;
						if (conv.symbol == "XLY")
						{
							positions["XLY"] += 20 * num;
							positions["AMZN"] -= 6 * num;
							positions["HD"] -= 6 * num;
							positions["DIS"] -= 8 * num;
							cash -= 200;
						}
						else if (conv.symbol == "XLP")
						{
							positions["XLP"] += 20 * num;
							positions["PG"] -= 12 * num;
							positions["KO"] -= 12 * num;
							positions["PM"] -= 6 * num;
							cash -= 200;
						}
						else if (conv.symbol == "XLU")
						{
							positions["XLU"] += 20 * num;
							positions["NEE"] -= 8 * num;
							positions["DUK"] -= 6 * num;
							positions["SO"] -= 8 * num;
							cash -= 200;
						}
						else if (conv.symbol == "RSP")
						{
							positions["RSP"] += 20 * num;
							positions["AMZN"] -= 3 * num;
							positions["HD"] -= 6 * num;
							positions["DIS"] -= 8 * num;
							positions["PG"] -= 6 * num;
							positions["KO"] -= 12 * num;
							positions["PM"] -= 6 * num;
							positions["NEE"] -= 4 * num;
							positions["DUK"] -= 6 * num;
							positions["SO"] -= 8 * num;
							cash -= 200;
						}
						else
						{
							LogError(string.Format("Convert on unknown symbol {0}", conv.symbol));
						}
					}
				}
				DumpCashAndPositions();
				var handler = Ack;
				if (handler != null) handler(this, args);
			}
			else if (tok0 == "REJECT")
			{
				LogReceive(msg);
				var args = new RejectEventArgs();
				args.id = int.Parse(toks[1]);
				string[] remainingToks = new string[toks.Length - 2];
				for (int i = 2; i < toks.Length; ++i) remainingToks[i - 2] = toks[i];
				args.message = string.Join(" ", remainingToks);
				var handler = Reject;
				if (handler != null) handler(this, args);
			}
			else if (tok0 == "FILL")
			{
				LogReceive(msg);
				var args = new FillEventArgs();
				args.id = int.Parse(toks[1]);
				args.symbol = toks[2];
				args.dir = ParseDirection(toks[3]);
				args.price = int.Parse(toks[4]);
				args.size = int.Parse(toks[5]);
				int sign = (args.dir == Direction.BUY) ? 1 : -1;
				lock (thisLock)
				{
					if (!positions.ContainsKey(args.symbol)) positions[args.symbol] = 0;
					positions[args.symbol] += sign * args.size;
					cash -= sign * (args.price * args.size);
				}
				DumpCashAndPositions();
				var handler = Fill;
				if (handler != null) handler(this, args);
			}
			else if (tok0 == "OUT")
			{
				LogReceive(msg);
				var args = new OutEventArgs();
				args.id = int.Parse(toks[1]);
				var handler = Out;
				if (handler != null) handler(this, args);
			}
			else
			{
				LogError(string.Format("Unknown message: {0}", msg));
			}
		}

		public void ReceiveLoop()
		{
			string msg;
			while ((msg = reader.ReadLine()) != null)
			{
				try
				{
					ProcessMessage(msg);
				}
				catch (Exception ex)
				{
					LogError("Exn in Receive processing: " + ex.Message);
				}
				if (DateTime.Now - lastPositionsDump > TimeSpan.FromSeconds(1.0))
				{
					DumpCashAndPositions();
				}
			}
		}

		public void Hello()
		{
			string msg = "HELLO AMPERE";

			writer.WriteLine(msg);
			writer.Flush();
			LogSend(msg);
		}

		public int Add(string symbol, Direction dir, int price, int size)
		{
			if (price <= 0)
			{
				//LogError("ADD with nonpositive price attempted");
				return INVALID_ID;
			}

			if (size == 0) return INVALID_ID;
			if (size < 0)
			{
				//LogError("ADD with negative size attempted");
				return INVALID_ID;
			}

			int id = Interlocked.Increment(ref currentID);
			string msg = string.Format("ADD {0} {1} {2} {3} {4}", id, symbol.ToUpper(), dir, price, size);

			writer.WriteLine(msg);
			writer.Flush();
			LogSend(msg);
			return id;
		}

		public int Convert(string symbol, Direction dir, int size)
		{
			if (size == 0) return INVALID_ID;
			if (size < 0)
			{
				//LogError("CONVERT with negative size attempted");
				return INVALID_ID;
			}

			int id = Interlocked.Increment(ref currentID);
			string msg = string.Format("CONVERT {0} {1} {2} {3}", id, symbol.ToUpper(), dir, size);

			pendingConverts.Add(id, new ConvertOrder(symbol, dir, size));
			writer.WriteLine(msg);
			writer.Flush();
			LogSend(msg);
			return id;
		}

		public void Cancel(int id)
		{
			string msg = string.Format("CANCEL {0}", id);

			writer.WriteLine(msg);
			writer.Flush();
			LogSend(msg);
		}
	}
}
