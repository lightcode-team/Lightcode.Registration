using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

/// <summary>
/// Persiste o JSON Schema como documento BSON embutido (subdocumento), mantendo <see cref="string"/> no modelo.
/// Aceita leitura legada em que o valor foi gravado como string.
/// </summary>
public sealed class SchemaJsonBsonSerializer : SerializerBase<string>
{
    private static readonly JsonWriterSettings RelaxedJsonSettings = new()
    {
        OutputMode = JsonOutputMode.RelaxedExtendedJson,
        Indent = false
    };

    public override string Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        return reader.CurrentBsonType switch
        {
            BsonType.Null => ReadNullToEmpty(reader),
            BsonType.String => reader.ReadString(),
            BsonType.Document => DocumentToJson(BsonSerializer.Deserialize<BsonDocument>(reader)),
            _ => throw new FormatException(
                $"SchemaJson deve ser um objeto JSON ou string legada; tipo BSON: {reader.CurrentBsonType}.")
        };
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, string value)
    {
        if (value is null)
        {
            context.Writer.WriteNull();
            return;
        }

        var document = BsonDocument.Parse(value);
        BsonSerializer.Serialize(context.Writer, document);
    }

    private static string ReadNullToEmpty(IBsonReader reader)
    {
        reader.ReadNull();
        return string.Empty;
    }

    private static string DocumentToJson(BsonDocument document) => document.ToJson(RelaxedJsonSettings);
}
