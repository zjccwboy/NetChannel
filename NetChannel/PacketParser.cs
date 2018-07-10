using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Common;

namespace NetChannel
{
    /// <summary>
    /// 数据包体结构
    /// </summary>
    public struct Packet
    {
        /// <summary>
        /// 接收成功
        /// </summary>
        public bool IsSuccess;

        /// <summary>
        /// Rpc请求标志
        /// </summary>
        public bool IsRpc;

        /// <summary>
        /// 心跳标志
        /// </summary>
        public bool IsHeartbeat;

        /// <summary>
        /// 压缩标志
        /// </summary>
        public bool IsCompress;

        /// <summary>
        /// 加密标志
        /// </summary>
        public bool IsEncrypt;

        /// <summary>
        /// Kcp包协议
        /// </summary>
        public byte KcpProtocal;

        /// <summary>
        /// 是否时Actor
        /// </summary>
        public bool IsActorMessage;

        /// <summary>
        /// Rpc请求标识
        /// </summary>
        public int RpcId;

        /// <summary>
        /// Actor消息Id
        /// </summary>
        public uint ActorMessageId;

        /// <summary>
        /// 数据包
        /// </summary>
        public byte[] Data;

        /// <summary>
        /// 获取包头的字节数组
        /// </summary>
        /// <returns></returns>
        public byte[] GetHeadBytes()
        {            
            var bodySize = 0;
            if (Data != null)
            {
                bodySize = Data.Length;
                if (Data.Length > PacketParser.BodyMaxSize)
                {
                    throw new ArgumentOutOfRangeException();
                }
            }
            int headSize = IsRpc ? PacketParser.HeadMinSize + PacketParser.RpcFlagSize : PacketParser.HeadMinSize;
            headSize = IsActorMessage ? headSize + PacketParser.ActorIdFlagSize : headSize;
            int packetSize = headSize + bodySize;
            var sizeBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Convert.ToInt16(packetSize)));
            var bytes = new byte[headSize];
            bytes[0] = sizeBytes[0];
            bytes[1] = sizeBytes[1];
            if (IsRpc)
            {
                bytes[2] |= 1;
            }
            if (IsHeartbeat)
            {
                bytes[2] |= 1 << 1;
            }
            if (IsCompress)
            {
                bytes[2] |= 1 << 2;
            }
            if (IsEncrypt)
            {
                bytes[2] |= 1 << 3;
            }
            if (KcpProtocal > 0)
            {
                bytes[2] |= (byte)(KcpProtocal << 4);
            }
            if (IsActorMessage)
            {
                bytes[2] |= 1 << 6;
            }
            if (IsRpc)
            {
                var rpcBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Convert.ToInt32(RpcId)));
                bytes[3] = rpcBytes[0];
                bytes[4] = rpcBytes[1];
                bytes[5] = rpcBytes[2];
                bytes[6] = rpcBytes[3];
            }
            if (IsActorMessage)
            {
                var snBytes = BitConverter.GetBytes((uint)IPAddress.HostToNetworkOrder(Convert.ToInt32(ActorMessageId)));
                if (IsRpc)
                {
                    bytes[7] = snBytes[0];
                    bytes[8] = snBytes[1];
                    bytes[9] = snBytes[2];
                    bytes[10] = snBytes[3];
                }
                else
                {
                    bytes[3] = snBytes[0];
                    bytes[4] = snBytes[1];
                    bytes[5] = snBytes[2];
                    bytes[6] = snBytes[3];
                }
            }
            return bytes;
        }

        /// <summary>
        /// 反序列化缓冲区包体字节数组并返回反序列化对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetData<T>() where T : class, new()
        {
            return Data.ConvertToObject<T>();
        }

        /// <summary>
        /// 把一个对象序列化成一个byte数组存到包体缓冲区中
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        public void SetData<T>(T data) where T:class, new()
        {
            Data = data.ConvertToBytes();
        }
    }

    /// <summary>
    /// 包状态
    /// </summary>
    public enum ParseState
    {
        /// <summary>
        /// 包头
        /// </summary>
        Head,

        /// <summary>
        /// RPC消息
        /// </summary>
        Rpc,

        /// <summary>
        /// Actor消息
        /// </summary>
        Actor,

        /// <summary>
        /// 消息包体
        /// </summary>
        Body,
    }

    /// <summary>
    /// 包解析类
    /// </summary>
    public class PacketParser
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public PacketParser()
        {
            Buffer = new BufferQueue();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="blockSize">指定缓冲区块大小</param>
        public PacketParser(int blockSize)
        {
            Buffer = new BufferQueue(blockSize);
        }

        /// <summary>
        /// 缓冲区对象
        /// </summary>
        internal readonly BufferQueue Buffer;

        private byte[] bodyBytes = new byte[0];
        private byte[] headBytes = new byte[HeadMaxSize];
        private int rpcId;
        private uint actorMessageId;
        private bool isRpc;
        private bool isCompress;
        private bool isHeartbeat;
        private bool isEncrypt;
        private byte kcpProtocal;
        private bool isActorMessage;

        private int readLength = 0;
        private int packetSize = 0;
        private int headSize = 0;
        private ParseState state;
        private bool isOk;
        private bool finish;

        /// <summary>
        /// 包头协议中表示数据包大小的第一个协议字节数，2个字节
        /// </summary>
        public static readonly int PacketFlagSize = sizeof(short);
        /// <summary>
        /// 包头协议中标志位的字节数，1个字节
        /// </summary>
        public static readonly int BitFlagSize = sizeof(byte);
        /// <summary>
        /// 包头协议中RPC请求Id的字节数，4个字节
        /// </summary>
        public static readonly int RpcFlagSize = sizeof(int);
        /// <summary>
        /// 包头协议中Actor消息Id字节数，4个字节
        /// </summary>
        public static readonly int ActorIdFlagSize = sizeof(int);
        /// <summary>
        /// 最小包头字节数
        /// </summary>
        public static readonly int HeadMinSize = PacketFlagSize + BitFlagSize;
        /// <summary>
        /// 最大包头字节数
        /// </summary>
        public static readonly int HeadMaxSize = PacketFlagSize + BitFlagSize + RpcFlagSize + ActorIdFlagSize;
        /// <summary>
        /// 表示允许发送的最大单个数据包字节数
        /// </summary>
        public static readonly int BodyMaxSize = short.MaxValue - HeadMaxSize;

        /// <summary>
        /// 解析数据包核心函数
        /// </summary>
        private void Parse()
        {
            var tryCount = 0;
            isOk = false;
            while (true)
            {
                if(tryCount > 4)
                {
                    throw new Exception("解包错误，数据包非法.");
                }
                tryCount++;
                switch (state)
                {
                    case ParseState.Head:
                        if (readLength == 0 && Buffer.DataSize < PacketFlagSize)
                        {
                            finish = true;
                            return;
                        }
                        if (Buffer.DataSize >= PacketFlagSize && readLength == 0)//读取包长度
                        {
                            if (Buffer.FirstDataSize >= PacketFlagSize)
                            {

                                System.Buffer.BlockCopy(Buffer.First, Buffer.FirstReadOffset, headBytes, 0, PacketFlagSize);
                                Buffer.UpdateRead(PacketFlagSize);
                            }
                            else
                            {
                                var count = Buffer.FirstDataSize;
                                System.Buffer.BlockCopy(Buffer.First, Buffer.FirstReadOffset, headBytes, 0, count);
                                Buffer.UpdateRead(count);
                                System.Buffer.BlockCopy(Buffer.First, Buffer.FirstReadOffset, headBytes, count, PacketFlagSize - count);
                                Buffer.UpdateRead(PacketFlagSize - count);
                            }
                            readLength += PacketFlagSize;
                            packetSize = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(headBytes, 0));

                        }
                        if (Buffer.DataSize >= BitFlagSize && readLength == PacketFlagSize)//读取标志位
                        {
                            System.Buffer.BlockCopy(Buffer.First, Buffer.FirstReadOffset, headBytes, PacketFlagSize, BitFlagSize);
                            Buffer.UpdateRead(BitFlagSize);
                            readLength += BitFlagSize;
                            SetBitFlag(headBytes[PacketFlagSize]);
                            bodyBytes = new byte[packetSize - headSize];
                            if (isRpc)
                            {
                                state = ParseState.Rpc;
                            }
                            else
                            {
                                if (isActorMessage)
                                {
                                    state = ParseState.Actor;
                                }
                                else
                                {
                                    state = ParseState.Body;
                                }
                            }
                        }
                        break;
                    case ParseState.Rpc:
                        if (Buffer.DataSize >= RpcFlagSize && readLength == HeadMinSize)//读取Rpc标志位
                        {
                            if (Buffer.FirstDataSize >= RpcFlagSize)
                            {
                                System.Buffer.BlockCopy(Buffer.First, Buffer.FirstReadOffset, headBytes, HeadMinSize, RpcFlagSize);
                                Buffer.UpdateRead(RpcFlagSize);
                            }
                            else
                            {
                                var count = Buffer.FirstDataSize;
                                System.Buffer.BlockCopy(Buffer.First, Buffer.FirstReadOffset, headBytes, HeadMinSize, count);
                                Buffer.UpdateRead(count);
                                System.Buffer.BlockCopy(Buffer.First, Buffer.FirstReadOffset, headBytes, HeadMinSize + count, RpcFlagSize - count);
                                Buffer.UpdateRead(RpcFlagSize - count);
                            }
                            readLength += RpcFlagSize;
                            rpcId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(headBytes, HeadMinSize));
                            if (isActorMessage)
                            {
                                state = ParseState.Actor;
                            }
                            else
                            {
                                state = ParseState.Body;
                            }
                        }
                        break;
                    case ParseState.Actor:
                        var needSize = isRpc ? HeadMinSize + RpcFlagSize : HeadMinSize;
                        if (Buffer.DataSize >= ActorIdFlagSize && readLength == needSize)
                        {
                            if (Buffer.FirstDataSize >= ActorIdFlagSize)
                            {
                                System.Buffer.BlockCopy(Buffer.First, Buffer.FirstReadOffset, headBytes, needSize, ActorIdFlagSize);
                                Buffer.UpdateRead(ActorIdFlagSize);
                            }
                            else
                            {
                                var count = Buffer.FirstDataSize;
                                System.Buffer.BlockCopy(Buffer.First, Buffer.FirstReadOffset, headBytes, needSize, count);
                                Buffer.UpdateRead(count);
                                System.Buffer.BlockCopy(Buffer.First, Buffer.FirstReadOffset, headBytes, needSize + count, ActorIdFlagSize - count);
                                Buffer.UpdateRead(ActorIdFlagSize - count);
                            }
                            readLength += ActorIdFlagSize;
                            actorMessageId = (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(headBytes, needSize));
                            state = ParseState.Body;
                        }
                        break;
                    case ParseState.Body:
                        needSize = packetSize - readLength;
                        if (Buffer.DataSize >= needSize)
                        {
                            if (Buffer.FirstDataSize >= needSize)
                            {
                                System.Buffer.BlockCopy(Buffer.First, Buffer.FirstReadOffset, bodyBytes, readLength - headSize, needSize);
                                Buffer.UpdateRead(needSize);
                                readLength += needSize;
                            }
                            else
                            {
                                var count = Buffer.FirstDataSize;
                                System.Buffer.BlockCopy(Buffer.First, Buffer.FirstReadOffset, bodyBytes, readLength - headSize, count);
                                Buffer.UpdateRead(count);
                                readLength += count;
                                needSize -= count;
                                count = needSize > Buffer.FirstDataSize ? Buffer.FirstDataSize : needSize;
                                System.Buffer.BlockCopy(Buffer.First, Buffer.FirstReadOffset, bodyBytes, readLength - headSize, count);
                                Buffer.UpdateRead(count);
                                readLength += count;
                            }
                        }
                        break;
                }

                if (Buffer.DataSize == 0)
                {
                    finish = true;
                }

                if (Buffer.DataSize < packetSize - readLength)
                {
                    finish = true;
                }

                if (readLength == packetSize)
                {
                    isOk = true;
                }

                if (isOk)
                {
                    state = ParseState.Head;
                    break;
                }

                if (finish)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 设置解析标志位结果
        /// </summary>
        /// <param name="flagByte"></param>
        private void SetBitFlag(byte flagByte)
        {
            isRpc = Convert.ToBoolean(flagByte & 1);
            isHeartbeat = Convert.ToBoolean(flagByte >> 1 & 1);
            isCompress = Convert.ToBoolean(flagByte >> 2 & 1);
            isEncrypt = Convert.ToBoolean(flagByte >> 3 & 1);
            kcpProtocal = (byte)(flagByte >> 4 & 3);
            isActorMessage = Convert.ToBoolean(flagByte >> 6 & 1);
            headSize = isRpc ? HeadMinSize + RpcFlagSize : HeadMinSize;
            headSize = isActorMessage ? headSize + ActorIdFlagSize : headSize;
        }

        /// <summary>
        /// 重置当前解析器所有状态
        /// </summary>
        public void Clear()
        {
            state = ParseState.Head;
            isOk = false;
            finish = false;
            Flush();
            Buffer.Flush();
        }

        /// <summary>
        /// 重置解析器
        /// </summary>
        private void Flush()
        {
            rpcId = 0;
            actorMessageId = 0;
            isRpc = false;
            isEncrypt = false;
            isCompress = false;
            isHeartbeat = false;
            kcpProtocal = 0;
            isActorMessage = false;
            readLength = 0;
            packetSize = 0;
            headSize = 0;
            bodyBytes = null;
        }

        private Packet FailedPacket = new Packet();
        /// <summary>
        /// 从缓冲区中读数据包
        /// </summary>
        /// <returns></returns>
        public Packet ReadBuffer()
        {
            finish = false;
            while (!finish)
            {
                Parse();
                if (isOk)
                {
                    var packet = new Packet
                    {
                        IsSuccess = true,
                        RpcId = rpcId,
                        IsRpc = isRpc,
                        IsCompress = isCompress,
                        IsHeartbeat = isHeartbeat,
                        KcpProtocal = kcpProtocal,
                        ActorMessageId = actorMessageId,
                        Data = bodyBytes,
                    };
                    Flush();
                    return packet;
                }
            }
            return FailedPacket;
        }

        /// <summary>
        /// 写一个字节数组到缓冲区中
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        public void WriteBuffer(byte[] bytes, int offset, int length)
        {
            Buffer.Write(bytes, offset, length);
        }

        /// <summary>
        /// 写一个包到缓冲区中
        /// </summary>
        /// <param name="packet"></param>
        public void WriteBuffer(Packet packet)
        {
            Buffer.Write(packet.GetHeadBytes());
            if (packet.Data != null)
            {
                Buffer.Write(packet.Data);
            }
        }

        private List<byte[]> packetByte = new List<byte[]> { new byte[0], new byte[0] };
        /// <summary>
        /// 获取一个包的字节数组
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        public List<byte[]> GetPacketBytes(Packet packet)
        {
            packetByte[0] = packet.GetHeadBytes();
            packetByte[1] = packet.Data;
            return packetByte;
        }
    }
}
