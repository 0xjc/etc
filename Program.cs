using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace etc
{
	class Program
	{
		static void Main(string[] args)
		{
			string host = args[0];
			int port = int.Parse(args[1]);
			Console.WriteLine(string.Format("Connecting to {0}:{1}", host, port));
			string runID = string.Format("{0}-{1}", host, port);

			var client = new TcpClient(host, port);
			var stream = client.GetStream();
			var market = new Market(stream, runID);

			Task.Run(() => market.SendLoop());

			market.Hello();

			//var bonds = new BondMarketMaker(market);
			//Task.Run(() => { bonds.Main(); });

			//var adr = new ADRArbitrager(market);
			//Task.Run(() => { adr.Main(); });

			var etf = new ETFArbitrager(market);
			Task.Run(() => { etf.Main(); });

			market.ReceiveLoop();
		}
	}
}
