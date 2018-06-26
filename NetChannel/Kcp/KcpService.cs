using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NetChannel
{
    public class KcpService : ANetService
    {
        private IPEndPoint endPoint;

        public KcpService(IPEndPoint endPoint, Session session) : base(session)
        {
            sendQueue = new WorkQueue(session);
            this.endPoint = endPoint;
        }

        private readonly WorkQueue sendQueue;
        internal override WorkQueue SendQueue
        {
            get
            {
                return sendQueue;
            }
        }

        public override Task AcceptAsync()
        {
            throw new NotImplementedException();
        }

        public override Task<ANetChannel> ConnectAsync()
        {
            throw new NotImplementedException();
        }
    }
}
