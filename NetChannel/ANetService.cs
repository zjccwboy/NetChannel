using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NetChannel
{
    public abstract class ANetService
    {
        public ANetService(Session session)
        {
            Session = session;
        }
        /// <summary>
        /// 连接通道池
        /// </summary>
        public readonly ConcurrentDictionary<long, ANetChannel> Channels = new ConcurrentDictionary<long, ANetChannel>();
        /// <summary>
        /// 连接通道池
        /// </summary>
        protected readonly ConcurrentDictionary<long, IEnumerable<IMessageHandler>> Handlers = new ConcurrentDictionary<long, IEnumerable<IMessageHandler>>();
        /// <summary>
        /// 消息会话
        /// </summary>
        public Session Session { get;private set; }
        /// <summary>
        /// 监听并接受Socket连接
        /// </summary>
        /// <returns></returns>
        public abstract Task AcceptAsync();
        /// <summary>
        /// 发送连接请求与创建连接
        /// </summary>
        /// <returns></returns>
        public abstract Task<ANetChannel> ConnectAsync();
        /// <summary>
        /// 发送队列
        /// </summary>
        internal abstract WorkQueue SendQueue { get;}
    }
}