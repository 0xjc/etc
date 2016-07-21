using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace etc
{
    class ADRArbitrager
    {
        private object thisLock = new object();
        public bool canTrade;
        public static string adrTicker = "VALE";
        public static string ordTicker = "VALBZ";

        public int cash;
        public int adr;
        public int ord;

        public int adrBuyOrderID;
        public int adrSellOrderID;

        private Market market;

        public ADRArbitrager(Market market_)
        {
            market = market_;
            adr = 0;
            ord = 0;
            cash = 0;
            adrBuyOrderID = -1;
            adrSellOrderID = -1;
            canTrade = false;
        }

        public int Main()
        {
            market.GotHello += market_GotHello;
            market.Open += market_Open;
            market.Close += market_Close;
            market.Ack += market_Ack;
            market.Fill += market_Fill;
            while (true)
            {
                if (canTrade)
                {

                }
            }
        }

        void market_Fill(object sender, FillEventArgs e)
        {
            lock (thisLock)
            {
                int id = e.id;
                    if (e.dir == Direction.BUY)
                    {
                        cash -= e.price * e.size;
                        if (e.symbol == adrTicker)
                            adr += e.size;
                        else
                            ord += e.size;
                    }
                    else
                    {
                        cash += e.price * e.size;
                        if (e.symbol == adrTicker)
                            adr -= e.size;
                        else
                            ord -= e.size;
                    }
                
            }
        }

        void market_Ack(object sender, AckEventArgs e)
        {
            lock (thisLock)
            {
                int id = e.id;
            }
        }

        void market_Close(object sender, CloseEventArgs e)
        {
            lock (thisLock)
            {
                canTrade = (!e.symbols.Contains(adrTicker)) && (!e.symbols.Contains(ordTicker));
            }
        }

        void market_Open(object sender, OpenEventArgs e)
        {
            lock (thisLock)
            {
                canTrade = e.symbols.Contains(adrTicker) && e.symbols.Contains(ordTicker);
            }
        }

        void market_GotHello(object sender, HelloEventArgs e)
        {
            lock (thisLock)
            {
                cash = e.cash;
                adr = e.positions[adrTicker];
                ord = e.positions[ordTicker];
            }
        }
    }
}
