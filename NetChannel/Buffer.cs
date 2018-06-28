using System;
using System.Collections.Generic;
using System.Linq;

namespace NetChannel
{
    /// <summary>
    /// 接收发送数据缓冲区
    /// </summary>
    public class Buffer
    {
        private int blockSize = 8192;

        /// <summary>
        /// 缓存队列
        /// </summary>
        private readonly Queue<byte[]> bufferQueue = new Queue<byte[]>();

        /// <summary>
        /// 用于复用的缓存队列
        /// </summary>
        private readonly Queue<byte[]> bufferCache = new Queue<byte[]>();

        public Buffer()
        {
            //默认分配一块缓冲区
            bufferQueue.Enqueue(new byte[blockSize]);
        }

        public Buffer(int blockSize)
        {
            this.blockSize = blockSize;
            //默认分配一块缓冲区
            bufferQueue.Enqueue(new byte[blockSize]);
        }

        private int readOffset;
        private int writeOffset;

        public int FirstOffset
        {
            get
            {
                return readOffset % blockSize;
            }
        }

        public void UpdateRead(int addValue)
        {
            readOffset += addValue;
            if (readOffset > writeOffset)
            {
                throw new ArgumentOutOfRangeException("read offset out of buffer.");
            }

            if (readOffset >= blockSize)
            {
                readOffset -= blockSize;
                writeOffset -= blockSize;
                bufferCache.Enqueue(bufferQueue.Dequeue());
            }
        }

        public int LastOffset
        {
            get
            {
                return writeOffset % blockSize;
            }
        }

        public void UpdateWrite(int addValue)
        {
            writeOffset += addValue;
            if (LastOffset == 0)
            {
                if (bufferCache.Count > 0)
                {
                    bufferQueue.Enqueue(bufferCache.Dequeue());
                }
                else
                {
                    bufferQueue.Enqueue(new byte[blockSize]);
                }
            }
        }

        public int FirstCount
        {
            get
            {
                if(writeOffset > blockSize)
                {
                    return blockSize - FirstOffset;
                }
                else
                {
                    return writeOffset - FirstOffset;
                }
            }
        }

        public int LastCount
        {
            get
            {
                return blockSize - LastOffset;
            }
        }

        public int DataSize
        {
            get
            {
                int size = 0;
                if (bufferQueue.Count == 0)
                {
                    return size;
                }
                else
                {
                    size = writeOffset - readOffset;
                }
                if (size < 0)
                {
                    throw new ArgumentOutOfRangeException("data index out of buffer.");
                }
                return size;
            }
        }

        public void Write(byte[] bytes)
        {
            Write(bytes, 0, bytes.Length);
        }

        public void Write(byte[] bytes, int index, int length)
        {
            while(length > 0)
            {
                var count = length > LastCount ? LastCount : length;
                Array.Copy(bytes, index, Last, LastOffset, count);
                index += count;
                length -= count;
                UpdateWrite(count);
            }
        }

        public byte[] Last
        {
            get
            {
                return bufferQueue.Last();
            }
        }

        public byte[] First
        {
            get
            {
                return bufferQueue.Peek();
            }
        }

        public void Flush()
        {
            readOffset = 0;
            writeOffset = 0;
            while(bufferQueue.Count > 1)
            {
                bufferQueue.Dequeue();
            }
            bufferCache.Clear();
        }
    }
}
