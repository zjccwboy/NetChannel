using NetChannel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestKcpServer
{
    public class TestMergeHandler : IMessageHandler
    {
        public ANetChannel Channel { get; set; }
        public ANetService NetService { get; set; }

        public void DoReceive(Packet packet)
        {
            NetService.Session.Notice(Channel, packet);
        }
    }
}