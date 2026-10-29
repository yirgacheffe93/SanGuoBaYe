using Game.Domain;

namespace Game.Application;

public sealed record MainBattleUnitView(
    string PersonId, string Name, bool IsAttacker, int X, int Y, int Hp, int Mp, int Move,
    bool HasActed, int State, int Arms, int ArmsType, bool HasMoved = false);

public sealed record MainBattleView(
    string TargetCityName,
    int AttackerFood,
    int DefenderFood,
    int Weather,
    int Day,
    int MaxDays,
    MainBattleResult Result,
    bool PlayerIsAttacker,
    int MapWidth,
    int MapHeight,
    IReadOnlyList<int> Terrains,
    IReadOnlyList<MainBattleUnitView> Units,
    IReadOnlyList<string> Logs,
    IReadOnlyList<int> TerrainKinds, int CityX, int CityY);

public static class MainBattleViewFactory
{
    public static MainBattleView Create(MainWorld world, MainContent content)
    {
        var battle = world.ActiveBattle ?? throw new InvalidOperationException("没有进行中的战斗。");
        var city = world.Cities.Single(city => city.Id == battle.TargetCityId);
        var map = content.Battle.Maps.Single(map => map.CityId == city.Id);
        return new MainBattleView(city.Name, battle.AttackerFood, battle.DefenderFood, battle.Weather, battle.Day,
            battle.MaxDays, battle.Result, battle.PlayerIsAttacker, map.Width, map.Height, map.Terrains,
            battle.Units.Select(unit =>
            {
                var person = world.Persons.Single(person => person.Id == unit.PersonId);
                return new MainBattleUnitView(person.Id, person.Name, unit.IsAttacker, unit.X, unit.Y, unit.Hp, unit.Mp,
                    unit.Move, unit.HasActed, unit.State, person.Arms, person.ArmsType, unit.HasMoved);
            }).ToList(), battle.Logs, map.TerrainKinds, map.CityX, map.CityY);
    }
}
