namespace Game.Domain;

public enum TurnPhase { PlayerPhase, Resolution, TurnEnd }
public enum DevelopmentTrack { Agriculture, Commerce }
public enum OrderStatus { Queued, Completed, Cancelled }

public sealed class RuleParameters
{
    public int DevelopmentCost { get; set; } = 100;
    public int LevelCap { get; set; } = 5;
    public int AgricultureIncome { get; set; } = 20;
    public int CommerceIncome { get; set; } = 15;
}

public sealed class City
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FactionId { get; set; } = "";
    public int Gold { get; set; }
    public int Food { get; set; }
    public int Agriculture { get; set; }
    public int Commerce { get; set; }
    public int Troops { get; set; }
    public List<string> Connections { get; set; } = [];
}

public sealed class Character
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string FactionId { get; set; } = "";
    public string CityId { get; set; } = "";
    public string? OrderId { get; set; }
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
    public List<City> Cities { get; set; } = [];
    public List<Character> Characters { get; set; } = [];
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

public sealed record BattleSetup(int Attackers, int Defenders, int Attack, int Defense, int MaxRounds);
public sealed record BattleRound(int Round, int AttackerLosses, int DefenderLosses, int Attackers, int Defenders);
public sealed record BattleResult(bool AttackerWon, int Attackers, int Defenders, IReadOnlyList<BattleRound> Rounds);

public static class BattleResolver
{
    public static BattleResult Resolve(BattleSetup setup, ref ulong randomState)
    {
        if (setup.Attackers < 0 || setup.Defenders < 0 || setup.Attack < 1 || setup.Defense < 1 || setup.MaxRounds < 1)
            throw new ArgumentOutOfRangeException(nameof(setup));
        var attackers = setup.Attackers;
        var defenders = setup.Defenders;
        var rounds = new List<BattleRound>();
        for (var round = 1; round <= setup.MaxRounds && attackers > 0 && defenders > 0; round++)
        {
            var attackRoll = DeterministicRng.NextInt(ref randomState, 90, 110);
            var defenseRoll = DeterministicRng.NextInt(ref randomState, 90, 110);
            var defenderLosses = Math.Min(defenders, Math.Max(1, attackers * setup.Attack * attackRoll / 100000));
            var attackerLosses = Math.Min(attackers, Math.Max(1, defenders * setup.Defense * defenseRoll / 100000));
            attackers -= attackerLosses;
            defenders -= defenderLosses;
            rounds.Add(new BattleRound(round, attackerLosses, defenderLosses, attackers, defenders));
        }
        return new BattleResult(defenders == 0 && attackers > 0, attackers, defenders, rounds);
    }
}
