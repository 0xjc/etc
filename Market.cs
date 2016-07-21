using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
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

		private NetworkStream stream;
		private StreamReader reader;
		private StreamWriter writer;

		public Market(NetworkStream stream_)
		{
			stream = stream_;
			reader = new StreamReader(stream, Encoding.ASCII);
			writer = new StreamWriter(stream, Encoding.ASCII);
		}

		private void LogSend(string msg)
		{
			Console.WriteLine("SEND: " + msg);
		}

		private void LogReceive(string msg)
		{
			Console.WriteLine("RECV: " + msg);
		}

		private void LogError(string msg)
		{
			Console.Error.WriteLine("ERROR: " + msg);
		}

		public void ReceiveLoop()
		{
			string msg;
			while ((msg = reader.ReadLine()) != null)
			{
				LogReceive(msg);
				string[] toks = msg.Split(' ');
				try
				{
					switch (toks[0].ToUpper())
					{
						case "HELLO":
							{
								var args = new HelloEventArgs();
								args.cash = int.Parse(toks[1]);
								args.positions = new Dictionary<string, int>();
								for (int i = 2; i < toks.Length; ++i)
								{
									string[] symAndPosn = toks[i].Split(':');
									args.positions.Add(symAndPosn[0], int.Parse(symAndPosn[1]));
								}
								var handler = GotHello;
								if (handler != null) handler(this, args);
								break;
							}
						case "BOOK":
							{
								var args = new BookEventArgs();
								args.symbol = toks[1];
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
								break;
							}
					}
				}
				catch (Exception ex)
				{
					LogError("Exn in Receive processing: " + ex.Message);
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

		public void Add(int id, string symbol, Direction dir, int price, int size)
		{
			if (size < 0)
			{
				LogError("ADD with negative size attempted");
				return;
			}

			if (price < 0)
			{
				LogError("ADD with negative price attempted");
				return;
			}

			string msg = string.Format("ADD {0} {1} {2} {3} {4}", id, symbol.ToUpper(), dir, price, size);

			writer.WriteLine(msg);
			writer.Flush();
			LogSend(msg);
		}

		public void Convert(int id, string symbol, Direction dir, int size)
		{
			if (size < 0)
			{
				LogError("CONVERT with negative size attempted");
				return;
			}

			string msg = string.Format("CONVERT {0} {1} {2} {3}", id, symbol.ToUpper(), dir, size);

			writer.WriteLine(msg);
			writer.Flush();
			LogSend(msg);
		}

		public void Cancel(int id)
		{
			string msg = string.Format("CANCEL {0}", id);
			Console.WriteLine("SEND: " + msg);

			writer.WriteLine(msg);
			writer.Flush();
			LogSend(msg);
		}
	}
}
