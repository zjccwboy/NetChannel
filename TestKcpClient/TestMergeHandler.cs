using NetChannel;
using System;
using System.Diagnostics;
using System.Threading;

namespace TestKcpClient
{
    public class TestMergeHandler : IMessageHandler
    {
        public ANetChannel Channel { get; set; }
        public ANetService NetService { get; set; }

        Stopwatch stopwatch = new Stopwatch();

        public TestMergeHandler()
        {
            stopwatch.Start();
        }

        int count = 1;
        public void DoReceive(Packet packet)
        {
            var data = BitConverter.ToInt32(packet.Data, 0);
            if (data != 999)
            {
                Console.WriteLine($"解包出错:{data}");
                Console.Read();
            }
            Interlocked.Increment(ref count);
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                stopwatch.Restart();
                Console.WriteLine(" 一秒钟响应请求:{0}/条", count);
                Interlocked.Exchange(ref count, 1);
            }
        }
    }
}
