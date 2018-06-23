using System;
using System.Net;

namespace NetChannel
{
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
    }

    public enum ParseState
    {
        Head,
        Rpc,
        Body,
    }

    /// <summary>
    /// 该类主要
    /// </summary>
    public class PacketParse
    {
        internal readonly Buffer Buffer = new Buffer();
        private byte[] bodyBytes = new byte[0];
        private byte[] headBytes = new byte[headMaxSize];
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

        private static readonly int packetFlagSize = sizeof(short);
        private static readonly int bitFlagSize = sizeof(byte);
        private static readonly int rpcFlagSize = sizeof(UInt32);
        private static readonly int headMinSize = packetFlagSize + bitFlagSize;
        private static readonly int headMaxSize = packetFlagSize + bitFlagSize + rpcFlagSize;
        private static readonly int bodyMaxSize = short.MaxValue - headMaxSize;

        private void Parse()
        {
            isOk = false;
            while (true)
            {
                switch (state)
                {
                    case ParseState.Head:
                        if (readLength == 0 && Buffer.DataSize < packetFlagSize)
                        {
                            finish = true;
                            return;
                        }
                        if (Buffer.DataSize >= packetFlagSize && readLength == 0)//读取包长度
                        {
                            if (Buffer.FirstCount >= packetFlagSize)
                            {
                                Array.Copy(Buffer.First, Buffer.FirstOffset, headBytes, 0, packetFlagSize);
                                Buffer.UpdateRead(packetFlagSize);
                            }
                            else
                            {
                                var count = Buffer.FirstCount;
                                Array.Copy(Buffer.First, Buffer.FirstOffset, headBytes, 0, count);
                                Buffer.UpdateRead(count);
                                Array.Copy(Buffer.First, Buffer.FirstOffset, headBytes, count, packetFlagSize - count);
                                Buffer.UpdateRead(packetFlagSize - count);
                            }
                            readLength += packetFlagSize;
                            packetSize = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(headBytes, 0));

                        }
                        if (Buffer.DataSize >= bitFlagSize && readLength == packetFlagSize)//读取标志位
                        {
                            Array.Copy(Buffer.First, Buffer.FirstOffset, headBytes, packetFlagSize, bitFlagSize);
                            Buffer.UpdateRead(bitFlagSize);
                            readLength += bitFlagSize;
                            SetBitFlag(headBytes[packetFlagSize]);
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
                        if (Buffer.DataSize >= rpcFlagSize && readLength == headMinSize)//读取Rpc标志位
                        {
                            if (Buffer.FirstCount >= rpcFlagSize)
                            {
                                Array.Copy(Buffer.First, Buffer.FirstOffset, headBytes, headMinSize, rpcFlagSize);
                                Buffer.UpdateRead(rpcFlagSize);
                            }
                            else
                            {
                                var count = Buffer.FirstCount;
                                Array.Copy(Buffer.First, Buffer.FirstOffset, headBytes, headMinSize, count);
                                Buffer.UpdateRead(count);
                                Array.Copy(Buffer.First, Buffer.FirstOffset, headBytes, headMinSize + count, rpcFlagSize - count);
                                Buffer.UpdateRead(rpcFlagSize - count);
                            }
                            readLength += rpcFlagSize;
                            rpcId = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(headBytes, headMinSize));
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
            headSize = isRpc ? headMaxSize : headMinSize;
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
                if (packet.Data.Length > bodyMaxSize)
                {
                    throw new ArgumentOutOfRangeException();
                }
            }

            int headSize = packet.IsRpc ? headMaxSize : headMinSize;
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
