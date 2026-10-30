using Game.Application;

namespace Game.Infrastructure;

public sealed class MainSaveStore(MainSaveCatalog catalog) : IMainSaveStore
{
    public IReadOnlyList<MainSaveSlotInfo> List() => catalog.List()
        .Select(slot => new MainSaveSlotInfo(slot.Index, slot.Exists, slot.CanLoad, slot.LastWriteUtc, slot.Summary, slot.Error)).ToList();

    public MainGameSession Load(int slot)
    {
        var saved = catalog.List().SingleOrDefault(candidate => candidate.Index == slot)
            ?? throw new MainSaveException($"存档槽位 {slot} 不存在。");
        return catalog.Load(saved);
    }

    public void Save(MainGameSession session, int slot) => catalog.Save(session, slot);
}
