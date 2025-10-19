using BlueArchiveAPI.NetworkModels;
using System.Reflection;

namespace BlueArchiveAPI.Handlers
{
    public static class HandlerManager
    {
        private static readonly Dictionary<Protocol, IHandler> _handlers = new Dictionary<Protocol, IHandler>();
        
        public static void Initialize()
        {
            foreach (var type in Assembly.GetExecutingAssembly().ExportedTypes)
            {
                if (type.BaseType is not { IsGenericType: true } || 
                    type.BaseType.GetGenericTypeDefinition() != typeof(BaseHandler<,>))
                {
                    continue;
                }
                
                if (type.IsAbstract)
                {
                    continue;
                }
                
                var handler = (IHandler)Activator.CreateInstance(type);

                Console.WriteLine($"[HandlerManager] registering handler: {type.Name} for protocol {handler.RequestProtocol} => {handler.ResponseProtocol}");
                _handlers.Add(handler.RequestProtocol, handler);
            }
        }

        public static IHandler GetHandler(Protocol protocol)
        {
            return _handlers.TryGetValue(protocol, out var val) ? val : null;
        }
    }
}
