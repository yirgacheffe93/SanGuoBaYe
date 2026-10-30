using Game.Application;

namespace Game.Godot;

public sealed class MainSaveController(IMainGameApplication app)
{
    public IReadOnlyList<MainSaveSlotInfo> Slots() => app.GetSaveSlots();
    public bool CanContinue() => app.GetSaveSlots().Any(slot => slot.CanLoad);

    public int? LatestSlot() => app.GetSaveSlots().Where(slot => slot.CanLoad)
        .OrderByDescending(slot => slot.LastWriteUtc).FirstOrDefault()?.Index;

    public MainSaveOperationResult Save(int slot)
    {
        try
        {
            app.SaveToSlot(slot);
            return new MainSaveOperationResult(true, $"已保存到存档 {slot}。");
        }
        catch (Exception error)
        {
            return new MainSaveOperationResult(false, $"保存失败：{error.Message}");
        }
    }

    public MainSaveOperationResult Load(int slot)
    {
        try
        {
            app.LoadFromSlot(slot);
            return new MainSaveOperationResult(true, "读档成功。");
        }
        catch (Exception error)
        {
            return new MainSaveOperationResult(false, $"读档失败：{error.Message}");
        }
    }
}

public sealed record MainSaveOperationResult(bool Success, string Message);
