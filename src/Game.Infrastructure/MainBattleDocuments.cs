using Game.Domain;

namespace Game.Infrastructure;

internal sealed class BattleDocument
{
    public List<BattleMapDocument> Maps { get; set; } = [];
    public List<List<int>> InitialPositions { get; set; } = [];
    public List<List<string>> UnitSkills { get; set; } = [];
    public List<int> UnitSkillCounts { get; set; } = [];
    public List<List<string?>> SpecialSkills { get; set; } = [];
}

internal sealed class BattleMapDocument
{
    public string CityId { get; set; } = "";
    public int MapResourceId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int CityX { get; set; }
    public int CityY { get; set; }
    public List<int> Terrains { get; set; } = [];
}

internal static class MainBattleMapper
{
    public static MainBattleContent Map(BattleDocument document) => new()
    {
        Maps = document.Maps.Select(map => new MainBattleMap
        {
            CityId = map.CityId, MapResourceId = map.MapResourceId, Width = map.Width, Height = map.Height,
            CityX = map.CityX, CityY = map.CityY, Terrains = map.Terrains
        }).ToList(),
        InitialPositions = document.InitialPositions,
        UnitSkills = document.UnitSkills,
        UnitSkillCounts = document.UnitSkillCounts,
        SpecialSkills = document.SpecialSkills
    };
}
