using NetChannel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Common;

namespace MergeClient
{
    class Program
    {
        static void Main(string[] args)
        {
            var endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 8989);
            var session = new Session(endPoint, ProtocalType.Tcp);
            var channel = session.Connect(); ;
            stopwatch.Start();
            while (true)
            {
                Subscribe(session, channel);
                session.Update();
                Thread.Sleep(1);
            }
        }


        static Stopwatch stopwatch = new Stopwatch();
        static int sendCount = 0;
        static int recvCount = 0;
        static void Subscribe(Session session, ANetChannel channel)
        {
            //优先处理完接收的包
            if (sendCount - recvCount > 0)
            {
                if(stopwatch.ElapsedMilliseconds > 5000)
                {
                    sendCount = 0;
                    recvCount = 0;
                    stopwatch.Restart();
                }
                return;
            }

            if (!channel.Connected)
            {
                return;
            }

            var send = new Packet { Data = Encoding.UTF8.GetBytes("111111111122222222223333333333444444444455555555556666666666777777777788888888889999999999") };
            for (var i = 1; i <= 1000; i++)
            {
                sendCount++;
                session.Subscribe(send, (packet) =>
                {
                    recvCount++;
                    var data = Encoding.UTF8.GetString(packet.Data);//BitConverter.ToInt32(packet.Data, 0);
                                                                    //Console.WriteLine($"接收数据包:{data}");
                    if (data != "111111111122222222223333333333444444444455555555556666666666777777777788888888889999999999")
                    {
                        Console.WriteLine($"解包出错:{data}");
                        //Console.Read();
                    }
                    if (recvCount % 1000 == 0)
                    {
                        //Console.WriteLine($"当前线程ID:{Thread.CurrentThread.ManagedThreadId}");
                        LogRecord.Log(LogLevel.Info, "数据响应测试", $"响应:{1000}个包耗时{stopwatch.ElapsedMilliseconds}毫秒");
                        Thread.Sleep(1000);
                        stopwatch.Restart();
                    }
                });
            }
        }
    }
}
