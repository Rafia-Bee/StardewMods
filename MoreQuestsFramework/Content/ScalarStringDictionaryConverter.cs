using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;

namespace MoreQuestsFramework.Content;

// Accepts a JSON object whose values may be string, number, or bool, and stores
// each value as its string form. Lets content packs write "MinDaysPlayed": 28 or
// "IsPlayerMarried": false without forcing string quotes for every condition value.
internal sealed class ScalarStringDictionaryConverter : JsonConverter<Dictionary<string, string>>
{
    public override Dictionary<string, string>? ReadJson(JsonReader reader, Type objectType, Dictionary<string, string>? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;
        if (reader.TokenType != JsonToken.StartObject)
            throw new JsonSerializationException($"Expected object for condition dictionary; got {reader.TokenType}.");

        var dict = new Dictionary<string, string>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonToken.EndObject)
                return dict;
            if (reader.TokenType != JsonToken.PropertyName)
                throw new JsonSerializationException($"Expected property name; got {reader.TokenType}.");

            string key = (string)reader.Value!;
            if (!reader.Read())
                throw new JsonSerializationException($"Unexpected end of JSON inside condition value for '{key}'.");

            dict[key] = reader.TokenType switch
            {
                JsonToken.String => (string)reader.Value!,
                JsonToken.Integer => Convert.ToString(reader.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                JsonToken.Float => Convert.ToString(reader.Value, CultureInfo.InvariantCulture) ?? string.Empty,
                JsonToken.Boolean => ((bool)reader.Value!) ? "true" : "false",
                JsonToken.Null => string.Empty,
                _ => throw new JsonSerializationException($"Condition value for '{key}' must be a string, number, or bool; got {reader.TokenType}.")
            };
        }
        throw new JsonSerializationException("Unexpected end of JSON while reading condition dictionary.");
    }

    public override void WriteJson(JsonWriter writer, Dictionary<string, string>? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }
        writer.WriteStartObject();
        foreach (var (k, v) in value)
        {
            writer.WritePropertyName(k);
            writer.WriteValue(v);
        }
        writer.WriteEndObject();
    }
}
