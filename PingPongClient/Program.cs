//using NetChannel;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Net;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

//namespace PingPongClient
//{
//    class Program
//    {
//        static void Main(string[] args)
//        {
//            TestPingPong();
//            Console.Read();
//        }

//        static async void TestPingPong()
//        {
//            var session = new Session();
//            session.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8887));
//            Thread.Sleep(200);

//            var stopwatch = new Stopwatch();
//            stopwatch.Start();
//            var count = 0;
//            var send = new Packet { Data = BitConverter.GetBytes(999) };
//            while (true)
//            {
//                for (var i = 0; i < 100000000; i++)
//                {
//                    var recv = await session.Request(send);
//                    var data = BitConverter.ToInt32(recv.Data, 0);
//                    if (data != 999)
//                    {
//                        Console.WriteLine($"解包出错:{data}");
//                        Console.Read();
//                    }
//                    Interlocked.Increment(ref count);
//                    if (stopwatch.ElapsedMilliseconds > 1000)
//                    {
//                        Console.WriteLine(" 一秒钟响应请求:{0}/条", count);
//                        Interlocked.Exchange(ref count, 0);
//                        stopwatch.Restart();
//                    }
//                }
//            }

//        }
//    }

//}
