using System.Collections.Concurrent;
using System.Text.Json;
using Game.Domain;
using Json.Schema;

namespace Game.Infrastructure;

public static class ScenarioSchemaValidator
{
    private static readonly ConcurrentDictionary<string, Lazy<JsonSchema>> Schemas = new(StringComparer.Ordinal);

    public static World LoadValidatedFile(string contentPath, string schemaPath)
    {
        var json = File.ReadAllText(contentPath);
        Validate(json, schemaPath);
        return ContentLoader.Load(json);
    }

    public static void Validate(string json, string schemaPath)
    {
        var schema = Schemas.GetOrAdd(Path.GetFullPath(schemaPath),
            path => new Lazy<JsonSchema>(() => JsonSchema.FromFile(path))).Value;
        Evaluate(json, schema);
    }

    public static void ValidateWithSchema(string json, string schemaJson)
    {
        var schema = Schemas.GetOrAdd("text:" + schemaJson,
            _ => new Lazy<JsonSchema>(() => JsonSchema.FromText(schemaJson))).Value;
        Evaluate(json, schema);
    }

    private static void Evaluate(string json, JsonSchema schema)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var result = schema.Evaluate(document.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
            if (!result.IsValid)
                throw new ContentException($"Schema 校验失败：{JsonSerializer.Serialize(result)}");
        }
        catch (JsonException error)
        {
            throw new ContentException($"内容或 Schema JSON 无效：{error.Message}");
        }
    }
}
