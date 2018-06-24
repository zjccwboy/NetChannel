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
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var send = new Packet { Data = BitConverter.GetBytes(999) };
            while (true)
            {
                if (channel.Connected)
                {
                    int sendCount = 1000;
                    int count = 0;
                    int i = 0;
                    while (true)
                    {
                        if (!channel.Connected)
                        {
                            break;
                        }
                        if(i < sendCount)
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
                                if (count == sendCount)
                                {
                                    Console.WriteLine(" {0}毫秒钟响应请求:{1}/条", stopwatch.ElapsedMilliseconds, count);
                                }
                            });
                        }
                        else
                        {
                            Thread.Sleep(1);
                        }
                        if(count == sendCount)
                        {
                            break;
                        }
                        i++;
                    }
                }
                Thread.Sleep(1000);
                stopwatch.Restart();
            }
        }
    }
}
