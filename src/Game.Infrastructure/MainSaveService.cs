using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Application;
using Game.Domain;

namespace Game.Infrastructure;

public sealed class MainSaveException(string message) : Exception(message);

public static class MainSaveService
{
    private const int CurrentSchema = 7;
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
        EnsureSafeToSave(session.World);
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
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            root = document.RootElement.Clone();
            EnsureUniqueProperties(root);
        }
        catch (JsonException error) { throw new MainSaveException($"存档 JSON 无效：{error.Message}"); }
        if (root.ValueKind != JsonValueKind.Object) throw new MainSaveException("存档 envelope 必须为对象。");
        var schema = RequiredInt(root, "SchemaVersion", "存档");
        if (schema is not (4 or 5 or 6 or CurrentSchema)) throw new MainSaveException("不支持的存档版本。");
        var contentHash = RequiredString(root, "ContentHash", "存档");
        var checksum = RequiredString(root, "Checksum", "存档");
        var worldElement = Required(root, "World", "存档");
        if (worldElement.ValueKind != JsonValueKind.Object) throw new MainSaveException("存档缺少有效世界状态。");
        var worldSchema = RequiredInt(worldElement, "SchemaVersion", "存档世界");
        if (worldSchema != schema) throw new MainSaveException("存档 envelope 与世界版本不一致。");
        var worldContentHash = RequiredString(worldElement, "ContentHash", "存档世界");
        if (!string.Equals(contentHash, content.ContentHash, StringComparison.Ordinal))
            throw new MainSaveException("存档内容版本不匹配。");
        if (!string.Equals(worldContentHash, content.ContentHash, StringComparison.Ordinal))
            throw new MainSaveException("存档世界内容版本不匹配。");
        var versionedWorld = ReadVersionedWorld(worldElement, schema);
        if (!string.Equals(checksum, Hash(versionedWorld.ToJsonString(Compact)), StringComparison.Ordinal))
            throw new MainSaveException("存档校验和失败。");
        MainWorld world;
        try { world = versionedWorld.Deserialize<MainWorld>(Compact) ?? throw new MainSaveException("存档缺少世界状态。"); }
        catch (JsonException error) { throw new MainSaveException($"存档世界 JSON 无效：{error.Message}"); }
        Migrate(world, schema);
        world.SchemaVersion = CurrentSchema;
        Validate(world);
        return new MainGameSession(world, content);
    }

    private static void Migrate(MainWorld world, int schema)
    {
        if (schema == 4) world.Outcome = MainGameOutcome.InProgress;
        if (schema <= 5)
        {
            world.TurnPhase = MainTurnPhase.Player;
            world.BattleReturnPhase = MainTurnPhase.Player;
            return;
        }
        if (schema > 6) return;
        if (world.Outcome == MainGameOutcome.InProgress && world.ActiveBattle is null)
            throw new MainSaveException("schema 6 进行中战略存档缺少月度阶段，无法可靠推断恢复点；原文件未修改。");
        world.TurnPhase = world.ActiveBattle is null ? MainTurnPhase.Player : MainTurnPhase.AwaitBattle;
        world.BattleReturnPhase = world.ActiveBattle is { PlayerIsAttacker: false } ? MainTurnPhase.Orders : MainTurnPhase.Player;
        if (world.ActiveBattle is not null) world.ActiveBattle.OrderId = "legacy-schema-6-battle";
    }

    private static JsonObject ReadVersionedWorld(JsonElement world, int schema)
    {
        var versioned = JsonNode.Parse(world.GetRawText())!.AsObject();
        if (schema <= 6)
        {
            versioned.Remove("TurnPhase");
            versioned.Remove("BattleReturnPhase");
            if (versioned["ActiveBattle"] is JsonObject battle) battle.Remove("OrderId");
        }
        if (schema <= 5) versioned.Remove("ActiveBattle");
        if (schema == 4) versioned.Remove("Outcome");
        return versioned;
    }

    private static JsonElement Required(JsonElement parent, string name, string owner)
    {
        if (!parent.TryGetProperty(name, out var value)) throw new MainSaveException($"{owner}缺少必要字段 {name}。");
        return value;
    }

    private static int RequiredInt(JsonElement parent, string name, string owner)
    {
        var value = Required(parent, name, owner);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
            throw new MainSaveException($"{owner}字段 {name} 必须为整数。");
        return result;
    }

    private static string RequiredString(JsonElement parent, string name, string owner)
    {
        var value = Required(parent, name, owner);
        if (value.ValueKind != JsonValueKind.String) throw new MainSaveException($"{owner}字段 {name} 必须为字符串。");
        return value.GetString()!;
    }

    private static void EnsureUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) EnsureUniqueProperties(item);
            return;
        }
        if (element.ValueKind != JsonValueKind.Object) return;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!names.Add(property.Name)) throw new MainSaveException($"存档包含重复字段 {property.Name}。");
            EnsureUniqueProperties(property.Value);
        }
    }

    private static void EnsureSafeToSave(MainWorld world) => ValidateRecovery(world);

    private static void ValidateRecovery(MainWorld world)
    {
        if (world.TurnPhase is not (MainTurnPhase.Player or MainTurnPhase.Orders or MainTurnPhase.AwaitBattle))
            throw new MainSaveException("只能保存玩家、订单或等待战斗的安全阶段。");
        if (world.BattleReturnPhase is not (MainTurnPhase.Player or MainTurnPhase.Orders))
            throw new MainSaveException("存档战斗返回阶段非法。");
        if (world.TurnPhase == MainTurnPhase.AwaitBattle)
        {
            if (world.ActiveBattle is null || world.ActiveBattle.OrderId.Length == 0)
                throw new MainSaveException("等待战斗存档缺少战斗或订单关联。");
        }
        else if (world.ActiveBattle is not null) throw new MainSaveException("存档阶段与进行中战斗不一致。");
    }

    private static void Validate(MainWorld world)
    {
        ValidateRecovery(world);
        if (world.SchemaVersion != CurrentSchema || world.Year < 1 || world.Month is < 1 or > 12 || world.RandomState == 0)
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
