using Game.Domain;

namespace Game.Application;

public interface IMainGameApplication
{
    MainGameView GetView();
    MainBattleView GetBattleView();
    MainCommandPreview PreviewCommand(MainCommandRequest request);
    MainCommandResult IssueCommand(MainCommandRequest request);
    void EnterBattle(string orderId);
    IReadOnlyList<string> EndMonth(bool runAi = true);
    IReadOnlyList<(string Id, string Name)> GetDiscoveredTools(string cityId);
    IReadOnlyList<(string Id, string Name, int Distance)> GetTravelTargets(string cityId);
    IReadOnlyList<string> GetCommandTargetPersonIds(int commandId, string cityId);
    MainSearchDiagnostics GetSearchDiagnostics(string cityId, string personId);
    string GetToolName(string toolId);
    IReadOnlyList<(string Id, string Name)> GetBattleSkills(string personId);
    IReadOnlyList<(int X, int Y)> GetBattleMovement(string personId);
    IReadOnlyList<(int X, int Y)> GetBattleRange(string personId, string? skillId);
    string? GetBattleAttackFailure(string personId, string? targetId);
    string? GetBattleSkillFailure(string personId, string? skillId, string? targetId);
    MainBattleActionResult MoveBattle(string personId, int x, int y);
    MainBattleActionResult AttackBattle(string personId, string targetId);
    MainBattleActionResult UseBattleSkill(string personId, string skillId, string targetId);
    MainBattleActionResult WaitBattle(string personId);
    MainBattleActionResult EndBattleTurn();
    MainBattleActionResult RetreatBattle();
    MainBattleActionResult ResolveBattle();
    IReadOnlyList<MainSaveSlotInfo> GetSaveSlots();
    void SaveToSlot(int slot);
    void LoadFromSlot(int slot);
}

public interface IMainSaveStore
{
    IReadOnlyList<MainSaveSlotInfo> List();
    MainGameSession Load(int slot);
    void Save(MainGameSession session, int slot);
}

public sealed record MainBattleActionResult(bool Success, string Code, string Message);
public sealed record MainSaveSlotInfo(int Index, bool Exists, bool CanLoad, DateTime LastWriteUtc, MainSaveSummary? Summary, string? Error);
public sealed record MainSaveSummary(int Year, int Month, string RulerName, bool InBattle, string LocationName,
    MainGameOutcome Outcome, MainTurnPhase TurnPhase);
public sealed record MainPeriodOption(int Period, int Year, int Month, IReadOnlyList<MainRulerOption> Rulers);
public sealed record MainRulerOption(string Id, string Name, int SourceIndex);
