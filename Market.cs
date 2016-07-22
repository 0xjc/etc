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
		private StreamWriter bookDumpFile;
		private StreamWriter outputDumpFile;

		public const int INVALID_ID = -1;
		public List<string> Symbols = new List<string> { "AMZN", "HD", "DIS", "PG", "KO", "PM", "NEE", "DUK", "SO", "XLY", "XLP", "XLU", "RSP" };

		private object thisLock = new object();
		private int currentID = 0;

		private ConcurrentQueue<string> sendQueue;
		private SemaphoreSlim sendAvailable;

		private ConcurrentQueue<string> outputQueue;
		private SemaphoreSlim outputAvailable;

		private int cash;
		private Dictionary<string, int> positions;

		private ConcurrentDictionary<string, Security> securities;

		private Dictionary<int, ConvertOrder> pendingConverts;

		public Market(NetworkStream stream_, string runID)
		{
			stream = stream_;
			reader = new StreamReader(stream, Encoding.ASCII);
			writer = new StreamWriter(stream, Encoding.ASCII);
			posDumpFile = File.CreateText(string.Format("pos-{0}.txt", runID));
			bookDumpFile = File.CreateText(string.Format("book-{0}.txt", runID));
			outputDumpFile = File.CreateText(string.Format("output-{0}.txt", runID));
			sendQueue = new ConcurrentQueue<string>();
			sendAvailable = new SemaphoreSlim(0);
			outputQueue = new ConcurrentQueue<string>();
			outputAvailable = new SemaphoreSlim(0);
			cash = 0;
			positions = new Dictionary<string, int>();
			securities = new ConcurrentDictionary<string, Security>();
			pendingConverts = new Dictionary<int, ConvertOrder>();
		}

		public void OutputLoop()
		{
			while (true)
			{
				string output;
				outputAvailable.Wait();
				if (outputQueue.TryDequeue(out output))
				{
					Console.WriteLine(output);
					outputDumpFile.WriteLine(output);
				}
			}
		}

		private void QueueOutput(string output)
		{
			outputQueue.Enqueue(output);
			outputAvailable.Release();
		}

		private void LogSend(string msg)
		{
			if (msg.StartsWith("CANCEL")) return;
			QueueOutput("\x1b[36mSEND: " + msg + "\x1b[0m");
		}

		private void LogReceive(string msg)
		{
			string colorCode;
			int spc = msg.IndexOf(' ');
			if (spc == -1) colorCode = "";
			else
			{
				string pre = msg.Substring(0, spc);
				int color;
				if (pre == "ACK") color = 32;
				else if (pre == "FILL") color = 33;
				else if (pre == "REJECT" || pre == "ERROR") color = 31;
				else color = 0;
				colorCode = "\x1b[" + color + "m";
			}
			QueueOutput(colorCode + "RECV: " + msg + (colorCode == "" ? "" : "\x1b[0m"));
		}

		private void LogError(string msg)
		{
			QueueOutput("\x1b[31mERROR: " + msg + "\x1b[0m");
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

		// Creates a snapshot of the current security (safe)
		public Security GetSecurity(string symbol)
		{
			Security sec = GetSecurityInternal(symbol);
			return sec.Clone();
		}

		private Security GetSecurityInternal(string symbol)
		{
			if (!securities.ContainsKey(symbol))
			{
				securities.TryAdd(symbol, new Security(symbol));
			}
			return securities[symbol];
		}

		public void DumpBooks()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("BOOK:");
			foreach (var sym in Symbols)
			{
				Security sec;
				if (securities.TryGetValue(sym, out sec))
				{
					lock (sec.GetLock())
					{
						sb.Append(string.Format(" {0}:{1}@{2}", sec.symbol, sec.bid, sec.ask));
					}
				}
			}
			string dumpText = sb.ToString();
			bookDumpFile.WriteLine(dumpText);
			bookDumpFile.Flush();
			//Console.WriteLine(dumpText);
		}

		public void DumpCashAndPositions()
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("POS:");
			double pnl = 0.0;
			lock (thisLock)
			{
				sb.Append(string.Format(" $={0}", cash));
				pnl += cash;
				foreach (var sym in Symbols)
				{
					int pos;
					Security sec;
					if (positions.TryGetValue(sym, out pos))
					{
						sb.Append(string.Format(" {0}={1}", sym, pos));
					}
					if (pnl != -1.0)
					{
						if (securities.TryGetValue(sym, out sec))
						{
							int mid = sec.mid;
							if (mid == -1)
								pnl = -1.0;
							else
								pnl += pos * mid;
						}
					}
				}
			}
			sb.AppendFormat(" PnL={0}", (pnl == -1.0) ? "???" : ((int)Math.Round(pnl)).ToString());
			string dumpText = sb.ToString();
			posDumpFile.WriteLine(dumpText);
			posDumpFile.Flush();
			//Console.WriteLine(dumpText);
		}

		public void DumpLoop()
		{
			while (true)
			{
				DumpBooks();
				DumpCashAndPositions();
				Task.Delay(1000).Wait();
			}
		}

		public void ProcessMessage(string msg)
		{
			string[] toks = msg.Split(' ');
			var tok0 = toks[0];

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
					if (toks[i] == "SELL") { break; }
					string[] priceAndSize = toks[i].Split(':');
					args.buys.Add(int.Parse(priceAndSize[0]), int.Parse(priceAndSize[1]));
				}
				++i;
				for (; i < toks.Length; ++i)
				{
					string[] priceAndSize = toks[i].Split(':');
					args.sells.Add(int.Parse(priceAndSize[0]), int.Parse(priceAndSize[1]));
				}
				var sec = GetSecurityInternal(args.symbol);
				sec.OnBook(args);
				var handler = Book;
				if (handler != null) handler(this, args);
			}
			else if (tok0 == "TRADE")
			{
				var args = new TradeEventArgs();
				args.symbol = toks[1];
				args.price = int.Parse(toks[2]);
				args.size = int.Parse(toks[3]);
				var sec = GetSecurityInternal(args.symbol);
				sec.OnTrade(args);
				var handler = Trade;
				if (handler != null) handler(this, args);
			}
			else if (tok0 == "ACK")
			{
				//LogReceive(msg);
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
				var handler = Fill;
				if (handler != null) handler(this, args);
			}
			else if (tok0 == "OUT")
			{
				//LogReceive(msg);
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
					LogError("EXN in Receive processing: " + ex.StackTrace);
				}
			}
		}

		public void SendLoop()
		{
			while (true)
			{
				string msg;
				sendAvailable.Wait();
				if (sendQueue.TryDequeue(out msg))
				{
					writer.WriteLine(msg);
					writer.Flush();
					LogSend(msg);
				}
			}
		}

		public void QueueSend(string msg)
		{
			sendQueue.Enqueue(msg);
			sendAvailable.Release();
		}

		public void Hello()
		{
			string msg = "HELLO AMPERE";
			QueueSend(msg);
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
			string msg = string.Format("ADD {0} {1} {2} {3} {4}", id, symbol.ToUpperInvariant(), dir, price, size);
			QueueSend(msg);
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
			string msg = string.Format("CONVERT {0} {1} {2} {3}", id, symbol.ToUpperInvariant(), dir, size);
			pendingConverts.Add(id, new ConvertOrder(symbol, dir, size));
			QueueSend(msg);
			return id;
		}

		public void Cancel(int id)
		{
			string msg = string.Format("CANCEL {0}", id);
			QueueSend(msg);
		}
	}
}
