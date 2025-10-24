using System.Text.Json;
using System.Text.Json.Serialization;
using BlueArchiveAPI.NetworkModels;
using System.Reflection;
using Protocol = Plana.MX.NetworkProtocol.Protocol;

namespace BlueArchiveAPI.Core;

public class ProtocolFirstJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeof(ResponsePacket).IsAssignableFrom(typeToConvert);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        return (JsonConverter)Activator.CreateInstance(
            typeof(ProtocolFirstJsonConverter<>).MakeGenericType(typeToConvert))!;
    }
}

public class ProtocolFirstJsonConverter<T> : JsonConverter<T> where T : ResponsePacket
{
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var text = doc.RootElement.GetRawText();
        return JsonSerializer.Deserialize<T>(text);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        var type = value.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        var protocolProperty = properties.FirstOrDefault(p => p.Name == "Protocol" && p.PropertyType == typeof(Protocol));
        if (protocolProperty != null)
        {
            var protocolValue = protocolProperty.GetValue(value);
            if (protocolValue != null)
            {
                writer.WriteNumber("Protocol", (long)protocolValue);
            }
        }

        foreach (var property in properties)
        {
            if (property.Name == "Protocol") continue;

            var propertyValue = property.GetValue(value);
            
            if (propertyValue == null)
            {
                if (options.DefaultIgnoreCondition != JsonIgnoreCondition.WhenWritingNull)
                {
                    writer.WriteNull(property.Name);
                }
                continue;
            }

            writer.WritePropertyName(property.Name);
            
            var propertyType = property.PropertyType;
            if (typeof(ResponsePacket).IsAssignableFrom(propertyType) && propertyType != typeof(ResponsePacket))
            {
                var tempOptions = new JsonSerializerOptions(options);
                tempOptions.Converters.Clear();
                foreach (var converter in options.Converters)
                {
                    tempOptions.Converters.Add(converter);
                }
                JsonSerializer.Serialize(writer, propertyValue, propertyType, tempOptions);
            }
            else
            {
                JsonSerializer.Serialize(writer, propertyValue, propertyType, options);
            }
        }

        writer.WriteEndObject();
    }
}
