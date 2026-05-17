using System;
using Newtonsoft.Json;

namespace MoreQuestsFramework.Rewards;

// RewardSpec is an abstract record with multiple concrete subtypes. Newtonsoft can't
// rehydrate that without a $type discriminator, which would also tie the on-disk
// shape to the C# type names. Instead we lean on RewardCodec (already used for mail
// stashing / net-sync) to round-trip each spec as a single text line.
public sealed class RewardSpecJsonConverter : JsonConverter<RewardSpec>
{
    public override void WriteJson(JsonWriter writer, RewardSpec? value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }
        writer.WriteValue(RewardCodec.Encode(value));
    }

    public override RewardSpec? ReadJson(JsonReader reader, Type objectType, RewardSpec? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;
        if (reader.TokenType == JsonToken.String)
            return RewardCodec.Decode((string)reader.Value!);
        // Legacy save data may have written the polymorphic object shape. Skip over it
        // so the loader falls back to defaults for this entry rather than throwing.
        if (reader.TokenType == JsonToken.StartObject)
        {
            serializer.Deserialize<Newtonsoft.Json.Linq.JObject>(reader);
            return null;
        }
        return null;
    }
}
