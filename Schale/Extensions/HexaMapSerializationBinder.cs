using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Schale.Extensions
{
    public class HexaMapSerializationBinder : ISerializationBinder
    {
        private static readonly string CurrentAssemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

        public Type BindToType(string? assemblyName, string typeName)
        {
            if (assemblyName != null && assemblyName.StartsWith("BlueArchive", StringComparison.OrdinalIgnoreCase))
                assemblyName = CurrentAssemblyName;

            var qualifiedName = $"{assemblyName}.{typeName}, {assemblyName}";

            var resolvedType = Type.GetType(qualifiedName);
            if (resolvedType == null)
                throw new JsonSerializationException($"Unable to resolve type '{qualifiedName}'");
            return resolvedType;
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            assemblyName = CurrentAssemblyName;
            typeName = serializedType.FullName;
        }
    }
}


