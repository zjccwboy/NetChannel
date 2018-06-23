using NetChannel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppTest1
{
    public class TestMergeHandler : IMessageHandler
    {
        public ANetChannel Channel { get; set; }
        public ANetService NetService { get; set; }

        public async void DoReceive(Packet packet)
        {
            await Channel.SendAsync(packet);
            //NetService.SendQueue.Enqueue(new SendTrough
            //{
            //    Channel = this.Channel,
            //    Packet = packet,
            //});
        }
    }
}
