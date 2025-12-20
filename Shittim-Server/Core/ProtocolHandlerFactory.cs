using Microsoft.EntityFrameworkCore;
using Schale.Data;
using Schale.MX.NetworkProtocol;
using Serilog;
using System.Reflection;

namespace Shittim_Server.Core;

public interface IProtocolHandlerFactory
{
    Task<object?> InvokeAsync(Protocol protocol, RequestPacket req);
    MethodInfo? GetProtocolHandler(Protocol protocol);
    Type? GetRequestPacketTypeByProtocol(Protocol protocol);
    Type? GetResponsePacketTypeByProtocol(Protocol protocol);
    void RegisterInstance(Type t, object? inst);
}

public class ProtocolHandlerFactory : IProtocolHandlerFactory
{
    private readonly IDbContextFactory<SchaleDataContext> contextFactory;
    private readonly Dictionary<Protocol, MethodInfo> handlers = [];
    private readonly Dictionary<Protocol, Type> requestPacketTypes = [];
    private readonly Dictionary<Protocol, Type> responsePacketTypes = [];
    private readonly Dictionary<Type, object?> handlerInstances = [];

    public ProtocolHandlerFactory(IDbContextFactory<SchaleDataContext> contextFactory)
    {
        this.contextFactory = contextFactory;

        foreach (var requestPacketType in Assembly.GetAssembly(typeof(RequestPacket))!.GetTypes().Where(x => x.IsAssignableTo(typeof(RequestPacket))))
        {
            if (requestPacketType == typeof(RequestPacket))
                continue;

            var obj = Activator.CreateInstance(requestPacketType);
            var protocol = requestPacketType.GetProperty("Protocol")!.GetValue(obj);

            if (!requestPacketTypes.ContainsKey((Protocol)protocol!))
                requestPacketTypes.Add((Protocol)protocol!, requestPacketType);
        }
        foreach (var responsePacketType in Assembly.GetAssembly(typeof(ResponsePacket))!.GetTypes().Where(x => x.IsAssignableTo(typeof(ResponsePacket))))
        {
            if (responsePacketType == typeof(ResponsePacket))
                continue;

            var obj = Activator.CreateInstance(responsePacketType);
            var protocol = responsePacketType.GetProperty("Protocol")!.GetValue(obj);

            if (!responsePacketTypes.ContainsKey((Protocol)protocol!))
                responsePacketTypes.Add((Protocol)protocol!, responsePacketType);
        }
    }

    public void RegisterInstance(Type t, object? inst)
    {
        handlerInstances.Add(t, inst);

        foreach (var method in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(x => x.GetCustomAttribute<ProtocolHandlerAttribute>() is not null))
        {
            var attr = method.GetCustomAttribute<ProtocolHandlerAttribute>()!;
            if (handlers.ContainsKey(attr.Protocol))
                continue;

            handlers.Add(attr.Protocol, method);
            Log.Debug("Loaded {methodName} for {attributeProtocol}", method.Name, attr.Protocol);
        }
    }

    public async Task<object?> InvokeAsync(Protocol protocol, RequestPacket requestData)
    {
        var handlerMethod = GetProtocolHandler(protocol);
        if (handlerMethod is null)
            return null;
        
        using var context = await contextFactory.CreateDbContextAsync();
        var account = await context.Accounts.FindAsync(requestData.AccountId);

        ResponsePacket responseData = (ResponsePacket)Activator.CreateInstance(GetResponsePacketTypeByProtocol(protocol)!)!;

        if (account != null && account.GameSettings != null)
            responseData.ServerTimeTicks = account.GameSettings.ServerDateTimeTicks();

        handlerInstances.TryGetValue(handlerMethod.DeclaringType!, out var inst);
        
        object? rawResult = handlerMethod.Invoke(inst, [context, requestData, responseData]);
        
        ResponsePacket? finalResponse;

        if (rawResult is Task task)
        {
            await task.ConfigureAwait(false);
            var resultProperty = task.GetType().GetProperty("Result");
            finalResponse = (resultProperty != null && resultProperty.GetValue(task) is ResponsePacket returnedResponse)
                ? returnedResponse
                : responseData;
        }
        else if (rawResult is ResponsePacket responseResult)
            finalResponse = responseResult;
        else
            finalResponse = responseData;

        return finalResponse;
    }

    public MethodInfo? GetProtocolHandler(Protocol protocol)
    {
        handlers.TryGetValue(protocol, out var handler);
        return handler;
    }

    public Type? GetRequestPacketTypeByProtocol(Protocol protocol)
    {
        requestPacketTypes.TryGetValue(protocol, out var type);
        return type;
    }

    public Type? GetResponsePacketTypeByProtocol(Protocol protocol)
    {
        responsePacketTypes.TryGetValue(protocol, out var type);
        return type;
    }
}
