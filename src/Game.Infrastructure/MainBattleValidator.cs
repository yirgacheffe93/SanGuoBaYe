using Game.Domain;

namespace Game.Infrastructure;

internal static class MainBattleValidator
{
    public static void Validate(MainContent content)
    {
        var battle = content.Battle;
        if (battle.Maps.Count == 0) return;
        var cityIds = content.Periods[0].Cities.Select(city => city.Id).ToHashSet(StringComparer.Ordinal);
        var skillIds = content.Skills.Select(skill => skill.Id).ToHashSet(StringComparer.Ordinal);
        if (battle.Maps.Count != 38 || battle.InitialPositions.Count != 9 || battle.InitialPositions.Any(row => row.Count != 20) ||
            battle.AttackRanges.Count != 6 || battle.AttackRanges.Any(row => row.Count != 25) ||
            battle.MovementResistance.Count != 6 || battle.MovementResistance.Any(row => row.Count != 46) ||
            battle.LandFightModifiers.Count != 6 || battle.LandFightModifiers.Any(row => row.Count != 8) ||
            battle.SkillRanges.Count != 30 || battle.SkillRanges.Any(row => row.Count != 81) ||
            battle.UnitSkills.Count != 6 || battle.UnitSkillCounts.Count != 6 || battle.SpecialSkills.Count != 4 || battle.SpecialSkills.Any(row => row.Count != 200))
            throw new MainContentException("战斗地图、站位、范围、地形或技能数量非法。");
        foreach (var map in battle.Maps)
        {
            if (!cityIds.Contains(map.CityId) || map.Width <= 0 || map.Height <= 0 || map.Terrains.Count != map.Width * map.Height ||
                map.TerrainKinds.Count != map.Terrains.Count || map.TerrainKinds.Any(kind => kind is < 0 or > 7) || map.Terrains.Any(tile => tile is < 0 or >= 46) ||
                map.Terrains.Count(terrain => terrain == 3) != 1 || map.CityX < 0 || map.CityX >= map.Width || map.CityY < 0 || map.CityY >= map.Height)
                throw new MainContentException($"城市 {map.CityId} 战斗地图非法。");
        }
        if (battle.UnitSkills.SelectMany(ids => ids).Any(id => !skillIds.Contains(id)) ||
            battle.SpecialSkills.SelectMany(ids => ids).Any(id => id is not null && !skillIds.Contains(id)))
            throw new MainContentException("战斗技能引用非法。");
    }
}
