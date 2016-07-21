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
			var client = new TcpClient(args[1], int.Parse(args[2]));
			var stream = client.GetStream();
		}
	}
}
