using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Application;
using Game.Domain;

namespace Game.Infrastructure;

public sealed class MainSaveException(string message) : Exception(message);

public static class MainSaveService
{
    private const int CurrentSchema = 6;
    private static readonly JsonSerializerOptions Compact = new();
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private sealed class Envelope
    {
        public int SchemaVersion { get; set; }
        public string ContentHash { get; set; } = "";
        public string Checksum { get; set; } = "";
        public MainWorld? World { get; set; }
    }

    public static void Save(MainGameSession session, string path)
    {
        var worldJson = JsonSerializer.Serialize(session.World, Compact);
        var envelope = new Envelope
        {
            SchemaVersion = CurrentSchema,
            ContentHash = session.Content.ContentHash,
            Checksum = Hash(worldJson),
            World = session.World
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(envelope, Indented));
        File.Move(temporary, path, true);
    }

    public static MainGameSession Load(string path, MainContent content)
    {
        Envelope envelope;
        try { envelope = JsonSerializer.Deserialize<Envelope>(File.ReadAllText(path), Compact) ?? throw new MainSaveException("存档为空。"); }
        catch (JsonException error) { throw new MainSaveException($"存档 JSON 无效：{error.Message}"); }
        if (envelope.SchemaVersion is not (4 or 5 or CurrentSchema)) throw new MainSaveException("不支持的存档版本。");
        if (!string.Equals(envelope.ContentHash, content.ContentHash, StringComparison.Ordinal))
            throw new MainSaveException("存档内容版本不匹配。");
        if (envelope.World is null) throw new MainSaveException("存档缺少世界状态。");
        if (!string.Equals(envelope.World.ContentHash, content.ContentHash, StringComparison.Ordinal))
            throw new MainSaveException("存档世界内容版本不匹配。");
        var worldJson = JsonSerializer.Serialize(envelope.World, Compact);
        if (!string.Equals(envelope.Checksum, Hash(worldJson), StringComparison.Ordinal))
            throw new MainSaveException("存档校验和失败。");
        Validate(envelope.World);
        if (envelope.World.SchemaVersion < CurrentSchema)
        {
            if (envelope.World.SchemaVersion == 4) envelope.World.Outcome = MainGameOutcome.InProgress;
            envelope.World.SchemaVersion = CurrentSchema;
        }
        return new MainGameSession(envelope.World, content);
    }

    private static void Validate(MainWorld world)
    {
        if (world.SchemaVersion is not (4 or 5 or CurrentSchema) || world.Year < 1 || world.Month is < 1 or > 12 || world.RandomState == 0)
            throw new MainSaveException("存档版本、日期或随机状态非法。");
        var cityIds = world.Cities.Select(city => city.Id).ToHashSet(StringComparer.Ordinal);
        var personIds = world.Persons.Select(person => person.Id).ToHashSet(StringComparer.Ordinal);
        if (cityIds.Count != world.Cities.Count || personIds.Count != world.Persons.Count || !personIds.Contains(world.PlayerRulerId))
            throw new MainSaveException("存档重复 ID 或玩家君主引用非法。");
        if (world.Cities.Any(city => city.Money < 0 || city.Food < 0 || city.Population < 0 || city.MothballArms < 0 ||
            !string.IsNullOrEmpty(city.Belong) && !personIds.Contains(city.Belong) || city.PersonIds.Any(id => !personIds.Contains(id))))
            throw new MainSaveException("存档城市资源或引用非法。");
        if (world.Persons.Any(person => !string.IsNullOrEmpty(person.CityId) && !cityIds.Contains(person.CityId) || person.Thew is < 0 or > 100))
            throw new MainSaveException("存档人物位置或体力非法。");
        if (world.Orders.Any(order => !cityIds.Contains(order.CityId) || !personIds.Contains(order.PersonId)))
            throw new MainSaveException("存档命令引用非法。");
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
