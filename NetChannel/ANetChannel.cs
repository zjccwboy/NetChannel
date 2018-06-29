﻿using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NetChannel
{
    /// <summary>
    /// 通讯管道抽象类
    /// </summary>
    public abstract class ANetChannel
    {
        /// <summary>
        /// 通讯管道Id标识
        /// </summary>
        public uint Id { get; protected set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="netService">网络通讯服务对象</param>
        public ANetChannel(ANetService netService)
        {
            this.netService = netService;
            Id = ChannelIdCreator.CreateId();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="netService">网络通讯服务对象</param>
        /// <param name="conv">KCP连接确认号Conv</param>
        public ANetChannel(ANetService netService, uint conv)
        {
            this.netService = netService;
            Id = conv;
        }

        private int rpcId;
        /// <summary>
        /// RPC请求Id生成器
        /// </summary>
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
        public IPEndPoint RemoteEndPoint { get; set; }

        /// <summary>
        /// 本地IP端口
        /// </summary>
        public IPEndPoint LocalEndPoint { get; set; }

        /// <summary>
        /// 接收包解析器
        /// </summary>
        protected PacketParser RecvParser;

        /// <summary>
        /// 接收最后一个数据包时间
        /// </summary>
        public uint LastRecvTime { get; set; } = TimeUitls.Now();

        /// <summary>
        /// 最后发送时间
        /// </summary>
        public uint LastSendTime { get; set; } = TimeUitls.Now();

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
        /// 添加一个发送数据包到发送缓冲区队列中
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="recvAction"></param>
        public abstract void AddPacket(Packet packet, Action<Packet> recvAction);

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