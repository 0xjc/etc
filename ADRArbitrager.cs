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

        public int adrBuyOrderID;
        public int adrSellOrderID;
        public int adrAsk;
        public int adrBid;
        public int ordAsk;
        public int ordBid;

        private Market market;

        public ADRArbitrager(Market market_)
        {
            market = market_;
            adrBuyOrderID = -1;
            adrSellOrderID = -1;
            adrAsk = adrBid = ordAsk = ordBid = -1;
            canTrade = false;
        }

        public int Main()
        {
            market.GotHello += market_GotHello;
            market.Open += market_Open;
            market.Close += market_Close;
            market.Ack += market_Ack;
            market.Fill += market_Fill;
            market.Book += market_Book;

            while (true)
            {
                if (canTrade)
                {
                    int adr = market.GetPosition(adrTicker); 
                    int ord = market.GetPosition(ordTicker);
                    if (Math.Abs(adr) >= 10)
                    {
                        CancelAllOrder();
                        Direction dir = Direction.BUY;
                        if (adr > 0)
                            dir = Direction.SELL;
                        market.Convert(adrTicker, dir, Math.Abs(adr));
                        ord += adr;
                        adr = 0;
                        
                    }

                    if (ord != 0)
                    {
                        Direction dir = Direction.BUY;
                        if (ord > 0)
                        {
                            dir = Direction.SELL;
                        }
                        else
                        {
                            dir = Direction.BUY;
                        }
                        int orderId = market.Add(ordTicker, dir, ordBid, 1);
                        Task.Delay(200).Wait();
                        market.Cancel(orderId);
                    }

                    Task.Delay(200).Wait();
                }
            }
        }

        public void CancelAllOrder()
        {
            if (adrBuyOrderID >= 0)
            {
                market.Cancel(adrBuyOrderID);
                adrBuyOrderID = -1;
            }
            if (adrSellOrderID >= 0)
            {
                market.Cancel(adrSellOrderID);
                adrSellOrderID = -1;
            }
            //Task.Delay(100).Wait();
        }

        void market_Book(object sender, BookEventArgs e)
        {
            string ticker = e.symbol;
            if (ticker == ordTicker)
            {
                if ((e.buys.Count > 0) && (e.sells.Count > 0))
                {
                    CancelAllOrder();

                    ordBid = e.buys.Last().Key;
                    ordAsk = e.sells.First().Key;

                    int adr = market.GetPosition(adrTicker); 
                    //Console.WriteLine("I am adding buy order.   " + "adr = " + adr + "I am going to buy " + Math.Min(10 - adr, 10) + "shares.");
                    adrBuyOrderID = market.Add(adrTicker, Direction.BUY, ordBid - 2, Math.Max(0, Math.Min(10 - adr, 6)));
                    //Console.WriteLine("I am adding sell order.   " + "adr = " + adr + "I am going to sell " + Math.Min(10 + adr, 10) + "shares.");
                    adrSellOrderID = market.Add(adrTicker, Direction.SELL, ordAsk + 2, Math.Max(0, Math.Min(10 + adr, 6)));

                    //Task.Delay(200).Wait();
                }
            }
            if (ticker == adrTicker)
            {
                if ((e.buys.Count > 0) && (e.sells.Count > 0))
                {
                    adrBid = e.buys.Last().Key;
                    adrAsk = e.sells.First().Key;
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
                    }
                    else
                    {
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
            }
        }
    }
}
