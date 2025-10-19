using MX.Core.Services;
using MX.NetworkProtocol;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Reflection;

namespace ProtocolGen;

internal class Program
{
    private static readonly HashSet<Type> writtenType = new HashSet<Type>();
    private static readonly Assembly sourceAsm = typeof(BasePacket).Assembly;

    private static void WriteHeader(StreamWriter sw)
    {
        sw.WriteLine("namespace BlueArchiveAPI.NetworkModels;");
        sw.WriteLine("using System.Collections.ObjectModel;");
        sw.WriteLine("");
    }

    public class Ref<T>
    {
        public T t;
    }

    private static Type ShouldAdd(Type type)
    {
        if (type == null) return null;
        if (type == typeof(TypedJsonWrapper<>)) return null;
        if (type.IsArray) return ShouldAdd(type.GetElementType());
        return type.Assembly == sourceAsm && !writtenType.Contains(type) ? type : null;
    }
    private static IEnumerable<Type> TypeGetName(Type type, Ref<string> name)
    {
        if (type.IsGenericType)
        {
            var bt = type.GetGenericTypeDefinition();
            yield return ShouldAdd(bt);

            var @base = type.GetGenericTypeDefinition().Name.Split('`')[0];
            if (@base == "IEnumerable") @base = "List";

            var names = new List<string>();

            foreach (var ctype in type.GenericTypeArguments)
            {
                var @ref = new Ref<string>();
                foreach (var t2 in TypeGetName(ctype, @ref))
                {
                    yield return ShouldAdd(t2);
                }
                names.Add(@ref.t);
            }
            var tt = string.Join(", ", names);
            name.t = $"{@base}<{tt}>";
            if (@base == "Nullable") name.t = type.GenericTypeArguments[0].Name + "?";
        }
        else
        {
            yield return ShouldAdd(type);

            name.t = type.Name;

            name.t = name.t switch
            {
                "Int32" => "int",
                "Int64" => "long",
                "Single" => "float",
                "Double" => "double",
                "String" => "string",
                "UInt32" => "uint",
                "Char" => "char",
                "Byte" => "byte",
                "UInt16" => "ushort",
                "Int16" => "short",
                "Boolean" => "bool",
                _ => name.t
            };

        }
    }
    
    private static IEnumerable<Type> WriteClass(StreamWriter sw, Type type)
    {
        if (writtenType.Contains(type)) yield break;
        writtenType.Add(type);
        
        if (type.IsEnum)
        {
            sw.WriteLine($"public enum {type.Name}");
            sw.WriteLine("{");

            foreach (var name in Enum.GetNames(type))
                sw.WriteLine($"    {name} = {Convert.ToInt32(Enum.Parse(type, name))},");

            sw.WriteLine("}");
            sw.WriteLine("");

            yield break; 
        }

        string @base = null;

        if (type.BaseType != null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
        {
            var @ref = new Ref<string>();
            foreach (var t in TypeGetName(type.BaseType, @ref))
                yield return t;
            @base = $" : {@ref.t}".Replace("KeyedCollection", "Dictionary");
        }

        sw.WriteLine($"public {(type.IsValueType ? "struct" : "class")} {type.Name}{@base}");
        sw.WriteLine("{");

        foreach (var prop in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (prop.IsPrivate)
            {
                if (prop.GetCustomAttribute<JsonPropertyAttribute>() == null)
                    continue;
            }

            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                continue;

            var ptype = prop.FieldType;

            if (prop.DeclaringType != type) continue;

            var name = new Ref<string>();

            foreach (var t in TypeGetName(ptype, name))
                yield return t;

            sw.WriteLine($"    public {name.t} {prop.Name};");
        }

        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (prop.GetMethod.IsPrivate || prop.GetMethod.IsAbstract)
            {
                if (prop.GetCustomAttribute<JsonPropertyAttribute>() == null)
                    continue;
            }

            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                continue;

            var ptype = prop.PropertyType;

            if (prop.DeclaringType != type) continue;

            var name = new Ref<string>();

            foreach (var t in TypeGetName(ptype, name))
                yield return t;

            sw.WriteLine($"    public {name.t} {prop.Name};");
        }


        sw.WriteLine("}");
        sw.WriteLine("");
        
        yield break;
    }
    
