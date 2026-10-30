namespace Game.Domain;

public sealed class MainBattleContent
{
    public List<MainBattleMap> Maps { get; set; } = [];
    public List<List<int>> InitialPositions { get; set; } = [];
    public List<List<int>> AttackRanges { get; set; } = [];
    public List<List<int>> MovementResistance { get; set; } = [];
    public List<List<int>> LandFightModifiers { get; set; } = [];
    public List<List<int>> SkillRanges { get; set; } = [];
    public List<List<string>> UnitSkills { get; set; } = [];
    public List<int> UnitSkillCounts { get; set; } = [];
    public List<List<string?>> SpecialSkills { get; set; } = [];
}

public sealed class MainBattleMap
{
    public string CityId { get; set; } = "";
    public int MapResourceId { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int CityX { get; set; }
    public int CityY { get; set; }
    public List<int> Terrains { get; set; } = [];
    public List<int> TerrainKinds { get; set; } = [];
}

public enum MainBattleResult { Ongoing, AttackerWon, DefenderWon }

public sealed class MainBattleState
{
    public string OrderId { get; set; } = "";
    public string SourceCityId { get; set; } = "";
    public string TargetCityId { get; set; } = "";
    public string AttackerRulerId { get; set; } = "";
    public string DefenderRulerId { get; set; } = "";
    public int AttackerFood { get; set; }
    public int DefenderFood { get; set; }
    public int Weather { get; set; } = 3;
    public int Day { get; set; }
    public int MaxDays { get; set; } = 30;
    public MainBattleResult Result { get; set; }
    public bool PlayerIsAttacker { get; set; } = true;
    public List<MainBattleUnit> Units { get; set; } = [];
    public List<string> Logs { get; set; } = [];
}

public sealed class MainBattleUnit
{
    public string PersonId { get; set; } = "";
    public bool IsAttacker { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Hp { get; set; }
    public int Mp { get; set; }
    public int MaxMp { get; set; }
    public int Move { get; set; }
    public int BaseMove { get; set; }
    public bool HasMoved { get; set; }
    public bool HasActed { get; set; }
    public int State { get; set; }
}
