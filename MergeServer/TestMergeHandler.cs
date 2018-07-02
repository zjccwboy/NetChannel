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

        public void DoReceive(Packet packet)
        {
            var result = "111111111122222222223333333333444444444455555555556666666666777777777788888888889999999999";
            var compare = Encoding.UTF8.GetString(packet.Data);
            if (result != compare)
            {
                Console.WriteLine($"解包出错:{compare}");
            }

            NetService.Session.Notice(Channel, packet);
        }
    }
}