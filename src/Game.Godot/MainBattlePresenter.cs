using Game.Application;

namespace Game.Godot;

public sealed class MainBattlePresenter(IMainGameApplication app)
{
    public MainBattleView Refresh() => app.GetBattleView();
    public IReadOnlyList<(string Id, string Name)> Skills(string personId) => app.GetBattleSkills(personId);
    public IReadOnlyList<(int X, int Y)> Movement(string personId) => app.GetBattleMovement(personId);
    public IReadOnlyList<(int X, int Y)> Range(string personId, string? skillId) => app.GetBattleRange(personId, skillId);
    public string? AttackFailure(string personId, string? targetId) => app.GetBattleAttackFailure(personId, targetId);
    public string? SkillFailure(string personId, string? skillId, string? targetId) => app.GetBattleSkillFailure(personId, skillId, targetId);

    public MainBattleActionResult Handle(MainBattleUiAction action, string? personId, string? targetId, string? skillId, int x, int y) => action switch
    {
        MainBattleUiAction.Move when personId is not null => app.MoveBattle(personId, x, y),
        MainBattleUiAction.Attack when personId is not null && targetId is not null => app.AttackBattle(personId, targetId),
        MainBattleUiAction.Skill when personId is not null && targetId is not null && skillId is not null => app.UseBattleSkill(personId, skillId, targetId),
        MainBattleUiAction.Wait when personId is not null => app.WaitBattle(personId),
        MainBattleUiAction.EndTurn => app.EndBattleTurn(),
        MainBattleUiAction.Retreat => app.RetreatBattle(),
        MainBattleUiAction.Resolve => app.ResolveBattle(),
        _ => new MainBattleActionResult(false, "invalid_input", "战斗操作参数不完整。")
    };
}
