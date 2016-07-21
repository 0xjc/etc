using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace etc
{
	class EquityMarketMaker
	{
		private Market market;
		private string symbol;
		private SortedDictionary<int, int> buys;
		private SortedDictionary<int, int> sells;

		public EquityMarketMaker(Market market_, string symbol_)
		{
			market = market_;
			symbol = symbol_;
			buys = new SortedDictionary<int, int>();
			sells = new SortedDictionary<int, int>();
			market.Book += Market_Book;
		}

		private void Market_Book(object sender, BookEventArgs e)
		{
			if (e.symbol != symbol) return;
			buys = e.
			throw new NotImplementedException();
		}
	}
}
