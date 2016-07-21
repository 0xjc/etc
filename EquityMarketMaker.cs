using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace etc
{
	class EquityMarketMaker
	{
		class Security
		{
			public string symbol;
			public SortedDictionary<int, int> buys;
			public SortedDictionary<int, int> sells;
			public int lastTradePrice; // 0 to start with
			public DateTime lastTradeTime; // null to start with
			public double fair; // estimate; 0.0 if unable to calculate (WARNING!)
			
			public Security(string symbol_)
			{
				symbol = symbol_;
				buys = new SortedDictionary<int, int>();
				sells = new SortedDictionary<int, int>();
				if (symbol == "BOND") { fair = 1000.0; }
			}

			public void RecalcFair()
			{
				if (symbol == "BOND") return;

				// weight lastTradePrice and mid based on lastTradeTime recency
				double lastTradeWeight = 0.0;
				if (lastTradeTime != null)
				{
					TimeSpan diff = DateTime.Now - lastTradeTime;
					if (diff < TimeSpan.FromSeconds(10.0))
					{
						lastTradeWeight = 1.0 - (diff.TotalSeconds / 10.0);
					}
				}

				double mid = 0.0;
				if (buys.Count > 0 && sells.Count > 0)
				{
					mid = (buys.Last().Key + sells.First().Key) / 2.0;
				}

				if (lastTradeWeight == 0.0)
				{
					if (mid == 0.0)
					{
						if (lastTradeTime != null) fair = lastTradePrice;
						else fair = 0.0;
					}
					else
					{
						fair = mid;
					}
				}
				else
				{
					if (mid == 0.0)
					{
						fair = lastTradePrice;
					}
					else
					{
						fair = lastTradePrice * lastTradeWeight
							+ mid * (1.0 - lastTradeWeight);
					}
				}
			}
		}

		private List<string> symbols = new List<string> { "BOND", "GS", "MS", "WFC", "XLF" };

		private object thisLock = new object();
		private Market market;

		private Dictionary<string, Security> secs;

		public EquityMarketMaker(Market market_)
		{
			market = market_;
			secs = new Dictionary<string, Security>();

			foreach (string sym in symbols)
			{
				secs[sym] = new Security(sym);
			}

			market.Book += Market_Book;
			market.Trade += Market_Trade;
		}

		public void Main()
		{
			while (true)
			{
				Recalculate();
				Task.Delay(50).Wait();
			}
		}

		private void Recalculate()
		{
			lock (thisLock)
			{
				foreach (string sym in symbols)
				{
					var sec = secs[sym];
					sec.RecalcFair();
				}
			}
		}

		private void Market_Book(object sender, BookEventArgs e)
		{
			lock (thisLock)
			{
				Security sec;
				if (secs.TryGetValue(e.symbol, out sec))
				{
					sec.buys = e.buys;
					sec.sells = e.sells;
				}
			}
		}

		private void Market_Trade(object sender, TradeEventArgs e)
		{
			lock (thisLock)
			{
				Security sec;
				if (secs.TryGetValue(e.symbol, out sec))
				{
					sec.lastTradePrice = e.price;
					sec.lastTradeTime = DateTime.Now;
				}
			}
		}
	}
}
