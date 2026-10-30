using Game.Application;

namespace Game.Infrastructure;

public sealed record MainSaveSlot(int Index, string Title, string Path, bool Exists, bool CanLoad,
    DateTime LastWriteUtc, MainSaveSummary? Summary, string? Error);

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
        if (!File.Exists(path)) return new(index, title, path, false, false, DateTime.MinValue, null, null);
        try
        {
            var session = MainSaveService.Load(path, content);
            var world = session.World;
            var ruler = world.Persons.Single(person => person.Id == world.PlayerRulerId).Name;
            var location = world.ActiveBattle is null ? "战略地图" : world.Cities.Single(city => city.Id == world.ActiveBattle.TargetCityId).Name;
            var summary = new MainSaveSummary(world.Year, world.Month, ruler, world.ActiveBattle is not null, location, world.Outcome, world.TurnPhase);
            return new(index, title, path, true, true, File.GetLastWriteTimeUtc(path), summary, null);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or MainSaveException or InvalidOperationException)
        {
            return new(index, title, path, true, false, DateTime.MinValue, null, error.Message);
        }
    }
}
