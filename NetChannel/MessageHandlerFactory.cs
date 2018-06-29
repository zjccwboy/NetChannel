using System;
using System.Collections.Generic;

namespace NetChannel
{
    /// <summary>
    /// 消息处理类工厂
    /// </summary>
    public class MessageHandlerFactory
    {
        private static List<Type> types;
        static MessageHandlerFactory()
        {
            Load();
        }

        /// <summary>
        /// 创建消息处理类
        /// </summary>
        /// <param name="channel">通讯管道对象</param>
        /// <param name="netService">网络服务对象</param>
        /// <returns></returns>
        public static IEnumerable<IMessageHandler> CreateHandlers(ANetChannel channel, ANetService netService)
        {
            var handlers = new List<IMessageHandler>();
            foreach(var type in types)
            {
                var handler = (IMessageHandler)Activator.CreateInstance(type);
                handler.Channel = channel;
                handler.NetService = netService;
                channel.OnReceive += handler.DoReceive;
                handlers.Add(handler);
            }
            return handlers;
        }

        /// <summary>
        /// 加载全部消息处理类类型到内存中
        /// </summary>
        private static void Load()
        {
            var assemblys = AppDomain.CurrentDomain.GetAssemblies();
            types = new List<Type>();
            foreach (var assembly in assemblys)
            {
                var type = assembly.GetTypes();
                foreach(var t in type)
                {
                    if(t == typeof(IMessageHandler))
                    {
                        continue;
                    }
                    if (typeof(IMessageHandler).IsAssignableFrom(t))
                    {
                        types.Add(t);
                    }
                }
            }
        }
    }
}
