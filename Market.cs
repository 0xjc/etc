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
		int cash;
		Dictionary<string, int> positions; // key = symbol, value = position
	}

	public class OpenEventArgs : EventArgs
	{
		List<string> symbols;
	}

	public class CloseEventArgs : EventArgs
	{
		List<string> symbols;
	}

	public class ErrorEventArgs : EventArgs
	{
		string message;
	}

	public class BookEventArgs : EventArgs
	{
		string symbol;
		SortedDictionary<int, int> buys; // key = price, value = quantity
		SortedDictionary<int, int> sells;
	}

	public class TradeEventArgs : EventArgs
	{
		string symbol;
		int price;
		int size;
	}

	public class AckEventArgs : EventArgs
	{
		int id;
	}

	public class RejectEventArgs : EventArgs
	{
		int id;
		string message;
	}

	public class FillEventArgs : EventArgs
	{
		int id;
		string symbol;
		Direction dir;
		int price;
		int size;
	}

	public class OutEventArgs : EventArgs
	{
		int id;
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
			reader = new StreamReader(stream);
			writer = new StreamWriter(stream);
		}

		public void ReceiveLoop()
		{
			string line;
			while ((line = reader.ReadLine()) != null)
			{
				Console.WriteLine("Got line: " + line);
			}
		}

		public void Hello() { writer.WriteLine("HELLO AMPERE"); }
		public void Add(int id, string symbol, Direction dir, int price, int size) { }
		public void Convert(int id, string symbol, Direction dir, int size) { }
		public void Cancel(int id) { }
	}
}
