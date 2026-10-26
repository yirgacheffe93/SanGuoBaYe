namespace Game.Domain;

public enum TurnPhase { PlayerPhase, Resolution, TurnEnd }
public enum DevelopmentTrack { Agriculture, Commerce }
public enum OrderStatus { Queued, Completed, Cancelled }

public sealed class RuleParameters
{
    public int MaxTurns { get; set; } = 24;
    public int DevelopmentCost { get; set; } = 100;
    public int LevelCap { get; set; } = 5;
    public int AgricultureIncome { get; set; } = 20;
    public int CommerceIncome { get; set; } = 15;
    public int UpkeepFoodPer1000Troops { get; set; } = 10;
    public int RecruitGoldPer100Troops { get; set; } = 20;
    public int RecruitFoodPer100Troops { get; set; } = 10;
    public int EnvoyCost { get; set; } = 50;
    public int StarvationLossPercent { get; set; } = 10;
    public int TruceBaseScore { get; set; } = 50;
    public int TruceStrengthWeight { get; set; } = 25;
    public int TruceRelationWeight { get; set; } = 25;
    public int TruceAcceptScore { get; set; } = 60;
    public int BattleDamageDivisor { get; set; } = 100000;
    public int BattleMinRoll { get; set; } = 90;
    public int BattleMaxRoll { get; set; } = 110;
    public int BattleMaxRounds { get; set; } = 8;
    public int BattleMinDamage { get; set; } = 1;
}

public sealed class City
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FactionId { get; set; } = "";
    public int Gold { get; set; }
    public int Food { get; set; }
    public int Population { get; set; }
    public int Agriculture { get; set; }
    public int Commerce { get; set; }
    public int Defense { get; set; }
    public int Troops { get; set; }
    public List<string> Connections { get; set; } = [];
}

public sealed class Character
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string CityId { get; set; } = "";
    public int Leadership { get; set; } = 50;
    public int War { get; set; } = 50;
    public int Intellect { get; set; } = 50;
    public int Politics { get; set; } = 50;
    public int Charm { get; set; } = 50;
    public string? OrderId { get; set; }
}

public sealed class Faction
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class RoadDefinition
{
    public string FromCityId { get; set; } = "";
    public string ToCityId { get; set; } = "";
    public int Distance { get; set; } = 1;
}

public sealed class ReportEventDefinition
{
    public string Id { get; set; } = "";
    public int TriggerTurn { get; set; }
    public string TextKey { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class Order
{
    public string Id { get; set; } = "";
    public long Sequence { get; set; }
    public string CityId { get; set; } = "";
    public string CharacterId { get; set; } = "";
    public DevelopmentTrack Track { get; set; }
    public int Cost { get; set; }
    public int CreatedTurn { get; set; }
    public int RemainingTurns { get; set; } = 1;
    public OrderStatus Status { get; set; } = OrderStatus.Queued;
}

public sealed class ReportEntry
{
    public int Turn { get; set; }
    public string Message { get; set; } = "";
}

public sealed class World
{
    public int SchemaVersion { get; set; } = 1;
    public string RulesVersion { get; set; } = "spike-1";
    public string ContentVersion { get; set; } = "spike-1";
    public string RngAlgorithmVersion { get; set; } = "xorshift64star-1";
    public string FactionId { get; set; } = "faction.player";
    public int Turn { get; set; } = 1;
    public TurnPhase Phase { get; set; } = TurnPhase.PlayerPhase;
    public long NextSequence { get; set; } = 1;
    public ulong RandomState { get; set; }
    public RuleParameters Rules { get; set; } = new();
    public List<Faction> Factions { get; set; } = [];
    public List<City> Cities { get; set; } = [];
    public List<Character> Characters { get; set; } = [];
    public List<RoadDefinition> Roads { get; set; } = [];
    public List<ReportEventDefinition> Events { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
    public List<ReportEntry> Reports { get; set; } = [];
}

public static class DeterministicRng
{
    public static ulong Next(ref ulong state)
    {
        if (state == 0) state = 0x9E3779B97F4A7C15UL;
        state ^= state >> 12;
        state ^= state << 25;
        state ^= state >> 27;
        return state * 2685821657736338717UL;
    }

    public static int NextInt(ref ulong state, int inclusiveMin, int inclusiveMax)
    {
        if (inclusiveMin > inclusiveMax) throw new ArgumentOutOfRangeException(nameof(inclusiveMin));
        var range = (ulong)(inclusiveMax - inclusiveMin + 1);
        return inclusiveMin + (int)(Next(ref state) % range);
    }
}

public sealed record BattleSetup(
    int Attackers,
    int Defenders,
    int Attack,
    int Defense,
    int MaxRounds,
    int DamageDivisor = 100000,
    int MinRoll = 90,
    int MaxRoll = 110,
    int MinDamage = 1);
public sealed record BattleRound(int Round, int AttackerLosses, int DefenderLosses, int Attackers, int Defenders);
public sealed record BattleResult(bool AttackerWon, int Attackers, int Defenders, IReadOnlyList<BattleRound> Rounds);

public static class BattleResolver
{
    public static BattleResult Resolve(BattleSetup setup, ref ulong randomState)
    {
        if (setup.Attackers < 0 || setup.Defenders < 0 || setup.Attack < 1 || setup.Defense < 1 || setup.MaxRounds < 1 ||
            setup.DamageDivisor < 1 || setup.MinRoll < 0 || setup.MaxRoll < setup.MinRoll || setup.MinDamage < 1)
            throw new ArgumentOutOfRangeException(nameof(setup));
        var attackers = setup.Attackers;
        var defenders = setup.Defenders;
        var rounds = new List<BattleRound>();
        for (var round = 1; round <= setup.MaxRounds && attackers > 0 && defenders > 0; round++)
        {
            var attackRoll = DeterministicRng.NextInt(ref randomState, setup.MinRoll, setup.MaxRoll);
            var defenseRoll = DeterministicRng.NextInt(ref randomState, setup.MinRoll, setup.MaxRoll);
            var defenderLosses = Math.Min(defenders, Math.Max(setup.MinDamage, attackers * setup.Attack * attackRoll / setup.DamageDivisor));
            var attackerLosses = Math.Min(attackers, Math.Max(setup.MinDamage, defenders * setup.Defense * defenseRoll / setup.DamageDivisor));
            attackers -= attackerLosses;
            defenders -= defenderLosses;
            rounds.Add(new BattleRound(round, attackerLosses, defenderLosses, attackers, defenders));
        }
        return new BattleResult(defenders == 0 && attackers > 0, attackers, defenders, rounds);
    }
}
