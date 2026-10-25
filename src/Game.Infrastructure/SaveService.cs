using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Application;
using Game.Domain;

namespace Game.Infrastructure;

public sealed class SaveException(string message) : Exception(message);

public static class SaveService
{
    private const int CurrentSchema = 1;
    private static readonly JsonSerializerOptions Compact = new();
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private sealed class Envelope
    {
        public int SchemaVersion { get; set; }
        public string Checksum { get; set; } = "";
        public World? World { get; set; }
    }

    public static void Save(GameSession session, string path)
    {
        if (session.World.Phase != TurnPhase.PlayerPhase) throw new SaveException("只能在玩家下令阶段存档。");
        var worldJson = JsonSerializer.Serialize(session.World, Compact);
        var envelope = new Envelope { SchemaVersion = CurrentSchema, Checksum = Hash(worldJson), World = session.World };
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(envelope, Indented));
        File.Move(temporary, path, true);
    }

    public static GameSession Load(string path)
    {
        Envelope envelope;
        try { envelope = JsonSerializer.Deserialize<Envelope>(File.ReadAllText(path), Compact) ?? throw new SaveException("存档为空。"); }
        catch (JsonException error) { throw new SaveException($"存档 JSON 无效：{error.Message}"); }
        if (envelope.SchemaVersion != CurrentSchema) throw new SaveException("不支持的存档版本。");
        if (envelope.World is null) throw new SaveException("存档缺少世界状态。");
        var worldJson = JsonSerializer.Serialize(envelope.World, Compact);
        if (!string.Equals(envelope.Checksum, Hash(worldJson), StringComparison.Ordinal)) throw new SaveException("存档校验和失败。");
        Validate(envelope.World);
        return new GameSession(envelope.World);
    }

    private static void Validate(World world)
    {
        if (world.SchemaVersion != CurrentSchema || world.Phase != TurnPhase.PlayerPhase || world.Turn < 1 || world.RandomState == 0)
            throw new SaveException("存档结构或阶段非法。");
        var cityIds = world.Cities.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var characterIds = world.Characters.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        if (cityIds.Count != world.Cities.Count || characterIds.Count != world.Characters.Count)
            throw new SaveException("存档存在重复 ID。");
        if (world.Cities.Any(c => c.Gold < 0 || c.Food < 0 || c.Troops < 0) || world.Characters.Any(c => !cityIds.Contains(c.CityId)))
            throw new SaveException("存档资源或引用非法。");
        var orderIds = world.Orders.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
        if (orderIds.Count != world.Orders.Count || world.Orders.Any(o => !cityIds.Contains(o.CityId) || !characterIds.Contains(o.CharacterId)))
            throw new SaveException("存档任务引用非法。");
        if (world.Characters.Any(c => c.OrderId is not null && !world.Orders.Any(o => o.Id == c.OrderId && o.Status == OrderStatus.Queued)))
            throw new SaveException("存档武将占用状态非法。");
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
