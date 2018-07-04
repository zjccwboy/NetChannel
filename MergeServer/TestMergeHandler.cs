using NetChannel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MergeServer
{
    public class TestMergeHandler : IMessageHandler
    {
        public ANetChannel Channel { get; set; }
        public ANetService NetService { get; set; }

        private int recvCount = 0;

        public void DoReceive(Packet packet)
        {
            var data = Encoding.UTF8.GetString(packet.Data);
            NetService.Session.Notice(Channel, packet);
            //recvCount++;
            //Console.WriteLine($"接收到数据包数量:{recvCount}");
        }
    }
}