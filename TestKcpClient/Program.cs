using Common;
using NetChannel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestKcpClient
{
    class Program
    {
        static void Main(string[] args)
        {
            var endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8989);
            var session = new Session(endPoint, ProtocalType.Kcp);
            var task = session.Connect(); ;
            task.Wait();
            stopwatch.Start();
            while (true)
            {
                Subscribe(session, task.Result);
                session.Start();
            }
        }


        static Stopwatch stopwatch = new Stopwatch();
        static void Subscribe(Session session, ANetChannel channel)
        {
            var send = new Packet { Data = BitConverter.GetBytes(999) };
            int count = 0;
            for (var i = 1; i <= 100; i++)
            {
                if (channel.Connected)
                {
                    if (!channel.Connected)
                    {
                        break;
                    }
                    //session.SendMessage(send);
                    session.Subscribe(send, (packet) =>
                    {
                        count++;
                        var data = BitConverter.ToInt32(packet.Data, 0);
                        if (data != 999)
                        {
                            Console.WriteLine($"解包出错:{data}");
                            Console.Read();
                        }
                        Interlocked.Increment(ref count);
                        if (count == 100)
                        {
                            //Console.WriteLine($"{stopwatch.ElapsedMilliseconds}毫秒钟响应请求:{count}/条");
                            LogRecord.Log(LogLevel.Info, "接收数据包", $"{stopwatch.ElapsedMilliseconds}毫秒钟响应请求:{count}/条");
                            stopwatch.Restart();
                        }
                    });
                }
            }

            //Thread.Sleep(1000);
            
        }
    }
}
