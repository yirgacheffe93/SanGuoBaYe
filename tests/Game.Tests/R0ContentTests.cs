using Game.Infrastructure;

namespace Game.Tests;

public sealed class R0ContentTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private static readonly string ContentPath = Path.Combine(Root, "content/r0/scenario.json");
    private static readonly string SchemaPath = Path.Combine(Root, "content/schema/scenario.schema.json");

    [Fact]
    public void Complete_r0_content_passes_schema_and_semantic_validation()
    {
        var world = ScenarioSchemaValidator.LoadValidatedFile(ContentPath, SchemaPath);
        Assert.Equal(4, world.Cities.Count);
        Assert.Equal(2, world.Factions.Count);
        Assert.Equal(8, world.Characters.Count);
        Assert.Equal(2, world.Events.Count);
        Assert.Equal(4, world.Roads.Count);
        Assert.Equal("r0-1", world.RulesVersion);
        Assert.Contains(world.Cities, city => city.FactionId == "faction.xuanshuo");
    }

    [Theory]
    [InlineData("\"gold\": 400,", "")]
    [InlineData("\"leadership\": 62", "\"leadership\": 620")]
    public void Schema_rejects_missing_fields_and_out_of_range_values(string original, string replacement)
    {
        var json = File.ReadAllText(ContentPath).Replace(original, replacement, StringComparison.Ordinal);
        Assert.Throws<ContentException>(() => ScenarioSchemaValidator.Validate(json, SchemaPath));
    }

    [Theory]
    [InlineData("\"id\": \"city.beiping\"", "\"id\": \"city.anling\"")]
    [InlineData("\"cityId\": \"city.anling\"", "\"cityId\": \"city.missing\"")]
    [InlineData("\"connections\": [\"city.anling\", \"city.beiping\", \"city.lingchuan\"]", "\"connections\": [\"city.beiping\", \"city.lingchuan\"]")]
    [InlineData("\"triggerTurn\": 4", "\"triggerTurn\": 25")]
    public void Semantic_validation_rejects_invalid_references_roads_and_events(string original, string replacement)
    {
        var json = File.ReadAllText(ContentPath).Replace(original, replacement, StringComparison.Ordinal);
        ScenarioSchemaValidator.Validate(json, SchemaPath);
        Assert.Throws<ContentException>(() => ContentLoader.Load(json));
    }
}
