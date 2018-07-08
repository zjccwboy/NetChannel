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
        static int successful = 0;
        static void Subscribe(Session session, ANetChannel channel)
        {
            //优先处理完接收的包
            if (sendCount - recvCount > 0)
            {
                //if (stopwatch.ElapsedMilliseconds > 5000)
                //{
                //    sendCount = 0;
                //    recvCount = 0;
                //    stopwatch.Restart();
                //}
                return;
            }

            if (!channel.Connected)
            {
                return;
            }

            var send = new Packet { Data = Encoding.UTF8.GetBytes("111111111122222222223333333333444444444455555555556666666666777777777788888888889999999999") };
            for (var i = 1; i <= 10; i++)
            {
                sendCount++;
                session.Subscribe(send, (packet) =>
                {
                    successful++;
                    recvCount++;
                    var data = Encoding.UTF8.GetString(packet.Data);
                    if (data != "111111111122222222223333333333444444444455555555556666666666777777777788888888889999999999")
                    {
                        Console.WriteLine($"解包出错:{data}");
                    }
                    if (recvCount % 10 == 0)
                    {
                        sendCount = 0;
                        recvCount = 0;
                        LogRecord.Log(LogLevel.Info, "数据响应测试", $"响应:{10}个包耗时{stopwatch.ElapsedMilliseconds}毫秒");
                        Thread.Sleep(100);
                        stopwatch.Restart();
                    }
                });
            }
        }
    }
}
