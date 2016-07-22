using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace etc
{
	class ETFArbitrager
	{
		public class Security
		{
			public string symbol;
			public SortedDictionary<int, int> buys;
			public SortedDictionary<int, int> sells;
            public int ask; // -1 to start with
            public int bid; // -1 to start with
            public int mid;
			public int lastTradePrice; // -1 to start with
			public DateTime? lastTradeTime; // null to start with
			//public double fair; // estimate; -1.0 if unable to calculate (WARNING!)

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
                ask = bid = -1;
                lastTradePrice = -1;
                lastTradeTime = null;
                rspWeight = xlyWeight = xlpWeight = xluWeight = 0;
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
            public int Mid()
            {
                if (bid<0 || ask<0)
                {
                    return -1;
                }
                return (int)((ask+bid)/2);
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

		private List<string> symbols;

		private object thisLock = new object();
		private Market market;

		private Dictionary<string, Security> secs;
		private Dictionary<string, HashSet<int>> existingOrder;

        public static int MEMBER_COUNT = 9;
        public int RSP_DIVISOR = 20;
        public int XLY_DIVISOR = 20;
        public int XLP_DIVISOR = 20;
        public int XLU_DIVISOR = 20;
        public int RSP_EDGE = 2;

        public Security rsp;
        public Security[] members = new Security[MEMBER_COUNT];
        public Dictionary<string, int> memberID;

		public ETFArbitrager(Market market_)
		{
			market = market_;
			symbols = market.Symbols;
			secs = new Dictionary<string, Security>();
            existingOrder = new Dictionary<string, HashSet<int>>();

			foreach (string sym in symbols)
			{
				secs[sym] = new Security(sym);
                existingOrder[sym] = new HashSet<int>();
			}
            rsp = secs["RSP"];
            members[0] = secs["AMZN"]; members[0].rspWeight = 3; members[0].xlyWeight = 6;
            members[1] = secs["HD"]; ; members[1].rspWeight = 6; members[1].xlyWeight = 6;
            members[2] = secs["DIS"]; members[2].rspWeight = 8; members[2].xlyWeight = 8;
            members[3] = secs["PG"]; members[3].rspWeight = 6; members[3].xlpWeight = 12;
            members[4] = secs["KO"]; members[4].rspWeight = 12; members[4].xlpWeight = 12;
            members[5] = secs["PM"]; members[5].rspWeight = 6; members[5].xlpWeight = 6;
            members[6] = secs["NEE"]; members[6].rspWeight = 4; members[6].xluWeight = 8;
            members[7] = secs["DUK"]; members[7].rspWeight = 6; members[7].xluWeight = 6;
            members[8] = secs["SO"]; members[8].rspWeight = 8; members[8].xluWeight = 8;

			market.Book += Market_Book;
			market.Trade += Market_Trade;
		}

		public void Main()
		{
			while (true)
			{
                CancelExistingOrderOnAll();
                Task.Delay(200).Wait();
                DoArb();
                Task.Delay(100).Wait();
                DoConvert();
                Task.Delay(100).Wait();
                //DoSmartUnposition();
                //Task.Delay(100).Wait();
                DoUnposition();
				Task.Delay(900).Wait();
			}
		}
		

        private void CancelExistingOrderOnAll()
        {
            foreach (string sym in symbols)
            {
                CancelExistingOrder(sym);
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
                    }
                }
                existingOrder[symbol].Clear();
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
                    rsp_buy += (double)(members[i].bid) * (double)members[i].rspWeight / (double)RSP_DIVISOR;
                    rsp_sell += (double)(members[i].ask) * (double)members[i].rspWeight / (double)RSP_DIVISOR;
                }

                existingOrder["RSP"].Add(market.Add("RSP", Direction.SELL, (int)Math.Ceiling(rsp_sell), Math.Min(20, 100 + market.GetPosition("RSP"))));
                existingOrder["RSP"].Add(market.Add("RSP", Direction.BUY, (int)Math.Floor(rsp_buy), Math.Min(20, 100 - market.GetPosition("RSP"))));
                existingOrder["RSP"].Add(market.Add("RSP", Direction.SELL, (int)Math.Ceiling(rsp_sell + rsp.mid * 10 / 10000), Math.Min(20, 100 + market.GetPosition("RSP"))));
                existingOrder["RSP"].Add(market.Add("RSP", Direction.BUY, (int)Math.Floor(rsp_buy - rsp.mid * 10 / 10000), Math.Min(20, 100 - market.GetPosition("RSP"))));
                existingOrder["RSP"].Add(market.Add("RSP", Direction.SELL, (int)Math.Ceiling(rsp_sell + rsp.mid * 30 / 10000), Math.Min(20, 100 + market.GetPosition("RSP"))));
                existingOrder["RSP"].Add(market.Add("RSP", Direction.BUY, (int)Math.Floor(rsp_buy - rsp.mid * 30 / 10000), Math.Min(20, 100 - market.GetPosition("RSP"))));
            }
		}

		private void DoConvert()
		{
			lock (thisLock)
			{
                int rspPos = market.GetPosition("RSP");
                if (rspPos >= 80)
				{
                    market.Convert("RSP", Direction.SELL, 80);
				}
                else if (rspPos <= -80)
				{
                    market.Convert("RSP", Direction.BUY, 80);
				}
			}
		}

        public int Synpos(int memberIndex)
        {
            lock (thisLock)
            {
                string symbol = members[memberIndex].symbol;
                var sec = secs[symbol];
                int pos = market.GetPosition(symbol);
                int synpos = pos + (int)Math.Round( (double)(market.GetPosition("RSP") * members[memberIndex].rspWeight) / ((double)RSP_DIVISOR) +
                     (double)(market.GetPosition("XLY") * members[memberIndex].xlyWeight) / ((double)XLY_DIVISOR) +
                     (double)(market.GetPosition("XLP") * members[memberIndex].xlpWeight) / ((double)XLP_DIVISOR) +
                     (double)(market.GetPosition("XLU") * members[memberIndex].xluWeight) / ((double)XLU_DIVISOR) );
                return synpos;
            }
        }

		private void UnpositionOne(int memberIndex)
		{
            lock (thisLock)
            {
                string symbol = members[memberIndex].symbol;
                var sec = secs[symbol];
                if (sec.ask < 0 || sec.bid < 0)
                    return;

                int synpos = Synpos(memberIndex);
                if (synpos > 5)
                {
                    existingOrder[symbol].Add(market.Add(symbol, Direction.SELL, (3 * sec.ask + 0 * sec.bid + 7 * sec.mid) / 10, Math.Abs(synpos) + 1));
                }
                else if (synpos < -5)
                {
                    existingOrder[symbol].Add(market.Add(symbol, Direction.BUY, (0 * sec.ask + 3 * sec.bid + 7 * sec.mid) / 10, Math.Abs(synpos) + 1));
                }
                else
                {
                    if ((sec.ask - sec.bid)  > sec.mid * 5 / 10000)
                    {
                        existingOrder[symbol].Add(market.Add(symbol, Direction.BUY, sec.bid + 1, 2));
                        existingOrder[symbol].Add(market.Add(symbol, Direction.SELL, sec.ask - 1, 2));
                    }
                }
                existingOrder[symbol].Add(market.Add(symbol, Direction.BUY, sec.bid - sec.mid * 10 / 10000, 2));
                existingOrder[symbol].Add(market.Add(symbol, Direction.SELL, sec.ask + sec.mid * 10 / 10000, 2));
                existingOrder[symbol].Add(market.Add(symbol, Direction.BUY, sec.bid - sec.mid * 30 / 10000, 5));
                existingOrder[symbol].Add(market.Add(symbol, Direction.SELL, sec.ask + sec.mid * 30 / 10000, 5));
                existingOrder[symbol].Add(market.Add(symbol, Direction.BUY, sec.bid - sec.mid * 100 / 10000, 12));
                existingOrder[symbol].Add(market.Add(symbol, Direction.SELL, sec.ask + sec.mid * 100 / 10000, 12));
            }
		}

        private void DoUnposition()
        {
            for (int i = 0; i < MEMBER_COUNT; i++)
            {
                UnpositionOne(i);
            }
        }

        private void DoSmartUnposition()
        {
            if (Synpos(0) > 5 && Synpos(1) > 5 && Synpos(2) > 5)
            {

            }
        }

		private void Market_Book(object sender, BookEventArgs e)
		{
			Security sec;
			if (secs.TryGetValue(e.symbol, out sec))
			{
				lock (thisLock)
				{
					sec.buys = e.buys;
					sec.sells = e.sells;
					sec.ask = sec.Ask();
					sec.bid = sec.Bid();
                    sec.mid = sec.Mid();
				}
			}
		}

		private void Market_Trade(object sender, TradeEventArgs e)
		{
			Security sec;
			if (secs.TryGetValue(e.symbol, out sec))
			{
				lock (thisLock)
				{
					sec.lastTradePrice = e.price;
					sec.lastTradeTime = DateTime.Now;
				}
			}
		}
	}
}
