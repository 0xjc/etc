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
		}

		private List<string> symbols;

		private object thisLock = new object();
		private Market market;

		private Dictionary<string, Security> secs;
		private Dictionary<string, HashSet<int>> existingOrder;

		private Direction BUY = Direction.BUY, SELL = Direction.SELL;

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
                DoConvert("RSP");
                DoConvert("XLY");
                DoConvert("XLP");
                DoConvert("XLU");
                Task.Delay(100).Wait();
                //DoSmartUnposition();
                //Task.Delay(100).Wait();
                DoUnposition();
				Task.Delay(900).Wait();
			}
		}

		private void AddOrder(string symbol, Direction direction, int price, int size)
		{
			int id = market.Add(symbol, direction, price, size);
            if (id >= 0)
            {
                existingOrder[symbol].Add(id);
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
                AddOrder("RSP", SELL, (int)Math.Ceiling(rsp_sell), Math.Min(20, 100 + market.GetPosition("RSP")));
				AddOrder("RSP", BUY, (int)Math.Floor(rsp_buy), Math.Min(20, 100 - market.GetPosition("RSP")));
				AddOrder("RSP", SELL, (int)Math.Ceiling(rsp_sell + rsp.mid * 10 / 10000), Math.Min(20, 100 + market.GetPosition("RSP")));
				AddOrder("RSP", BUY, (int)Math.Floor(rsp_buy - rsp.mid * 10 / 10000), Math.Min(20, 100 - market.GetPosition("RSP")));
				AddOrder("RSP", SELL, (int)Math.Ceiling(rsp_sell + rsp.mid * 30 / 10000), Math.Min(20, 100 + market.GetPosition("RSP")));
				AddOrder("RSP", BUY, (int)Math.Floor(rsp_buy - rsp.mid * 30 / 10000), Math.Min(20, 100 - market.GetPosition("RSP")));

            }
		}

		private void DoConvert(string ETFSymbol)
		{
			lock (thisLock)
			{
                int rspPos = market.GetPosition(ETFSymbol);
                if (rspPos >= 80)
				{
                    market.Convert(ETFSymbol, Direction.SELL, 80);
				}
                else if (rspPos <= -80)
				{
                    market.Convert(ETFSymbol, Direction.BUY, 80);
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
                if (sec.ask > 0 && sec.bid > 0)
                {

                    int synpos = Synpos(memberIndex);
                    if (synpos > 5)
                    {
                        AddOrder(symbol, SELL, (8 * sec.ask + 0 * sec.bid + 2 * sec.mid) / 10, Math.Abs(synpos) + 1);
                    }
                    else if (synpos < -5)
                    {
                        AddOrder(symbol, BUY, (0 * sec.ask + 8 * sec.bid + 2 * sec.mid) / 10, Math.Abs(synpos) + 1);
                    }
                    else
                    {
                        if ((sec.ask - sec.bid) > sec.mid * 5 / 10000)
                        {
                            AddOrder(symbol, BUY, sec.bid + 1, 2);
                            AddOrder(symbol, SELL, sec.ask - 1, 2);
                        }
                    }
                    AddOrder(symbol, BUY, sec.bid - sec.mid * 10 / 10000, 2);
                    AddOrder(symbol, SELL, sec.ask + sec.mid * 10 / 10000, 2);
                    AddOrder(symbol, BUY, sec.bid - sec.mid * 30 / 10000, 5);
                    AddOrder(symbol, SELL, sec.ask + sec.mid * 30 / 10000, 5);
                    AddOrder(symbol, BUY, sec.bid - sec.mid * 100 / 10000, 12);
                    AddOrder(symbol, SELL, sec.ask + sec.mid * 100 / 10000, 12);
                }
                else
                {
                    if (sec.ask > 0)
                    {
                        AddOrder(symbol, SELL, sec.ask + sec.ask * 10 / 10000, 5);
                        AddOrder(symbol, BUY, sec.ask / 2, 25);
                    }
                    else if (sec.bid > 0)
                    {
                        AddOrder(symbol, BUY, sec.bid - sec.bid * 10 / 10000, 5);
                        AddOrder(symbol, SELL, sec.bid * 2, 25);
                    }
                }
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
            lock (thisLock)
            {
                Security sec = secs["XLY"];
                if (sec.ask > 0 && sec.bid > 0)
                {
                    if (Synpos(0) > 0 && Synpos(1) > 0 && Synpos(2) > 0)
                    {
                        AddOrder("XLY", Direction.SELL, secs["XLY"].mid, 4);
                    }
                    else if (Synpos(0) < 0 && Synpos(1) < 0 && Synpos(2) < 0)
                    {
                        AddOrder("XLY", Direction.BUY, secs["XLY"].mid, 4);
                    }
                    else
                    {
                        if ((sec.ask - sec.bid) > sec.mid * 5 / 10000)
                        {
                            AddOrder("XLY", BUY, sec.bid + 1, 2);
                            AddOrder("XLY", SELL, sec.ask - 1, 2);
                        }
                    }
                    AddOrder("XLY", BUY, sec.bid - sec.mid * 10 / 10000, 2);
                    AddOrder("XLY", SELL, sec.ask + sec.mid * 10 / 10000, 2);
                    AddOrder("XLY", BUY, sec.bid - sec.mid * 30 / 10000, 5);
                    AddOrder("XLY", SELL, sec.ask + sec.mid * 30 / 10000, 5);
                }
                else
                {
                    if (sec.ask > 0)
                    {
                        AddOrder("XLY", SELL, sec.ask + sec.ask * 10 / 10000, 5);
                        AddOrder("XLY", BUY, sec.ask / 2, 25);
                    }
                    else if (sec.bid > 0)
                    {
                        AddOrder("XLY", BUY, sec.bid - sec.bid * 10 / 10000, 5);
                        AddOrder("XLY", SELL, sec.bid * 2, 25);
                    }
                }

                sec = secs["XLP"];
                if (sec.ask > 0 && sec.bid > 0)
                {
                    if (Synpos(3) > 0 && Synpos(4) > 0 && Synpos(5) > 0)
                    {
                        AddOrder("XLP", Direction.SELL, secs["XLP"].mid, 4);
                    }
                    else if (Synpos(3) < 0 && Synpos(4) < 0 && Synpos(5) < 0)
                    {
                        AddOrder("XLP", Direction.BUY, secs["XLP"].mid, 4);
                    }
                    else
                    {
                        if ((sec.ask - sec.bid) > sec.mid * 5 / 10000)
                        {
                            AddOrder("XLP", BUY, sec.bid + 1, 2);
                            AddOrder("XLP", SELL, sec.ask - 1, 2);
                        }
                    }
                    AddOrder("XLP", BUY, sec.bid - sec.mid * 10 / 10000, 2);
                    AddOrder("XLP", SELL, sec.ask + sec.mid * 10 / 10000, 2);
                    AddOrder("XLP", BUY, sec.bid - sec.mid * 30 / 10000, 5);
                    AddOrder("XLP", SELL, sec.ask + sec.mid * 30 / 10000, 5);
                }
                else
                {
                    if (sec.ask > 0)
                    {
                        AddOrder("XLP", SELL, sec.ask + sec.ask * 10 / 10000, 5);
                        AddOrder("XLP", BUY, sec.ask / 2, 25);
                    }
                    else if (sec.bid > 0)
                    {
                        AddOrder("XLP", BUY, sec.bid - sec.bid * 10 / 10000, 5);
                        AddOrder("XLP", SELL, sec.bid * 2, 25);
                    }
                }

                sec = secs["XLU"];
                if (sec.ask > 0 && sec.bid > 0)
                {
                    if (Synpos(6) > 0 && Synpos(7) > 0 && Synpos(8) > 0)
                    {
                        AddOrder("XLU", Direction.SELL, secs["XLU"].mid, 4);
                    }
                    else if (Synpos(6) < 0 && Synpos(7) < 0 && Synpos(8) < 0)
                    {
                        AddOrder("XLU", Direction.BUY, secs["XLU"].mid, 4);
                    }
                    else
                    {
                        if ((sec.ask - sec.bid) > sec.mid * 5 / 10000)
                        {
                            AddOrder("XLU", BUY, sec.bid + 1, 2);
                            AddOrder("XLU", SELL, sec.ask - 1, 2);
                        }
                    }
                    AddOrder("XLU", BUY, sec.bid - sec.mid * 10 / 10000, 2);
                    AddOrder("XLU", SELL, sec.ask + sec.mid * 10 / 10000, 2);
                    AddOrder("XLU", BUY, sec.bid - sec.mid * 30 / 10000, 5);
                    AddOrder("XLU", SELL, sec.ask + sec.mid * 30 / 10000, 5);
                }
                else
                {
                    if (sec.ask > 0)
                    {
                        AddOrder("XLU", SELL, sec.ask + sec.ask * 10 / 10000, 5);
                        AddOrder("XLU", BUY, sec.ask / 2, 25);
                    }
                    else if (sec.bid > 0)
                    {
                        AddOrder("XLU", BUY, sec.bid - sec.bid * 10 / 10000, 5);
                        AddOrder("XLU", SELL, sec.bid * 2, 25);
                    }
                }
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
