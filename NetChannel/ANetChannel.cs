using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NetChannel
{
    public class IdCreator
    {
        private static int id;
        public static int CreateId()
        {
            Interlocked.Increment(ref id);
            Interlocked.CompareExchange(ref id, 1, int.MaxValue);
            return id;
        }
    }

    public class KcpConnectSN
    {
        private static int id;
        public static int CreateSN()
        {
            Interlocked.Increment(ref id);
            Interlocked.CompareExchange(ref id, 1, int.MaxValue);
            return id;
        }
    }

    /// <summary>
    /// 网络通道抽象类
    /// </summary>
    public abstract class ANetChannel
    {
        public int Id { get; protected set; }

        public ANetChannel(ANetService netService)
        {
            this.netService = netService;
            Id = IdCreator.CreateId();
        }

        private int rpcId;
        public int RpcId
        {
            get
            {
                Interlocked.Increment(ref rpcId);
                Interlocked.CompareExchange(ref rpcId, 1, int.MaxValue);
                return rpcId;
            }
        }

        /// <summary>
        /// RPC字典
        /// </summary>
        protected readonly ConcurrentDictionary<int, Action<Packet>> RpcDictionarys = new ConcurrentDictionary<int, Action<Packet>>();

        /// <summary>
        /// 同步多线程发送队列
        /// </summary>
        protected readonly SemaphoreSlim SendSemaphore = new SemaphoreSlim(1);

        /// <summary>
        /// 网络服务类
        /// </summary>
        public ANetService netService { get; private set; }

        /// <summary>
        /// 远程IP端口
        /// </summary>
        public EndPoint RemoteEndPoint { get; set; }

        /// <summary>
        /// 本地IP端口
        /// </summary>
        public EndPoint LocalEndPoint { get; set; }

        /// <summary>
        /// 如果是服务端则是本地监听IP端口，要是客户端则是远程连接IP端口
        /// </summary>
        public IPEndPoint DefaultEndPoint { get; protected set; }

        /// <summary>
        /// 接收包解析器
        /// </summary>
        protected PacketParser RecvParser;

        /// <summary>
        /// 最后接收心跳时间
        /// </summary>
        public DateTime LastRecvHeartbeat { get; set; } = DateTime.Now;

        /// <summary>
        /// 最后发送心跳时间
        /// </summary>
        public DateTime LastSendHeartbeat { get; protected set; }

        /// <summary>
        /// 接收回调事件
        /// </summary>
        public Action<Packet> OnReceive;

        /// <summary>
        /// 错误回调事件
        /// </summary>
        public Action<ANetChannel> OnError;

        /// <summary>
        /// 连接成功回调
        /// </summary>
        public Action<ANetChannel> OnConnect;

        /// <summary>
        /// 连接断开回调
        /// </summary>
        public Action<ANetChannel> OnDisConnect;

        /// <summary>
        /// 连接状态
        /// </summary>
        public bool Connected { get; set; }

        /// <summary>
        /// 开始连接
        /// </summary>
        /// <returns></returns>
        public abstract Task<bool> StartConnecting();

        /// <summary>
        /// 连接检测
        /// </summary>
        /// <returns></returns>
        public abstract bool CheckConnection();

        /// <summary>
        /// 重新连接
        /// </summary>
        /// <returns></returns>
        public abstract Task<bool> ReConnecting();

        /// <summary>
        /// 断开连接
        /// </summary>
        public abstract void DisConnect();

        /// <summary>
        /// 插入请求
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="recvAction"></param>
        public abstract void AddRequest(Packet packet, Action<Packet> recvAction);

        /// <summary>
        /// 把发送数据包写到缓冲区
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        public abstract void WriteSendBuffer(Packet packet);

        /// <summary>
        /// 开始发送
        /// </summary>
        public abstract Task StartSend();

        /// <summary>
        /// 开始接收数据
        /// </summary>
        /// <returns></returns>
        public abstract void StartRecv();
    }
}