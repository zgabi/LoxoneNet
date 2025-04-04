using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoxoneNet.Loxone.Converters;

public class GuidConverter : JsonConverter<Guid>
{
    public override Guid ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Read(ref reader, typeToConvert, options);
    }

    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string str = reader.GetString();
        return StringToGuid(str);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(GuidToString(value));
    }

    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(GuidToString(value));
    }

    public static Guid StringToGuid(string str)
    {
        return Guid.Parse(str.Replace("-", ""));
    }

    public static string GuidToString(Guid guid)
    {
        string str = guid.ToString();

        // guid.ToString() is always generating format "00000000-0000-0000-0000-000000000000"
        // remove the last hyphen
        str = str.Remove(23, 1);

        return str;
    }
}

public class GuidAndNameConverter : JsonConverter<GuidAndName>
{
    public override GuidAndName ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Read(ref reader, typeToConvert, options);
    }

    public override GuidAndName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var strs = reader.GetString().Split('/');
        return new GuidAndName
        {
            id = GuidConverter.StringToGuid(strs[0]),
            name = strs.Length > 1 ? strs[1] : null
        };
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, GuidAndName value, JsonSerializerOptions options)
    {
        string str = GuidConverter.GuidToString(value.id);
        if (value.name != null)
        {
            str += '/' + value.name;
        }

        writer.WritePropertyName(str);
    }

    public override void Write(Utf8JsonWriter writer, GuidAndName value, JsonSerializerOptions options)
    {
        string str = GuidConverter.GuidToString(value.id);
        if (value.name != null)
        {
            str += '/' + value.name;
        }
        
        writer.WriteStringValue(str);
    }
}