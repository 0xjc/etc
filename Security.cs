using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace etc
{
	class Security
	{
		private object thisLock = new object();
		public string symbol;
		public SortedDictionary<int, int> buys;
		public SortedDictionary<int, int> sells;
		public int bid; // -1 = invalid
		public int ask; // -1 = invalid
		public int mid; // -1 = invalid
		public int lastTradePrice; // -1 = invalid
		public int lastTradeSize; // -1 = invalid
		public DateTime lastTradeTime; // DateTime.MinValue to start with

		public int rspWeight;
		public int xlyWeight;
		public int xlpWeight;
		public int xluWeight;

		public Security(string symbol_)
		{
			symbol = symbol_;
			buys = new SortedDictionary<int, int>();
			sells = new SortedDictionary<int, int>();
			//if (symbol == "BOND") { fair = 1000.0; }
			bid = ask = mid = lastTradePrice = -1;

			rspWeight = xlyWeight = xlpWeight = xluWeight = 0;
			switch (symbol)
			{
				case "AMZN": rspWeight = 3; xlyWeight = 6; break;
				case "HD": rspWeight = 6; xlyWeight = 6; break;
				case "DIS": rspWeight = 8; xlyWeight = 8; break;
				case "PG": rspWeight = 6; xlpWeight = 12; break;
				case "KO": rspWeight = 12; xlpWeight = 12; break;
				case "PM": rspWeight = 6; xlpWeight = 6; break;
				case "NEE": rspWeight = 4; xluWeight = 8; break;
				case "DUK": rspWeight = 6; xluWeight = 6; break;
				case "SO": rspWeight = 8; xluWeight = 8; break;
				case "XLY": break;
				case "XLP": break;
				case "XLU": break;
				case "RSP": break;
				default: Console.Error.WriteLine(string.Format("----- UNKNOWN SYMBOL: {0} -----", symbol)); break;
			}
		}

		public object GetLock()
		{
			return thisLock;
		}
	
		public void OnBook(BookEventArgs args)
		{
			if (args.symbol != symbol) return;
			lock (thisLock)
			{
				buys = args.buys;
				sells = args.sells;
				bid = Bid();
				ask = Ask();
				mid = Mid();
			}
		}

		public void OnTrade(TradeEventArgs args)
		{
			if (args.symbol != symbol) return;
			var now = DateTime.Now;
			lock (thisLock)
			{
				lastTradePrice = args.price;
				lastTradeSize = args.size;
				lastTradeTime = now;
			}
		}

		private int Bid()
		{
			if (buys.Count == 0) return -1;
			return buys.Last().Key;
		}

		private int Ask()
		{
			if (sells.Count == 0) return -1;
			return sells.First().Key;
		}

		private int Mid()
		{
			if (bid < 0 || ask < 0) return -1;
			return (bid + ask) / 2;
		}

		public void UpdateFrom(Security source)
		{
			buys = new SortedDictionary<int, int>(source.buys);
			sells = new SortedDictionary<int, int>(source.sells);
			bid = source.bid;
			ask = source.ask;
			mid = source.mid;
			lastTradePrice = source.lastTradePrice;
			lastTradeSize = source.lastTradeSize;
			lastTradeTime = source.lastTradeTime;
		}
	}
}
