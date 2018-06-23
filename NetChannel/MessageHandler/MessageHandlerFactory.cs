using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NetChannel
{
    public class MessageHandlerFactory
    {
        private static List<Type> types;
        static MessageHandlerFactory()
        {
            Load();
        }

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
