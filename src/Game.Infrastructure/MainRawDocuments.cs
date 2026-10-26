namespace Game.Infrastructure;

internal sealed class SourceDocument { public string Commit { get; set; } = ""; }

internal sealed class ContentDocument
{
    public SourceDocument Source { get; set; } = new();
    public List<ToolDocument> Tools { get; set; } = [];
    public List<SkillDocument> Skills { get; set; } = [];
}

internal sealed class ScenarioDocument
{
    public SourceDocument Source { get; set; } = new();
    public List<PeriodDocument> Periods { get; set; } = [];
}

internal sealed class PeriodDocument
{
    public int Period { get; set; }
    public DateDocument Date { get; set; } = new();
    public List<CityDocument> Cities { get; set; } = [];
    public List<PersonDocument> Persons { get; set; } = [];
    public List<SearchConditionDocument> PersonSearchConditions { get; set; } = [];
    public List<SearchConditionDocument> ToolSearchConditions { get; set; } = [];
}

internal sealed class DateDocument { public int Year { get; set; } public int Month { get; set; } }

internal sealed class CityDocument
{
    public string Id { get; set; } = "";
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public RawCity Raw { get; set; } = new();
    public string? OwnerPersonId { get; set; }
    public string? SatrapPersonId { get; set; }
    public List<string> PersonIds { get; set; } = [];
    public List<string> ToolIds { get; set; } = [];
    public List<ToolEntryDocument> ToolEntries { get; set; } = [];
}

internal sealed class RawCity
{
    public int State { get; set; }
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
}

internal sealed class PersonDocument
{
    public string Id { get; set; } = "";
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public RawPerson Raw { get; set; } = new();
    public string? OldOwnerPersonId { get; set; }
    public string? OwnerPersonId { get; set; }
    public string? Tool1Id { get; set; }
    public string? Tool2Id { get; set; }
}

internal sealed class RawPerson
{
    public int Level { get; set; }
    public int Force { get; set; }
    public int IQ { get; set; }
    public int Devotion { get; set; }
    public int Character { get; set; }
    public int Experience { get; set; }
    public int Thew { get; set; }
    public int ArmsType { get; set; }
    public int Arms { get; set; }
    public List<int> Equip { get; set; } = [];
    public int Age { get; set; }
}

internal sealed class ToolDocument
{
    public string Id { get; set; } = "";
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public RawTool Raw { get; set; } = new();
}

internal sealed class RawTool
{
    public int useflag { get; set; }
    public int changeAttackRange { get; set; }
    public int at { get; set; }
    public int iq { get; set; }
    public int move { get; set; }
    public int arm { get; set; }
}

internal sealed class SkillDocument
{
    public string Id { get; set; } = "";
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public RawSkill Raw { get; set; } = new();
}

internal sealed class RawSkill
{
    public int aim { get; set; }
    public int state { get; set; }
    public int power { get; set; }
    public int destroy { get; set; }
    public int useMp { get; set; }
    public List<int> weather { get; set; } = [];
    public List<int> eland { get; set; } = [];
    public List<int> oland { get; set; } = [];
    public List<int> earm { get; set; } = [];
}

internal sealed class SearchConditionDocument
{
    public int Index { get; set; }
    public int Birth { get; set; }
    public int Bole { get; set; }
    public int City { get; set; }
}

internal sealed class ToolEntryDocument
{
    public string ToolId { get; set; } = "";
    public bool Discovered { get; set; }
}
