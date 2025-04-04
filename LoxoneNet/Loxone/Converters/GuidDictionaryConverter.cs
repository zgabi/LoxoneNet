using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoxoneNet.Loxone.Converters;

public class GuidDictionaryConverter : JsonConverter<Dictionary<Guid, JsonElement>>
{
    public override Dictionary<Guid, JsonElement> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var result = new Dictionary<Guid, JsonElement>();
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new Exception("Invalid Dictionary");
        }

        reader.Read();

        while (reader.TokenType == JsonTokenType.PropertyName)
        {
            string str = reader.GetString();
            var guid = Guid.Parse(str.Replace("-", ""));
            reader.Read();

            var item = JsonSerializer.Deserialize<JsonElement>(ref reader, options);
            result.Add(guid, item);

            if (reader.TokenType != JsonTokenType.EndObject)
            {
                throw new Exception("Invalid Dictionary");
            }

            reader.Read();
        }

        return result;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<Guid, JsonElement> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var pair in value)
        {
            writer.WritePropertyName(GuidConverter.GuidToString(pair.Key));

            var data = JsonSerializer.SerializeToUtf8Bytes(pair.Value, options);
            writer.WriteRawValue(data);
        }
        
        writer.WriteEndObject();
    }
}