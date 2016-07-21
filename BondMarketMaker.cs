using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace etc
{
    class BondMarketMaker
    {
		private object thisLock = new object();
        public bool canTradeBond;
        public int cash;
        public int bond;
        public int orderID;

        public Dictionary<int, Direction> orderDir;
        public Dictionary<int, int> orderPrice;
        public Dictionary<int, int> orderSize;

        private Market market;

        public BondMarketMaker(Market market_)
        {
            market = market_;
            orderID = 0;
            canTradeBond = false;
            cash = 0;
            bond = 0;
            orderDir = new Dictionary<int, Direction>();
            orderPrice = new Dictionary<int, int>();
            orderSize = new Dictionary<int, int>();
        }

        public int Main()
        {
            market.GotHello += market_GotHello;
            market.Open += market_Open;
            market.Close += market_Close;
            market.Fill += market_Fill;

            while (true)
            {
                if (canTradeBond)
                {
                    if (bond <= 80)
                    {
                        AddOrder("BOND", Direction.BUY, 999, 20);
                    }
                    else
                    {
                        AddOrder("BOND", Direction.BUY, 1000, 5);
                    }

                    if (bond >= -80)
                    {
                        AddOrder("BOND", Direction.SELL, 1001, 20);
                    }
                    else
                    {
                        AddOrder("BOND", Direction.SELL, 1000, 5);
                    }					
                }
            }
        }

        void market_Fill(object sender, FillEventArgs e)
        {
			lock (thisLock)
			{
				int id = e.id;
				if (e.symbol == "BOND")
				{
					if (e.dir == Direction.BUY)
					{
						cash -= e.price * e.size;
						bond += e.size;
					}
					else
					{
						cash += e.price * e.size;
						bond -= e.size;
					}
				}
				if (orderDir.ContainsKey(id))
				{
					Direction dir = orderDir[id];
					int price = orderPrice[id];
					int size = orderSize[id];
					size -= e.size;
					if (size >= 0)
					{
						orderSize.Remove(id);
						orderSize.Add(id, size);
					}
					else
					{
						orderDir.Remove(id);
						orderPrice.Remove(id);
						orderSize.Remove(id);
					}
				}
			}
        }

        void AddOrder(string symbol, Direction dir, int price, int size)
		{
			lock (thisLock)
			{
				market.Add(orderID, symbol, dir, price, size);
				orderDir.Add(orderID, dir);
				orderPrice.Add(orderID, price);
				orderSize.Add(orderID, size);
				orderID++;

				Task.Delay(10).Wait();
			}
        }

        void market_Close(object sender, CloseEventArgs e)
		{
			lock (thisLock)
			{
				canTradeBond = !e.symbols.Contains("BOND");
			}
        }

        void market_Open(object sender, OpenEventArgs e)
		{
			lock (thisLock)
			{
				canTradeBond = e.symbols.Contains("BOND");
			}
        }

        void market_GotHello(object sender, HelloEventArgs e)
		{
			lock (thisLock)
			{
				cash = e.cash;
				bond = e.positions["BOND"];
			}
        }
    }
}
