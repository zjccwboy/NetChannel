using NetChannel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestKcpServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8989);
            var session = new Session(endPoint, ProtocalType.Kcp);
            session.Accept();
            while (true)
            {
                session.Update();
                Thread.Sleep(1);
            }
        }
    }
}
