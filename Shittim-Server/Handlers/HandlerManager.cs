using BlueArchiveAPI.NetworkModels;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace BlueArchiveAPI.Handlers
{
    public sealed class HandlerManager
    {
        private readonly IServiceProvider _root;
        private readonly Dictionary<int, ObjectFactory> _factories = new();

        public readonly record struct HandlerLease(IHandler Handler, IServiceScope Scope) : IDisposable
        {
            public void Dispose() => Scope.Dispose();
            public bool IsValid => Handler is not null;
        }

        public HandlerManager(IServiceProvider root)
        {
            _root = root;
        }

        public void Initialize()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var types = assemblies
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
                })
                .Where(t => t != null && !t.IsAbstract && t.IsClass);

            foreach (var type in types!)
            {
                // Pattern 1: Types with [ProtocolHandler] attribute
                var attr = type.GetCustomAttribute<ProtocolHandlerAttribute>(false);
                if (attr != null)
                {
                    RegisterHandler(type, (int)attr.Protocol);
                    continue;
                }

                // Pattern 2: Types inheriting BaseHandler<TReq, TRes> (legacy pattern)
                if (type.BaseType?.IsGenericType == true && 
                    type.BaseType.GetGenericTypeDefinition() == typeof(BaseHandler<,>))
                {
                    try
                    {
                        // Create temporary instance to get protocol from IHandler interface
                        using var tempScope = _root.CreateScope();
                        var tempInstance = ActivatorUtilities.CreateInstance(tempScope.ServiceProvider, type);
                        if (tempInstance is IHandler handler)
                        {
                            RegisterHandler(type, (int)handler.RequestProtocol);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[HandlerManager] WARNING: Could not register handler {type.Name}: {ex.Message}");
                    }
                }
            }
            
            Console.WriteLine($"[HandlerManager] Total handlers registered: {_factories.Count}");
        }

        private void RegisterHandler(Type type, int protocol)
        {
            if (_factories.ContainsKey(protocol))
            {
                Console.WriteLine($"[HandlerManager] WARNING: Duplicate handler for protocol {protocol}: {type.Name}");
                return;
            }

            var factory = ActivatorUtilities.CreateFactory(type, Type.EmptyTypes);
            _factories[protocol] = factory;
            
            var protocolEnum = (Protocol)protocol;
            Console.WriteLine($"[HandlerManager] Registered handler: {type.Name} for protocol {protocolEnum} ({protocol})");
        }

        public IHandler GetHandler(Protocol protocol)
        {
            return GetHandler((int)protocol);
        }

        public IHandler GetHandler(int protocol)
        {
            if (!_factories.TryGetValue(protocol, out var factory))
            {
                Console.WriteLine($"[HandlerManager] No handler registered for protocol {protocol}");
                return null;
            }

            using var scope = _root.CreateScope();
            return (IHandler)factory(scope.ServiceProvider, Array.Empty<object>());
        }

        public HandlerLease GetHandlerLease(Protocol protocol)
        {
            return GetHandlerLease((int)protocol);
        }

        public HandlerLease GetHandlerLease(int protocol)
        {
            if (!_factories.TryGetValue(protocol, out var factory))
            {
                Console.WriteLine($"[HandlerManager] No handler registered for protocol {protocol}");
                return default;
            }

            var scope = _root.CreateScope();
            var handler = (IHandler)factory(scope.ServiceProvider, Array.Empty<object>());
            return new HandlerLease(handler, scope);
        }
    }
}
