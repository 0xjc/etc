using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace etc
{
    class BondMarketMaker
    {
        public int cash;
        public int bond;
        public int order;

        private Market market;

        public BondMarketMaker(Market market_)
        {
            market = market_;
            order = 0;
        }

        public int Main()
        {
            market.GotHello += market_GotHello;

            while (true)
            {
                market.Add(order, "BOND", Direction.BUY, 999, 20);
                order++;
                market.Add(order, "BOND", Direction.SELL, 1001, 20);
                order++;
            }

            return 0;
        }

        void market_GotHello(object sender, HelloEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
