using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Plana.Extensions
{
    public class HexaMapSerializationBinder : ISerializationBinder
    {
        private static readonly string PlanaAssemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

        public Type BindToType(string? assemblyName, string typeName)
        {
            if (assemblyName != null && assemblyName.StartsWith("BlueArchive", StringComparison.OrdinalIgnoreCase))
                assemblyName = PlanaAssemblyName;

            var qn = $"{assemblyName}.{typeName}, {assemblyName}";

            var t = Type.GetType(qn);
            if (t == null)
                throw new JsonSerializationException($"Could not resolve type '{qn}'");
            return t;
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            assemblyName = PlanaAssemblyName;
            typeName     = serializedType.FullName;
        }
    }

}

