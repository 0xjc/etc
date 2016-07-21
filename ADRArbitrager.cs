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

        public int adr;
        public int ord;

        private Market market;

        public ADRArbitrager(Market market_)
        {
            market = market_;
        }


    }
}
