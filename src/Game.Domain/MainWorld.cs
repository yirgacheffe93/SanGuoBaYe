namespace Game.Domain;

public sealed class MainContent
{
    public string ContentVersion { get; set; } = "main-sgby-1";
    public string ContentHash { get; set; } = "";
    public string Source { get; set; } = "main/libs/SGBY.lib";
    public List<MainTool> Tools { get; set; } = [];
    public List<MainSkill> Skills { get; set; } = [];
    public List<MainPeriod> Periods { get; set; } = [];
    public MainBattleContent Battle { get; set; } = new();
}

public sealed class MainPeriod
{
    public int Period { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public List<string> RulerIds { get; set; } = [];
    public List<MainCity> Cities { get; set; } = [];
    public List<MainPerson> Persons { get; set; } = [];
    public List<MainSearchCondition> PersonConditions { get; set; } = [];
    public List<MainSearchCondition> ToolConditions { get; set; } = [];
}

public sealed class MainSearchCondition
{
    public int SourceIndex { get; set; }
    public int Birth { get; set; }
    public int Bole { get; set; }
    public int CityIndex { get; set; }
}

public sealed class MainCityToolEntry
{
    public string ToolId { get; set; } = "";
    public bool Discovered { get; set; }
}

public sealed class MainCity
{
    public string Id { get; set; } = "";
    public int SourceIndex { get; set; }
    public string Name { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int NativeX { get; set; }
    public int NativeY { get; set; }
    public int State { get; set; }
    public string Belong { get; set; } = "";
    public string SatrapId { get; set; } = "";
    public int FarmingLimit { get; set; }
    public int Farming { get; set; }
    public int CommerceLimit { get; set; }
    public int Commerce { get; set; }
    public int PeopleDevotion { get; set; }
    public int AvoidCalamity { get; set; }
    public int PopulationLimit { get; set; }
    public int Population { get; set; }
    public int Money { get; set; }
    public int Food { get; set; }
    public int MothballArms { get; set; }
    public List<string> PersonIds { get; set; } = [];
    public List<string> ToolIds { get; set; } = [];
    public List<MainCityToolEntry> ToolEntries { get; set; } = [];
    public List<string> Connections { get; set; } = [];
}

public enum MainPersonStatus { Active, Free, Captive, Executed }

public sealed class MainPerson
{
    public string Id { get; set; } = "";
    public MainPersonStatus Status { get; set; } = MainPersonStatus.Active;
    public int SourceIndex { get; set; }
    public string Name { get; set; } = "";
    public string OldBelong { get; set; } = "";
    public string Belong { get; set; } = "";
    public string CityId { get; set; } = "";
    public int Level { get; set; }
    public int Force { get; set; }
    public int Iq { get; set; }
    public int Devotion { get; set; }
    public int Character { get; set; }
    public int Experience { get; set; }
    public int Thew { get; set; }
    public int ArmsType { get; set; }
    public int Arms { get; set; }
    public string Tool1Id { get; set; } = "";
    public string Tool2Id { get; set; } = "";
    public List<string> EquipIds { get; set; } = [];
    public int Age { get; set; }
    public string? OrderId { get; set; }
}

public sealed class MainTool
{
    public string Id { get; set; } = "";
    public int SourceIndex { get; set; }
    public string Name { get; set; } = "";
    public int UseFlag { get; set; }
    public int ChangeAttackRange { get; set; }
    public int Attack { get; set; }
    public int Iq { get; set; }
    public int Move { get; set; }
    public int Arms { get; set; }
}

public sealed class MainSkill
{
    public string Id { get; set; } = "";
    public int SourceIndex { get; set; }
    public string Name { get; set; } = "";
    public int Aim { get; set; }
    public int State { get; set; }
    public int Power { get; set; }
    public int Destroy { get; set; }
    public int UseMp { get; set; }
    public List<int> Weather { get; set; } = [];
    public List<int> Eland { get; set; } = [];
    public List<int> Oland { get; set; } = [];
    public List<int> Earm { get; set; } = [];
}

public sealed class MainOrder
{
    public string Id { get; set; } = "";
    public int OrderId { get; set; }
    public string PersonId { get; set; } = "";
    public string CityId { get; set; } = "";
    public string ObjectId { get; set; } = "";
    public int Arms { get; set; }
    public int Food { get; set; }
    public int Money { get; set; }
    public int Consume { get; set; }
    public int TimeCount { get; set; }
    public List<string> ParticipantIds { get; set; } = [];
    public bool FoodPaid { get; set; }
}

public enum MainGameOutcome { InProgress, Won, Lost }

public sealed class MainWorld
{
    public int SchemaVersion { get; set; } = 6;
    public string ContentVersion { get; set; } = "main-sgby-1";
    public string ContentHash { get; set; } = "";
    public string RngAlgorithmVersion { get; set; } = "main-compatible-1";
    public int Period { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string PlayerRulerId { get; set; } = "";
    public ulong RandomState { get; set; }
    public MainGameOutcome Outcome { get; set; } = MainGameOutcome.InProgress;
    public long NextSequence { get; set; } = 1;
    public List<MainCity> Cities { get; set; } = [];
    public List<MainPerson> Persons { get; set; } = [];
    public List<MainOrder> Orders { get; set; } = [];
    public MainBattleState? ActiveBattle { get; set; }
    public List<ReportEntry> Reports { get; set; } = [];
}
