using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain;

namespace Game.Application;

public sealed record CommandResult(bool Accepted, string Code, string Message, string? OrderId = null);
public sealed record DevelopmentPreview(bool Available, string Code, string Message, int Cost, int GoldAfter, int CompletionTurn);

public sealed class GameSession
{
    private static readonly JsonSerializerOptions CanonicalOptions = new() { WriteIndented = false };
    private bool resolving;

    public GameSession(World world) => World = world ?? throw new ArgumentNullException(nameof(world));
    public World World { get; private set; }

    public DevelopmentPreview PreviewDevelopment(string cityId, string characterId, DevelopmentTrack track)
    {
        var city = World.Cities.FirstOrDefault(c => c.Id == cityId);
        var character = World.Characters.FirstOrDefault(c => c.Id == characterId);
        if (city is null || character is null) return Unavailable("not_found", "城市或武将不存在。");
        if (city.FactionId != World.FactionId || character.FactionId != World.FactionId || character.CityId != cityId)
            return Unavailable("invalid_target", "只能选择同城己方武将。");
        if (character.OrderId is not null) return Unavailable("character_busy", $"{character.Name} 已有任务。");
        var level = track == DevelopmentTrack.Agriculture ? city.Agriculture : city.Commerce;
        if (level >= World.Rules.LevelCap) return Unavailable("level_cap", "该项目已达到等级上限。");
        if (city.Gold < World.Rules.DevelopmentCost)
            return Unavailable("insufficient_gold", $"金不足：需要 {World.Rules.DevelopmentCost}，当前 {city.Gold}。");
        return new DevelopmentPreview(true, "ok", "可执行", World.Rules.DevelopmentCost,
            city.Gold - World.Rules.DevelopmentCost, World.Turn);

        DevelopmentPreview Unavailable(string code, string message) => new(false, code, message,
            World.Rules.DevelopmentCost, city?.Gold ?? 0, World.Turn);
    }

    public CommandResult IssueDevelopment(string cityId, string characterId, DevelopmentTrack track)
    {
        if (World.Phase != TurnPhase.PlayerPhase || resolving) return Rejected("invalid_phase", "当前不能下令。");
        var preview = PreviewDevelopment(cityId, characterId, track);
        if (!preview.Available) return Rejected(preview.Code, preview.Message);
        var city = World.Cities.Single(c => c.Id == cityId);
        var character = World.Characters.Single(c => c.Id == characterId);
        var order = new Order
        {
            Id = $"order.{World.NextSequence}", Sequence = World.NextSequence++, CityId = cityId,
            CharacterId = characterId, Track = track, Cost = preview.Cost, CreatedTurn = World.Turn
        };
        city.Gold -= preview.Cost;
        character.OrderId = order.Id;
        World.Orders.Add(order);
        return new CommandResult(true, "accepted", $"已安排{character.Name}开发{city.Name}。", order.Id);
    }

    public CommandResult CancelOrder(string orderId)
    {
        if (World.Phase != TurnPhase.PlayerPhase || resolving) return Rejected("invalid_phase", "当前不能取消任务。");
        var order = World.Orders.FirstOrDefault(o => o.Id == orderId);
        if (order is null) return Rejected("not_found", "任务不存在。");
        if (order.Status != OrderStatus.Queued || order.CreatedTurn != World.Turn)
            return Rejected("not_cancellable", "只有本回合排队中的任务可以取消。");
        var city = World.Cities.Single(c => c.Id == order.CityId);
        var character = World.Characters.Single(c => c.Id == order.CharacterId);
        city.Gold += order.Cost;
        character.OrderId = null;
        order.Status = OrderStatus.Cancelled;
        return new CommandResult(true, "cancelled", "任务已取消，成本已退回。", order.Id);
    }

    public IReadOnlyList<string> EndTurn()
    {
        if (World.Phase != TurnPhase.PlayerPhase || resolving) throw new InvalidOperationException("当前不能结束回合。");
        resolving = true;
        try
        {
            World.Phase = TurnPhase.Resolution;
            var completedTurn = World.Turn;
            var messages = new List<string>();
            foreach (var order in World.Orders.Where(o => o.Status == OrderStatus.Queued)
                         .OrderBy(o => o.CityId, StringComparer.Ordinal).ThenBy(o => o.Sequence))
            {
                var city = World.Cities.Single(c => c.Id == order.CityId);
                var character = World.Characters.Single(c => c.Id == order.CharacterId);
                order.RemainingTurns--;
                if (order.RemainingTurns == 0)
                {
                    if (order.Track == DevelopmentTrack.Agriculture) city.Agriculture++;
                    else city.Commerce++;
                    order.Status = OrderStatus.Completed;
                    character.OrderId = null;
                    messages.Add($"{city.Name}完成{TrackName(order.Track)}开发，等级提升。");
                }
            }
            World.Phase = TurnPhase.TurnEnd;
            foreach (var message in messages) World.Reports.Add(new ReportEntry { Turn = completedTurn, Message = message });
            World.Turn++;
            foreach (var city in World.Cities.OrderBy(c => c.Id, StringComparer.Ordinal))
            {
                var income = city.Agriculture * World.Rules.AgricultureIncome + city.Commerce * World.Rules.CommerceIncome;
                city.Gold += income;
            }
            World.Phase = TurnPhase.PlayerPhase;
            return messages;
        }
        finally { resolving = false; }
    }

    public GameView GetView() => new(
        World.Turn,
        World.Rules.DevelopmentCost,
        World.Cities.OrderBy(c => c.Id, StringComparer.Ordinal)
            .Select(c => new CityView(c.Id, c.Name, c.Gold, c.Food, c.Agriculture, c.Commerce, c.Troops, c.Connections)).ToList(),
        World.Characters.OrderBy(c => c.Id, StringComparer.Ordinal)
            .Select(c => new CharacterView(c.Id, c.Name, c.CityId, c.OrderId is not null)).ToList(),
        World.Orders.Where(o => o.Status == OrderStatus.Queued).OrderBy(o => o.Sequence)
            .Select(o => new OrderView(o.Id, o.CityId, World.Cities.Single(c => c.Id == o.CityId).Name,
                World.Characters.Single(c => c.Id == o.CharacterId).Name, o.Track, o.RemainingTurns)).ToList(),
        World.Reports.OrderBy(r => r.Turn).Select(r => new ReportView(r.Turn, r.Message)).ToList());

    public string GetStateHash() => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(World, CanonicalOptions)))).ToLowerInvariant();

    public void Restore(World world)
    {
        if (resolving) throw new InvalidOperationException("结算中不能读档。");
        World = world ?? throw new ArgumentNullException(nameof(world));
    }

    private static string TrackName(DevelopmentTrack track) => track == DevelopmentTrack.Agriculture ? "农业" : "商业";
    private static CommandResult Rejected(string code, string message) => new(false, code, message);
}