    private static IEnumerable<Type> WriteClass(StreamWriter sw, string baseClass, Protocol proto, Type type)
    {
        if (writtenType.Contains(type)) yield break;
        writtenType.Add(type);

        var @base = $" : ";
        if (type.BaseType != null && type.BaseType != typeof(object) && type.BaseType != typeof(ValueType))
        {
            var @ref = new Ref<string>();
            foreach (var t in TypeGetName(type.BaseType, @ref))
                yield return t;
            @base = $" : {@ref.t}, ".Replace("KeyedCollection", "Dictionary");
        }

        sw.WriteLine($"public class {type.Name}{@base}{baseClass}");
        sw.WriteLine("{");
        sw.WriteLine($"    public Protocol Protocol =>  BlueArchiveAPI.NetworkModels.Protocol.{proto};");

        foreach (var prop in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (prop.IsPrivate)
            {
                if (prop.GetCustomAttribute<JsonPropertyAttribute>() == null)
                    continue;
            }

            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                continue;

            var ptype = prop.FieldType;

            if (prop.DeclaringType != type) continue;

            var name = new Ref<string>();

            foreach (var t in TypeGetName(ptype, name))
                yield return t;

            sw.WriteLine($"    public {name.t} {prop.Name};");
        }

        foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (prop.GetMethod.IsPrivate)
            {
                if (prop.GetCustomAttribute<JsonPropertyAttribute>() == null)
                    continue;
            }

            if (prop.GetCustomAttribute<JsonIgnoreAttribute>() != null || prop.Name == "Protocol")
                continue;

            var ptype = prop.PropertyType;

            if (prop.DeclaringType != type) continue;

            var name = new Ref<string>();

            foreach (var t in TypeGetName(ptype, name))
                yield return t;

            sw.WriteLine($"    public {name.t} {prop.Name};");
        }

        sw.WriteLine("}");
        sw.WriteLine("");

        yield break;
    }

    static void Main(string[] args)
    {
        var protocols = Enum.GetValues<Protocol>();

        var commonTypes = new Queue<Type>();

        using var requestWriter = new StreamWriter(File.Open("Requests.cs", FileMode.Create, FileAccess.Write));
        using var responseWriter = new StreamWriter(File.Open("Responses.cs", FileMode.Create, FileAccess.Write));
        using var commonWriter = new StreamWriter(File.Open("Common.cs", FileMode.Create, FileAccess.Write));
        
        WriteHeader(requestWriter);
        WriteHeader(responseWriter);
        WriteHeader(commonWriter);

        var protoMap = new Dictionary<Protocol, string>()
        {
            [Protocol.Queuing_GetTicketGL] = "QueueingGetTicket",
            [Protocol.Account_VerifyCheckAdultAgree] = "AccountVerifyAdultCheck",
            [Protocol.Cafe_Get] = "CafeGetInfo",
            [Protocol.Notification_EventContentReddotCheck] = "NotificationEventContentReddot",
        };

        var reqdict = new Dictionary<Protocol, Type>();

        var respdict = new Dictionary<Protocol, Type>();

        foreach (var proto2 in protocols)
        {
            var proto = proto2;
            var name = protoMap.TryGetValue(proto, out var val) ? val : proto.ToString().Replace("_", "");
            if (proto == Protocol.NetworkTime_SyncReply) continue;
            if (proto == Protocol.Queuing_GetTicket) proto = Protocol.Queuing_GetTicketGL;
            
            var requestType = sourceAsm.GetType($"MX.NetworkProtocol.{name}Request");
            var responseType = sourceAsm.GetType($"MX.NetworkProtocol.{name}Response");

            if (requestType == null && responseType == null)
            {
                Console.WriteLine($"error when processing: {name}");
                continue;
            }

            reqdict.Add(proto, requestType);
            respdict.Add(proto == Protocol.NetworkTime_Sync ? Protocol.NetworkTime_SyncReply : proto, responseType);

            foreach (var @class in WriteClass(requestWriter, $"IRequest<{name}Response>", proto, requestType))
                commonTypes.Enqueue(@class);
            foreach (var @class in WriteClass(responseWriter, $"IResponse<{name}Request>", proto == Protocol.NetworkTime_Sync ? Protocol.NetworkTime_SyncReply : proto, responseType))
                commonTypes.Enqueue(@class);
        }
        
        while (commonTypes.TryDequeue(out var type))
            if (type != null)
                foreach (var @class in WriteClass(commonWriter, type))
                    commonTypes.Enqueue(@class);

        commonWriter.WriteLine("public static class ProtoDefine");
        commonWriter.WriteLine("{");
        commonWriter.WriteLine("    public static readonly Dictionary<Protocol, Type> requestType = new Dictionary<Protocol, Type>()");
        commonWriter.WriteLine("    {");
        foreach (var pair in reqdict)
            commonWriter.WriteLine($"        [Protocol.{pair.Key}] = typeof({pair.Value.Name}),"); 
        commonWriter.WriteLine("    };");

        commonWriter.WriteLine("    public static readonly Dictionary<Protocol, Type> responseType = new Dictionary<Protocol, Type>()");
        commonWriter.WriteLine("    {");

        foreach (var pair in respdict)
            commonWriter.WriteLine($"        [Protocol.{pair.Key}] = typeof({pair.Value.Name}),");
        
        commonWriter.WriteLine("    };");
        
        commonWriter.WriteLine("}");

    }
}