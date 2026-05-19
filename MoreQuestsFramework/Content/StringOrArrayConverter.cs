using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MoreQuestsFramework.Content;

// Accepts either "Item": "X" or "Item": ["X", "Y"].
public sealed class StringOrArrayConverter : JsonConverter<List<string>>
{
    public override List<string>? ReadJson(JsonReader reader, Type objectType, List<string>? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        switch (reader.TokenType)
        {
            case JsonToken.Null:
                return new List<string>();
            case JsonToken.String:
                return new List<string> { (string)reader.Value! };
            case JsonToken.StartArray:
                var list = new List<string>();
                serializer.Populate(reader, list);
                return list;
            default:
                throw new JsonSerializationException(
                    $"Expected string or array of strings; got {reader.TokenType}.");
        }
    }

    public override void WriteJson(JsonWriter writer, List<string>? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }
        if (value.Count == 1)
        {
            writer.WriteValue(value[0]);
            return;
        }
        serializer.Serialize(writer, value);
    }
}
