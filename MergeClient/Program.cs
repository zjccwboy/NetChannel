using NetChannel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MergeClient
{
    class Program
    {
        static void Main(string[] args)
        {
            //TestNotice();
            TestSubscription();
            Console.Read();
        }

        static async void TestNotice()
        {
            var session = new Session();
            await session.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8989));
            Thread.Sleep(200);
            var send = new Packet { Data = BitConverter.GetBytes(999) };
            for(int i=0;i<10;i++)
            {
                session.SendMessage(send);
            }
        }

        static async void TestSubscription()
        {
            var session = new Session();
            var channel = await session.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8989));
            int count = 0;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var send = new Packet { Data = BitConverter.GetBytes(999) };
            while (true)
            {
                if (channel.Connected)
                {
                    for (var i = 0; i < 10000000; i++)
                    {
                        session.Subscribe(send, (packet) =>
                        {
                            var data = BitConverter.ToInt32(packet.Data, 0);
                            if (data != 999)
                            {
                                Console.WriteLine($"解包出错:{data}");
                                Console.Read();
                            }
                            Interlocked.Increment(ref count);
                            if (count == 10000000)
                            {
                                Console.WriteLine(" {0}毫秒钟响应请求:{1}/条", stopwatch.ElapsedMilliseconds, count);
                                Console.WriteLine(" 平均1秒钟响应请求:{0}/条", count / (stopwatch.ElapsedMilliseconds / 1000), count);
                            }
                            if (count > 10000000)
                            {
                                Console.WriteLine("解包出错");
                            }
                        });
                    }
                    return;
                }
                Thread.Sleep(1);
            }
        }
    }
}
