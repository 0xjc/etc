using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace etc
{
	class EquityMarketMaker
	{
		private const string
			BOND = "BOND",
			GS = "GS",
			MS = "MS",
			WFC = "WFC",
			XLF = "XLF";

		private List<string> symbols = new List<string> { BOND, GS, MS, WFC, XLF };

		private object thisLock = new object();
		private Market market;

		private Dictionary<string, SortedDictionary<int, int>> buys;
		private Dictionary<string, SortedDictionary<int, int>> sells;
		private Dictionary<string, double> fairs;

		public EquityMarketMaker(Market market_)
		{
			market = market_;
			buys = new Dictionary<string, SortedDictionary<int, int>>();
			sells = new Dictionary<string, SortedDictionary<int, int>>();
			fairs = new Dictionary<string, double>();

			foreach (string sym in symbols)
			{
				buys[sym] = new SortedDictionary<int, int>();
				sells[sym] = new SortedDictionary<int, int>();
				fairs[sym] = 0.0; // watch out, check this
			}

			market.Book += Market_Book;
		}

		public void Main()
		{

		}

		private void Recalcluate()
		{
			foreach (string sym in symbols)
			{
				try
				{
					int bid = buys[sym].Last().Key;
					int ask = sells[sym].First().Key;
					fairs[sym] = ((double)bid + ask) / 2.0;
				}
				catch (InvalidOperationException)
				{
					// no bids / no asks
					fairs[sym] = 0.0;
				}
			}
		}

		private void Market_Book(object sender, BookEventArgs e)
		{
			lock (thisLock)
			{
				if (buys.ContainsKey(e.symbol))
				{
					buys[e.symbol] = e.buys;
					sells[e.symbol] = e.sells;
				}
			}
		}
	}
}
