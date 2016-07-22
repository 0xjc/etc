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
            public int ask; // -1 to start with
            public int bid; // -1 to start with
			public int lastTradePrice; // -1 to start with
			public DateTime? lastTradeTime; // null to start with
			//public double fair; // estimate; -1.0 if unable to calculate (WARNING!)
			
			public Security(string symbol_)
			{
				symbol = symbol_;
				buys = new SortedDictionary<int, int>();
				sells = new SortedDictionary<int, int>();
				//if (symbol == "BOND") { fair = 1000.0; }
                ask = bid = -1;
                lastTradePrice = -1;
			}

			public int Bid()
			{
				if (buys.Count == 0) return -1;
				return buys.Last().Key;
			}

			public int Ask()
			{
				if (sells.Count == 0) return -1;
				return sells.First().Key;
			}

			// do not use  
            /*
            public void RecalcFair()
			{
				//if (symbol == "BOND") return;

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
            */
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

		private List<string> symbols = new List<string> { "AMZN","DIS","DUK","HD","KO","NEE","PG","PM","RSP","SO","XLP","XLU","XLY" };

		private object thisLock = new object();
		private Market market;

		private Dictionary<string, Security> secs;
		private Dictionary<string, HashSet<int>> existingOrder;

        public static int MEMBER_COUNT = 9;
        public int RSP_DIVISOR = 20;
        public int RSP_EDGE = 5;
        public Security rsp;
        public Security[] members = new Security[MEMBER_COUNT];
        public string[] memberTickers = new string[MEMBER_COUNT];
        public int[] memberWeights = new int[MEMBER_COUNT];

		public ETFArbitrager(Market market_)
		{
			market = market_;
			secs = new Dictionary<string, Security>();
            existingOrder = new Dictionary<string, HashSet<int>>();

			foreach (string sym in symbols)
			{
				secs[sym] = new Security(sym);
                existingOrder[sym] = new HashSet<int>();
			}
            rsp = secs["RSP"];
            members[0] = secs["AMZN"]; memberTickers[0] = "AMZN"; memberWeights[0] = 3;
            members[1] = secs["HD"]; memberTickers[1] = "HD"; memberWeights[1] = 6;
            members[2] = secs["DIS"]; memberTickers[2] = "DIS"; memberWeights[2] = 8;
            members[3] = secs["PG"]; memberTickers[3] = "PG"; memberWeights[3] = 6;
            members[4] = secs["KO"]; memberTickers[4] = "KO"; memberWeights[4] = 12;
            members[5] = secs["PM"]; memberTickers[5] = "PM"; memberWeights[5] = 6;
            members[6] = secs["NEE"]; memberTickers[6] = "NEE"; memberWeights[6] = 4;
            members[7] = secs["DUK"]; memberTickers[7] = "DUK"; memberWeights[7] = 6;
            members[8] = secs["SO"]; memberTickers[8] = "SO"; memberWeights[8] = 8;

			market.Book += Market_Book;
			market.Trade += Market_Trade;
		}

		public void Main()
		{
			while (true)
			{
                DoArb();
                DoConvert();
                //DoUnposition();
				Task.Delay(200).Wait();
			}
		}
		
		private void CancelExistingOrder(string symbol)
		{
            lock (thisLock)
            {
                if (existingOrder[symbol].Count > 0)
                {
                    foreach (int orderId in existingOrder[symbol])
                    {
                        market.Cancel(orderId);
                        existingOrder[symbol].Remove(orderId);
                    }
                }
            }
		}

		private void DoArb()
		{
            lock (thisLock)
            {
                double rsp_buy = 0 - RSP_EDGE;
                double rsp_sell = RSP_EDGE;
                for (int i = 0; i < MEMBER_COUNT; i++)
                {
                    if (members[i].ask == Market.INVALID_ID || members[i].bid == Market.INVALID_ID)
                        return;
                    rsp_buy += (double)(members[i].bid) * (double)memberWeights[i] / (double)RSP_DIVISOR;
                    rsp_sell += (double)(members[i].ask) * (double)memberWeights[i] / (double)RSP_DIVISOR;
                }

                CancelExistingOrder("RSP");
                Task.Delay(200).Wait();
                existingOrder["RSP"].Add(market.Add("RSP", Direction.SELL, (int)Math.Ceiling(rsp_sell), Math.Min(10, 100 + market.GetPosition("RSP"))));
                existingOrder["RSP"].Add(market.Add("RSP", Direction.BUY, (int)Math.Floor(rsp_buy), Math.Min(10, 100 - market.GetPosition("RSP"))));
            }
		}

		private void DoConvert()
		{
			lock (thisLock)
			{
                int rspPos = market.GetPosition("RSP");
                if (rspPos >= 80)
				{
                    market.Convert("RSP", Direction.SELL, 20);
				}
                else if (rspPos <= -80)
				{
                    market.Convert("RSP", Direction.BUY, 20);
				}
			}
		}

		private void UnpositionOne(string symbol)
		{
            lock (thisLock)
            {
                var sec = secs[symbol];
                int pos = market.GetPosition(symbol);
                CancelExistingOrder(symbol);
                if (pos > 5)
                {
                    double fair = sec.fair;
                    int ask = sec.Ask();
                    if (fair != 0.0)
                    {
                        int price = (ask == 0) ? ((int)Math.Round(fair) + 1) : (int)(Math.Round((fair + ask) / 2.0));
                        existingOrder[symbol] = market.Add(symbol, Direction.SELL, price, pos / 2);
                    }
                }
                else if (pos < -5)
                {
                    double fair = sec.fair;
                    int bid = sec.Bid();
                    if (fair != 0.0)
                    {
                        int price = (bid == 0) ? ((int)Math.Round(fair) - 1) : (int)(Math.Round((fair + bid) / 2.0));
                        existingOrder[symbol] = market.Add(symbol, Direction.BUY, price, pos / 2);
                    }
                }
            }
		}

		private void DoUnposition()
		{
			lock (thisLock)
			{
				for (int i=0;i<MEMBER_COUNT;i++)
                {
                    UnpositionOne(memberTickers[i]);
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
                    sec.ask = sec.Ask();
                    sec.bid = sec.Bid();
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
