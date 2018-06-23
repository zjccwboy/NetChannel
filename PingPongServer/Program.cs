using NetChannel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PingPongServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var session = new Session();
            session.Accept(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8887));
            Console.Read();
        }
    }
}
