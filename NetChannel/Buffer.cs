using System;
using System.Collections.Generic;
using System.Linq;

namespace NetChannel
{
    /// <summary>
    /// 接收发送数据缓冲区
    /// </summary>
    public class BufferQueue
    {
        /// <summary>
        /// 缓冲区块大小
        /// </summary>
        private int blockSize = 8192;

        /// <summary>
        /// 缓冲区队列队列
        /// </summary>
        private readonly Queue<byte[]> bufferQueue = new Queue<byte[]>();

        /// <summary>
        /// 用于复用的缓冲区队列
        /// </summary>
        private readonly Queue<byte[]> bufferCache = new Queue<byte[]>();

        /// <summary>
        /// 构造函数，默认分配缓冲区块大小8192字节
        /// </summary>
        public BufferQueue()
        {
            //默认分配一块缓冲区
            bufferQueue.Enqueue(new byte[blockSize]);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="blockSize">指定缓冲区块大小</param>
        public BufferQueue(int blockSize)
        {
            this.blockSize = blockSize;
            //默认分配一块缓冲区
            bufferQueue.Enqueue(new byte[blockSize]);
        }

        private int readOffset;
        private int writeOffset;

        /// <summary>
        /// 指向缓冲区队列中第一个缓冲区块有效的数组下标位置
        /// </summary>
        public int FirstOffset
        {
            get
            {
                return readOffset % blockSize;
            }
        }

        /// <summary>
        /// 更新读的的缓冲区字节数
        /// </summary>
        /// <param name="addValue"></param>
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

        /// <summary>
        /// 指向缓冲区队列中最后一个缓冲区块有效的数组下标位置
        /// </summary>
        public int LastOffset
        {
            get
            {
                return writeOffset % blockSize;
            }
        }

        /// <summary>
        /// 更新写的的缓冲区字节数
        /// </summary>
        /// <param name="addValue"></param>
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

        /// <summary>
        /// 缓冲区队列中第一个缓冲区块有效字节数
        /// </summary>
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

        /// <summary>
        /// 缓冲区队列中最后一个缓冲区块有效字节数
        /// </summary>
        public int LastCount
        {
            get
            {
                return blockSize - LastOffset;
            }
        }

        /// <summary>
        /// 当前缓冲区中有效的字节数
        /// </summary>
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

        /// <summary>
        /// 写入一个字节数组到缓冲区中，该方法不需要调用UpdateWrite方法更新缓冲区字节数
        /// </summary>
        /// <param name="bytes"></param>
        public void Write(byte[] bytes)
        {
            Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// 写入一个字节数组指定的字节数到缓冲区中，该方法不需要调用UpdateWrite方法更新缓冲区字节数
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="index"></param>
        /// <param name="length"></param>
        public void Write(byte[] bytes, int index, int length)
        {
            while(length > 0)
            {
                var count = length > LastCount ? LastCount : length;
                System.Buffer.BlockCopy(bytes, index, Last, LastOffset, count);
                index += count;
                length -= count;
                UpdateWrite(count);
            }
        }

        /// <summary>
        /// 指向缓冲区队列最后一个缓冲区块指针
        /// </summary>
        public byte[] Last
        {
            get
            {
                return bufferQueue.Last();
            }
        }

        /// <summary>
        /// 指向缓冲区队列最后第一个缓冲区块指针
        /// </summary>
        public byte[] First
        {
            get
            {
                return bufferQueue.Peek();
            }
        }

        /// <summary>
        /// 清空缓冲区
        /// </summary>
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
