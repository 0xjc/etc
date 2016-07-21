using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace etc
{
	class ETFArbitrager
	{
		class Security
		{
			public string symbol;
			public SortedDictionary<int, int> buys;
			public SortedDictionary<int, int> sells;
			public int lastTradePrice; // 0 to start with
			public DateTime? lastTradeTime; // null to start with
			public double fair; // estimate; 0.0 if unable to calculate (WARNING!)
			
			public Security(string symbol_)
			{
				symbol = symbol_;
				buys = new SortedDictionary<int, int>();
				sells = new SortedDictionary<int, int>();
				if (symbol == "BOND") { fair = 1000.0; }
			}

			public int Bid()
			{
				if (buys.Count == 0) return 0;
				return buys.Last().Key;
			}

			public int Ask()
			{
				if (sells.Count == 0) return 0;
				return sells.First().Key;
			}

			public void RecalcFair()
			{
				if (symbol == "BOND") return;

				// weight lastTradePrice and mid based on lastTradeTime recency
				double lastTradeWeight = 0.0;
				if (lastTradeTime != null)
				{
					TimeSpan diff = DateTime.Now - lastTradeTime.Value;
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

		class Order
		{
			public string symbol;
			public Direction direction;
			public int price;
			public int size;

			public Order(string symbol_, Direction direction_, int price_, int size_)
			{
				symbol = symbol_;
				direction = direction_;
				price = price_;
				size = size_;
			}
		}

		private List<string> symbols = new List<string> { "BOND", "GS", "MS", "WFC", "XLF" };

		private object thisLock = new object();
		private Market market;

		private Dictionary<string, Security> secs;
		private Dictionary<string, int> prevOrder;

		public ETFArbitrager(Market market_)
		{
			market = market_;
			secs = new Dictionary<string, Security>();
			prevOrder = new Dictionary<string, int>();

			foreach (string sym in symbols)
			{
				secs[sym] = new Security(sym);
				prevOrder[sym] = Market.INVALID_ID;
			}

			market.Book += Market_Book;
			market.Trade += Market_Trade;
		}

		public void Main()
		{
			while (true)
			{
				Readjust();
				Task.Delay(2000).Wait();
			}
		}
		
		private void CancelPrevOrder(string symbol)
		{
			if (prevOrder[symbol] != Market.INVALID_ID)
			{
				market.Cancel(prevOrder[symbol]);
				prevOrder[symbol] = Market.INVALID_ID;
			}
		}

		private void DoArb()
		{
			lock (thisLock)
			{
				Security xlf = secs["XLF"];
				Security bond = secs["BOND"];
				Security gs = secs["GS"];
				Security ms = secs["MS"];
				Security wfc = secs["WFC"];

				if (xlf.fair == 0.0) return;
				if (bond.fair == 0.0) return;
				if (gs.fair == 0.0) return;
				if (ms.fair == 0.0) return;
				if (wfc.fair == 0.0) return;

				double diff = xlf.fair - 0.3 * bond.fair
					- 0.2 * gs.fair - 0.3 * ms.fair - 0.2 * wfc.fair;

				CancelPrevOrder("XLF");

				if (diff > 7.0)
				{
					prevOrder["XLF"] = market.Add("XLF", Direction.SELL, (int)Math.Round(xlf.fair), 10);
				}
				else if (diff < -7.0)
				{
					prevOrder["XLF"] = market.Add("XLF", Direction.BUY, (int)Math.Round(xlf.fair), 10);
				}
			}
		}

		private void DoConvert()
		{
			lock (thisLock)
			{
				int xlfPos = market.GetPosition("XLF");
				if (xlfPos >= 30)
				{
					market.Convert("XLF", Direction.SELL, 30);
				}
				else if (xlfPos <= -30)
				{
					market.Convert("XLF", Direction.BUY, 30);
				}
			}
		}

		private void UnpositionOne(string symbol)
		{
			var sec = secs[symbol];
			int pos = market.GetPosition(symbol);
			CancelPrevOrder(symbol);
			if (pos > 5)
			{
				double fair = sec.fair;
				int ask = sec.Ask();
				if (fair != 0.0)
				{
					int price = (ask == 0) ? ((int)Math.Round(fair) + 1) : (int)(Math.Round((fair + ask) / 2.0));
					prevOrder[symbol] = market.Add(symbol, Direction.SELL, price, pos / 2);
				}
			}
			else if (pos < -5)
			{
				double fair = sec.fair;
				int bid = sec.Bid();
				if (fair != 0.0)
				{
					int price = (bid == 0) ? ((int)Math.Round(fair) - 1) : (int)(Math.Round((fair + bid) / 2.0));
					prevOrder[symbol] = market.Add(symbol, Direction.BUY, price, pos / 2);
				}
			}
		}

		private void DoUnposition()
		{
			lock (thisLock)
			{
				UnpositionOne("GS");
				UnpositionOne("MS");
				UnpositionOne("WFC");
			}
		}

		private void Readjust()
		{
			lock (thisLock)
			{
				foreach (string sym in symbols)
				{
					var sec = secs[sym];
					sec.RecalcFair();
				}

				DoArb();
				DoConvert();
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
