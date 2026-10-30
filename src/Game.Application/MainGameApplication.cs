using Game.Domain;

namespace Game.Application;

public sealed class MainGameApplication(MainGameSession session, IMainSaveStore saves) : IMainGameApplication
{
    public MainGameSession Session { get; private set; } = session;

    public void ReplaceSession(MainGameSession value) => Session = value;

    public MainGameView GetView() => Session.GetView();
    public MainBattleView GetBattleView() => Session.GetBattleView();
    public MainCommandPreview PreviewCommand(MainCommandRequest request) => Session.PreviewCommand(request);
    public MainCommandResult IssueCommand(MainCommandRequest request) => Session.IssueCommand(request);
    public void EnterBattle(string orderId) => Session.EnterBattle(orderId);
    public IReadOnlyList<string> EndMonth(bool runAi = true) => Session.EndMonth(runAi);

    public IReadOnlyList<(string Id, string Name)> GetDiscoveredTools(string cityId) =>
        Session.GetView().Cities.Single(city => city.Id == cityId).DiscoveredToolIds
            .Select(id => (id, Session.GetToolName(id))).ToList();

    public IReadOnlyList<(string Id, string Name, int Distance)> GetTravelTargets(string cityId)
    {
        var view = Session.GetView();
        var city = view.Cities.Single(item => item.Id == cityId);
        return view.Cities.Where(item => item.Id != city.Id && item.Belong == city.Belong)
            .Select(item => (item.Id, item.Name, Route: Session.GetTravelRoute(city.Id, item.Id)))
            .Where(item => item.Route is not null).Select(item => (item.Id, item.Name, item.Route!.Distance)).ToList();
    }

    public IReadOnlyList<string> GetCommandTargetPersonIds(int commandId, string cityId) => Session.GetCommandTargetPersonIds(commandId, cityId);
    public MainSearchDiagnostics GetSearchDiagnostics(string cityId, string personId) => Session.GetSearchDiagnostics(cityId, personId);
    public string GetToolName(string toolId) => Session.GetToolName(toolId);

    public IReadOnlyList<(string Id, string Name)> GetBattleSkills(string personId) => CanOperateBattle()
        ? Session.GetBattleSkills(personId).Select(id => (id, Session.Content.Skills.Single(skill => skill.Id == id).Name)).ToList()
        : [];

    public IReadOnlyList<(int X, int Y)> GetBattleMovement(string personId) => CanOperateBattle()
        ? MainBattleService.GetReachableCells(Session.World, Session.Content, personId).Select(cell => (cell.X, cell.Y)).ToList()
        : [];

    public IReadOnlyList<(int X, int Y)> GetBattleRange(string personId, string? skillId) => CanOperateBattle()
        ? MainBattleService.GetActionRange(Session.World, Session.Content, personId, skillId)
        : [];

    public string? GetBattleAttackFailure(string personId, string? targetId) => CanOperateBattle()
        ? MainBattleService.AttackFailure(Session.World, Session.Content, personId, targetId)
        : "当前没有可操作的战斗。";

    public string? GetBattleSkillFailure(string personId, string? skillId, string? targetId) => CanOperateBattle()
        ? MainBattleService.SkillFailure(Session.World, Session.Content, personId, skillId, targetId)
        : "当前没有可操作的战斗。";

    public MainBattleActionResult MoveBattle(string personId, int x, int y) => Battle(
        () => MainBattleService.Move(Session.World, Session.Content, personId, x, y), "移动完成。", "目标格不可到达。");

    public MainBattleActionResult AttackBattle(string personId, string targetId) => Battle(
        () => MainBattleService.Attack(Session.World, Session.Content, personId, targetId), "攻击完成。", "目标不可攻击。");

    public MainBattleActionResult UseBattleSkill(string personId, string skillId, string targetId) => Battle(
        () => MainBattleService.UseSkill(Session.World, Session.Content, personId, skillId, targetId), "技能完成。", "技能目标或技力不符合条件。");

    public MainBattleActionResult WaitBattle(string personId) => Battle(
        () => MainBattleService.Wait(Session.World, personId), "已待机。", "该武将不能待机。");

    public MainBattleActionResult EndBattleTurn()
    {
        if (!CanOperateBattle()) return InvalidBattlePhase();
        MainBattleService.EndTurn(Session.World, Session.Content);
        return new MainBattleActionResult(true, "ok", "敌方行动完成，进入新的一天。");
    }

    public MainBattleActionResult RetreatBattle()
    {
        if (!CanOperateBattle()) return InvalidBattlePhase();
        MainBattleService.Retreat(Session.World);
        return new MainBattleActionResult(true, "ok", "已全军撤退。");
    }

    public MainBattleActionResult ResolveBattle()
    {
        if (!CanOperateBattle()) return InvalidBattlePhase();
        return new MainBattleActionResult(true, "ok", Session.ResolveBattle());
    }

    public IReadOnlyList<MainSaveSlotInfo> GetSaveSlots() => saves.List();
    public void SaveToSlot(int slot) => saves.Save(Session, slot);
    public void LoadFromSlot(int slot) => Session = saves.Load(slot);

    private MainBattleActionResult Battle(Func<bool> action, string success, string failure)
    {
        if (!CanOperateBattle()) return InvalidBattlePhase();
        return action() ? new MainBattleActionResult(true, "ok", success) : new MainBattleActionResult(false, "rejected", failure);
    }

    private bool CanOperateBattle() => Session.World.TurnPhase == MainTurnPhase.AwaitBattle && Session.World.ActiveBattle is not null;
    private static MainBattleActionResult InvalidBattlePhase() => new(false, "invalid_phase", "当前阶段不能执行战斗操作。");
}
