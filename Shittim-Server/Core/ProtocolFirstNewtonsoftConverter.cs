using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using BlueArchiveAPI.NetworkModels;
using System.Reflection;

namespace BlueArchiveAPI.Core;

public class ProtocolFirstNewtonsoftConverter : JsonConverter
{
    public override bool CanConvert(Type objectType)
    {
        return typeof(ResponsePacket).IsAssignableFrom(objectType);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);
        return obj.ToObject(objectType, serializer);
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var serializerWithoutConverter = new JsonSerializer
        {
            NullValueHandling = serializer.NullValueHandling,
            DefaultValueHandling = serializer.DefaultValueHandling,
            Formatting = serializer.Formatting,
            ReferenceLoopHandling = serializer.ReferenceLoopHandling
        };
        
        var obj = JObject.FromObject(value, serializerWithoutConverter);
        
        writer.WriteStartObject();
        
        if (obj["Protocol"] != null)
        {
            writer.WritePropertyName("Protocol");
            obj["Protocol"].WriteTo(writer);
        }
        
        foreach (var property in obj.Properties().Where(p => p.Name != "Protocol"))
        {
            property.WriteTo(writer);
        }
        
        writer.WriteEndObject();
    }
}
