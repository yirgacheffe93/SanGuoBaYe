using Game.Application;
using Game.Domain;
using Game.Godot;

namespace Game.Tests;

public sealed class MainPresenterTests
{
    [Fact]
    public void Strategic_presenter_refreshes_and_converts_input_using_only_contract()
    {
        var app = new FakeMainGameApplication();
        var presenter = new MainStrategicPresenter(app);
        presenter.SetSelection("city", "person");
        var participants = new List<string> { "person" };
        var input = new MainCommandInput(MainCommandCatalog.BattleOrderId, "target", "tool", MainTradeDirection.Sell,
            3, 4, 5, 6, "target-person", participants);
        var refreshed = presenter.Refresh(input);
        var preview = Assert.Single(app.Previews);
        AssertRequest(preview, participants);
        Assert.Equal("城市", refreshed.City.Name);
        Assert.Equal("武将", refreshed.Person!.Name);
        var result = presenter.Confirm(input);
        Assert.True(result.Accepted);
        var issued = Assert.Single(app.Issued);
        AssertRequest(issued, participants);
        Assert.Equal("order", app.EnteredBattle);
    }

    [Fact]
    public void Battle_presenter_translates_actions_and_rejects_incomplete_input()
    {
        var app = new FakeMainGameApplication();
        var presenter = new MainBattlePresenter(app);
        var moved = presenter.Handle(MainBattleUiAction.Move, "actor", null, null, 3, 4);
        Assert.True(moved.Success);
        Assert.Equal(("actor", 3, 4), app.Movement);
        var rejected = presenter.Handle(MainBattleUiAction.Attack, "actor", null, null, 0, 0);
        Assert.False(rejected.Success);
        Assert.Equal("invalid_input", rejected.Code);
    }

    [Fact]
    public void Save_controller_uses_slot_ids_and_structured_results()
    {
        var app = new FakeMainGameApplication();
        var controller = new MainSaveController(app);
        Assert.Equal(2, controller.LatestSlot());
        Assert.True(controller.Save(2).Success);
        Assert.Equal(2, app.SavedSlot);
        Assert.True(controller.Load(2).Success);
        Assert.Equal(2, app.LoadedSlot);
        app.LoadError = new IOException("bad save");
        var failed = controller.Load(2);
        Assert.False(failed.Success);
        Assert.Contains("bad save", failed.Message);
    }

    private static void AssertRequest(MainCommandRequest request, List<string> participants)
    {
        Assert.Equal(MainCommandCatalog.BattleOrderId, request.CommandId);
        Assert.Equal("city", request.CityId);
        Assert.Equal("person", request.PersonId);
        Assert.Equal("target", request.TargetCityId);
        Assert.Equal("tool", request.ToolId);
        Assert.Equal(MainTradeDirection.Sell, request.TradeDirection);
        Assert.Equal(3, request.Quantity);
        Assert.Equal(4, request.Food);
        Assert.Equal(5, request.Money);
        Assert.Equal(6, request.Arms);
        Assert.Equal("target-person", request.TargetPersonId);
        Assert.Same(participants, request.ParticipantIds);
    }

    private sealed class FakeMainGameApplication : IMainGameApplication
    {
        private readonly MainGameView view = new(1, 190, 1, "ruler", MainGameOutcome.InProgress, MainTurnPhase.Player, false,
            [new MainCityView("city", "城市", "ruler", 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], [])],
            [new MainPersonView("person", "武将", "city", "ruler", MainPersonStatus.Active, 1, 1, 1, 1, 100, 0, "tool", "", false)], [], []);
        public List<MainCommandRequest> Previews { get; } = [];
        public List<MainCommandRequest> Issued { get; } = [];
        public string? EnteredBattle { get; private set; }
        public (string PersonId, int X, int Y)? Movement { get; private set; }
        public int? SavedSlot { get; private set; }
        public int? LoadedSlot { get; private set; }
        public Exception? LoadError { get; set; }
        public MainGameView GetView() => view;
        public MainCommandPreview PreviewCommand(MainCommandRequest request)
        {
            Previews.Add(request);
            return new MainCommandPreview(true, "ok", "可执行", request.CommandId, 0, 0, 0, 0);
        }
        public MainCommandResult IssueCommand(MainCommandRequest request)
        {
            Issued.Add(request);
            return new MainCommandResult(true, "accepted", "接受", "order");
        }
        public void EnterBattle(string orderId) => EnteredBattle = orderId;
        public MainBattleActionResult MoveBattle(string personId, int x, int y)
        {
            Movement = (personId, x, y);
            return new MainBattleActionResult(true, "ok", "移动");
        }
        public IReadOnlyList<MainSaveSlotInfo> GetSaveSlots() =>
            [new MainSaveSlotInfo(2, true, true, DateTime.UtcNow, null, null), new MainSaveSlotInfo(1, true, true, DateTime.UtcNow.AddDays(-1), null, null)];
        public void SaveToSlot(int slot) => SavedSlot = slot;
        public void LoadFromSlot(int slot)
        {
            if (LoadError is not null) throw LoadError;
            LoadedSlot = slot;
        }
        public IReadOnlyList<(string Id, string Name)> GetDiscoveredTools(string cityId) => [];
        public IReadOnlyList<(string Id, string Name, int Distance)> GetTravelTargets(string cityId) => [];
        public IReadOnlyList<string> GetCommandTargetPersonIds(int commandId, string cityId) => [];
        public string GetToolName(string toolId) => toolId;
        public MainBattleView GetBattleView() => throw new NotImplementedException();
        public IReadOnlyList<string> EndMonth(bool runAi = true) => [];
        public MainSearchDiagnostics GetSearchDiagnostics(string cityId, string personId) => new(0, 0, "");
        public IReadOnlyList<(string Id, string Name)> GetBattleSkills(string personId) => [];
        public IReadOnlyList<(int X, int Y)> GetBattleMovement(string personId) => [];
        public IReadOnlyList<(int X, int Y)> GetBattleRange(string personId, string? skillId) => [];
        public string? GetBattleAttackFailure(string personId, string? targetId) => null;
        public string? GetBattleSkillFailure(string personId, string? skillId, string? targetId) => null;
        public MainBattleActionResult AttackBattle(string personId, string targetId) => throw new NotImplementedException();
        public MainBattleActionResult UseBattleSkill(string personId, string skillId, string targetId) => throw new NotImplementedException();
        public MainBattleActionResult WaitBattle(string personId) => throw new NotImplementedException();
        public MainBattleActionResult EndBattleTurn() => throw new NotImplementedException();
        public MainBattleActionResult RetreatBattle() => throw new NotImplementedException();
        public MainBattleActionResult ResolveBattle() => throw new NotImplementedException();
    }
}
