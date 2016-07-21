using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace etc
{
    class BondMarketMaker
    {
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
                    AddOrder("BOND", Direction.BUY, 999, 20);
                    AddOrder("BOND", Direction.SELL, 1001, 20);
                }
            }

            return 0;
        }

        void market_Fill(object sender, FillEventArgs e)
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
                if (size>=0)
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

        void AddOrder(string symbol, Direction dir, int price, int size)
        {
            market.Add(orderID, symbol, dir, price, size);
            orderDir.Add(orderID, dir);
            orderPrice.Add(orderID, price);
            orderSize.Add(orderID, size);
            orderID++;
        }

        void market_Close(object sender, CloseEventArgs e)
        {
            canTradeBond = !e.symbols.Contains("BOND");
        }

        void market_Open(object sender, OpenEventArgs e)
        {
            canTradeBond = e.symbols.Contains("BOND");
        }

        void market_GotHello(object sender, HelloEventArgs e)
        {
            cash = e.cash;
            bond = e.positions["BOND"];
        }
    }
}
