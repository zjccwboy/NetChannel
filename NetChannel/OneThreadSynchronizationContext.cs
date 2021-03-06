﻿using System;
using System.Collections.Concurrent;
using System.Threading;

namespace NetChannel
{
    public class OneThreadSynchronizationContext : SynchronizationContext
    {
        public static OneThreadSynchronizationContext Instance = new OneThreadSynchronizationContext();

        // 线程同步队列,发送接收socket回调都放到该队列,由poll线程统一执行
        private readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();

        private void Add(Action action)
        {
            this.queue.Enqueue(action);
        }

        public void Update()
        {
            while (true)
            {
                Action action;
                if (!this.queue.TryDequeue(out action))
                {
                    return;
                }
                action();
            }
        }

        public override void Post(SendOrPostCallback callback, object state)
        {
            this.Add(() => { callback(state); });
        }
    }
}