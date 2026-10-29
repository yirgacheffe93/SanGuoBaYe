using Game.Application;

namespace Game.Infrastructure;

public sealed record MainSaveSlot(int Index, string Title, string Path, bool Exists, bool CanLoad,
    DateTime LastWriteUtc, string Summary);

public sealed class MainSaveCatalog(string directory, Game.Domain.MainContent content)
{
    public const int SlotCount = 5;
    public string GetPath(int slot) => slot is >= 1 and <= SlotCount
        ? System.IO.Path.Combine(directory, $"slot-{slot}.json")
        : throw new ArgumentOutOfRangeException(nameof(slot));

    public IReadOnlyList<MainSaveSlot> List()
    {
        var slots = Enumerable.Range(1, SlotCount).Select(index => Describe(index, $"存档 {index}", GetPath(index))).ToList();
        var legacy = System.IO.Path.Combine(directory, "..", "main-m2a-save.json");
        if (File.Exists(legacy)) slots.Add(Describe(0, "旧版快速存档", legacy));
        return slots;
    }

    public MainGameSession Load(MainSaveSlot slot) => MainSaveService.Load(slot.Path, content);
    public void Save(MainGameSession session, int slot) => MainSaveService.Save(session, GetPath(slot));

    private MainSaveSlot Describe(int index, string title, string path)
    {
        if (!File.Exists(path)) return new(index, title, path, false, false, DateTime.MinValue, "空存档");
        try
        {
            var session = MainSaveService.Load(path, content);
            var world = session.World;
            var ruler = world.Persons.Single(person => person.Id == world.PlayerRulerId).Name;
            var location = world.ActiveBattle is null ? "战略地图" : $"{world.Cities.Single(city => city.Id == world.ActiveBattle.TargetCityId).Name}战场";
            return new(index, title, path, true, true, File.GetLastWriteTimeUtc(path),
                $"{world.Year} 年 {world.Month} 月 · {ruler} · {location} · {MainFactionRules.Name(world.Outcome)}");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or MainSaveException or InvalidOperationException)
        {
            return new(index, title, path, true, false, DateTime.MinValue, $"无法读取：{error.Message}");
        }
    }
}
