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
			var client = new TcpClient(args[0], int.Parse(args[1]));
			var stream = client.GetStream();
			var market = new Market(stream);
			market.Hello();
			market.ReceiveLoop();
		}
	}
}
