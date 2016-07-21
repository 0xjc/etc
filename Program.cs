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
            Console.WriteLine("hello swj05652");
			string host = args[0];
			int port = int.Parse(args[1]);
			Console.WriteLine(string.Format("Connecting to {0}:{1}", host, port));

			var client = new TcpClient(host, port);
			var stream = client.GetStream();
			var market = new Market(stream);
			market.Hello();
			market.ReceiveLoop();
		}
	}
}
