using System;
using System.Net;
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
        internal bool IsSuccess;

        /// <summary>
        /// Rpc请求标识
        /// </summary>
        public int RpcId;

        /// <summary>
        /// Rpc请求标志
        /// </summary>
        internal bool IsRpc;

        /// <summary>
        /// 心跳标志
        /// </summary>
        internal bool IsHeartbeat;

        /// <summary>
        /// 压缩标志
        /// </summary>
        public bool IsCompress;

        /// <summary>
        /// 加密标志
        /// </summary>
        public bool IsEncrypt;

        /// <summary>
        /// 数据包
        /// </summary>
        public byte[] Data;

        public T GetData<T>() where T : class, new()
        {
            return Data.ConvertToObject<T>();
        }

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
        /// 包体
        /// </summary>
        Body,
    }

    /// <summary>
    /// 包解析类
    /// </summary>
    public class PacketParser
    {
        internal readonly Buffer Buffer = new Buffer();
        private byte[] bodyBytes = new byte[0];
        private byte[] headBytes = new byte[HeadMaxSize];
        private int rpcId;
        private bool isRpc;
        private bool isCompress;
        private bool isHeartbeat;
        private bool isEncrypt;

        private int readLength = 0;
        private int packetSize = 0;
        private int headSize = 0;
        private ParseState state;
        private bool isOk;
        private bool finish;

        public static readonly int PacketFlagSize = sizeof(short);
        public static readonly int BitFlagSize = sizeof(byte);
        public static readonly int RpcFlagSize = sizeof(int);
        public static readonly int HeadMinSize = PacketFlagSize + BitFlagSize;
        public static readonly int HeadMaxSize = PacketFlagSize + BitFlagSize + RpcFlagSize;
        public static readonly int BodyMaxSize = short.MaxValue - HeadMaxSize;

        private void Parse()
        {
            isOk = false;
            while (true)
            {
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
                            if (Buffer.FirstCount >= PacketFlagSize)
                            {
                                Array.Copy(Buffer.First, Buffer.FirstOffset, headBytes, 0, PacketFlagSize);
                                Buffer.UpdateRead(PacketFlagSize);
                            }
                            else
                            {
                                var count = Buffer.FirstCount;
                                Array.Copy(Buffer.First, Buffer.FirstOffset, headBytes, 0, count);
                                Buffer.UpdateRead(count);
                                Array.Copy(Buffer.First, Buffer.FirstOffset, headBytes, count, PacketFlagSize - count);
                                Buffer.UpdateRead(PacketFlagSize - count);
                            }
                            readLength += PacketFlagSize;
                            packetSize = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(headBytes, 0));

                        }
                        if (Buffer.DataSize >= BitFlagSize && readLength == PacketFlagSize)//读取标志位
                        {
                            Array.Copy(Buffer.First, Buffer.FirstOffset, headBytes, PacketFlagSize, BitFlagSize);
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
                                state = ParseState.Body;
                            }
                        }
                        break;
                    case ParseState.Rpc:
                        if (Buffer.DataSize >= RpcFlagSize && readLength == HeadMinSize)//读取Rpc标志位
                        {
                            if (Buffer.FirstCount >= RpcFlagSize)
                            {
                                Array.Copy(Buffer.First, Buffer.FirstOffset, headBytes, HeadMinSize, RpcFlagSize);
                                Buffer.UpdateRead(RpcFlagSize);
                            }
                            else
                            {
                                var count = Buffer.FirstCount;
                                Array.Copy(Buffer.First, Buffer.FirstOffset, headBytes, HeadMinSize, count);
                                Buffer.UpdateRead(count);
                                Array.Copy(Buffer.First, Buffer.FirstOffset, headBytes, HeadMinSize + count, RpcFlagSize - count);
                                Buffer.UpdateRead(RpcFlagSize - count);
                            }
                            readLength += RpcFlagSize;
                            rpcId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(headBytes, HeadMinSize));
                            state = ParseState.Body;
                        }
                        break;
                    case ParseState.Body:
                        var needSize = packetSize - readLength;
                        if (Buffer.DataSize >= needSize)
                        {
                            if (Buffer.FirstCount >= needSize)
                            {
                                Array.Copy(Buffer.First, Buffer.FirstOffset, bodyBytes, readLength - headSize, needSize);
                                Buffer.UpdateRead(needSize);
                                readLength += needSize;
                            }
                            else
                            {
                                var count = Buffer.FirstCount;
                                Array.Copy(Buffer.First, Buffer.FirstOffset, bodyBytes, readLength - headSize, count);
                                Buffer.UpdateRead(count);
                                readLength += count;
                                needSize -= count;
                                count = needSize > Buffer.FirstCount ? Buffer.FirstCount : needSize;
                                Array.Copy(Buffer.First, Buffer.FirstOffset, bodyBytes, readLength - headSize, count);
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

        private void SetBitFlag(byte flagByte)
        {
            isRpc = Convert.ToBoolean(flagByte & 1);
            isHeartbeat = Convert.ToBoolean(flagByte >> 1 & 1);
            isCompress = Convert.ToBoolean(flagByte >> 2 & 1);
            isEncrypt = Convert.ToBoolean(flagByte >> 3 & 1);
            headSize = isRpc ? HeadMaxSize : HeadMinSize;
        }

        private void Flush()
        {
            rpcId = 0;
            isRpc = false;
            isEncrypt = false;
            isCompress = false;
            isHeartbeat = false;
            readLength = 0;
            packetSize = 0;
            headSize = 0;
            bodyBytes = null;
        }

        public void Clear()
        {
            Flush();
            Buffer.Flush();
        }

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
                        Data = bodyBytes,
                    };
                    Flush();
                    return packet;
                }
            }
            return new Packet();
        }

        public void WriteBuffer(Packet packet)
        {
            var bodySize = 0;
            if (packet.Data != null)
            {
                bodySize = packet.Data.Length;
                if (packet.Data.Length > BodyMaxSize)
                {
                    throw new ArgumentOutOfRangeException();
                }
            }

            int headSize = packet.IsRpc ? HeadMaxSize : HeadMinSize;
            int packetSize = headSize + bodySize;
            var sizeBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Convert.ToInt16(packetSize)));
            Buffer.Write(sizeBytes);

            var flagBytes = new byte[1];
            if (packet.IsRpc)
            {
                flagBytes[0] |= 1;
            }
            if (packet.IsHeartbeat)
            {
                flagBytes[0] |= 1 << 1;
            }
            if (packet.IsCompress)
            {
                flagBytes[0] |= 1 << 2;
            }
            if (packet.IsEncrypt)
            {
                flagBytes[0] |= 1 << 3;
            }
            Buffer.Write(flagBytes);
            if (packet.IsRpc)
            {
                var rpcBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(Convert.ToInt32(packet.RpcId)));
                Buffer.Write(rpcBytes);
            }
            if (packet.Data != null)
            {
                Buffer.Write(packet.Data);
            }
        }
    }
}
