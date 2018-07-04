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

        /// <summary>
        /// 指向缓冲区队列中第一个缓冲区块读取数据的数组下标位置
        /// </summary>
        public int FirstReadOffset;

        /// <summary>
        /// 指向缓冲区队列中最后一个缓冲区块写入数据的数组下标位置
        /// </summary>
        public int LastWriteOffset;

        /// <summary>
        /// 更新读的的缓冲区字节数
        /// </summary>
        /// <param name="addValue"></param>
        public void UpdateRead(int addValue)
        {
            FirstReadOffset += addValue;
            if (FirstReadOffset > blockSize)
            {
                throw new ArgumentOutOfRangeException("缓冲区索引超出有效范围.");
            }

            if (FirstReadOffset == blockSize)
            {
                FirstReadOffset = 0;
                bufferCache.Enqueue(bufferQueue.Dequeue());
            }
        }

        /// <summary>
        /// 更新写的的缓冲区字节数
        /// </summary>
        /// <param name="addValue"></param>
        public void UpdateWrite(int addValue)
        {
            LastWriteOffset += addValue;
            if (LastWriteOffset > blockSize)
            {
                throw new ArgumentOutOfRangeException("缓冲区索引超出有效范围.");
            }

            if (LastWriteOffset == blockSize)
            {
                LastWriteOffset = 0;
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
        /// 缓冲区队列中第一个缓冲区块可写字符数
        /// </summary>
        public int FirstDataSize
        {
            get
            {
                var result = 0;
                if(bufferQueue.Count == 1)
                {
                    result = LastWriteOffset - FirstReadOffset;
                }
                else
                {
                    result = blockSize - FirstReadOffset;
                }

                if(result < 0)
                {
                    throw new ArgumentOutOfRangeException("缓冲区索引超出有效范围.");
                }

                return result;
            }
        }

        /// <summary>
        /// 缓冲区队列中最后一个缓冲区块可写字符数
        /// </summary>
        public int LastCapacity
        {
            get
            {
                return blockSize - LastWriteOffset;
            }
        }

        /// <summary>
        /// 当前缓冲区中有效数据的字节数
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
                else if(bufferQueue.Count == 1)
                {
                    size = LastWriteOffset - FirstReadOffset;
                }
                else
                {
                    size = bufferQueue.Count * blockSize - FirstReadOffset - LastCapacity;
                }
                if (size < 0)
                {
                    throw new ArgumentOutOfRangeException("缓冲区索引超出有效范围.");
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
                var count = length > LastCapacity ? LastCapacity : length;
                System.Buffer.BlockCopy(bytes, index, Last, LastWriteOffset, count);
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
            FirstReadOffset = 0;
            LastWriteOffset = 0;
            while (bufferQueue.Count > 1)
            {
                bufferQueue.Dequeue();
            }
            for(var i = 0; i < First.Length; i++)
            {
                First[i] = 0;
            }
            bufferCache.Clear();
        }
    }
}
