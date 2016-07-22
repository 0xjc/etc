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

		public Security(string symbol_)
		{
			symbol = symbol_;
			buys = new SortedDictionary<int, int>();
			sells = new SortedDictionary<int, int>();
			//if (symbol == "BOND") { fair = 1000.0; }
			bid = ask = mid = lastTradePrice = -1;
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

		public Security Clone()
		{
			var sec = new Security(symbol);
			lock (thisLock)
			{
				sec.buys = new SortedDictionary<int, int>(buys);
				sec.sells = new SortedDictionary<int, int>(sells);
				sec.bid = bid;
				sec.ask = ask;
				sec.mid = mid;
				sec.lastTradePrice = lastTradePrice;
				sec.lastTradeSize = lastTradeSize;
				sec.lastTradeTime = lastTradeTime;
			}
			return sec;
		}
	}
}
