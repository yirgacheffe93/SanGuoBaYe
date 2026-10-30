using Game.Application;

namespace Game.Godot;

public sealed class MainStrategicPresenter(IMainGameApplication app)
{
    private string selectedCityId = "city-005";
    private string selectedPersonId = "person-089";

    public void Start()
    {
        var view = app.GetView();
        selectedCityId = view.Cities.First(city => city.Belong == view.PlayerRulerId).Id;
        selectedPersonId = view.Persons.FirstOrDefault(person => person.CityId == selectedCityId && person.Belong == view.PlayerRulerId)?.Id ?? "";
    }

    public void SelectCity(string cityId)
    {
        selectedCityId = cityId;
        selectedPersonId = "";
    }

    public string SelectedCityId => selectedCityId;
    public string SelectedPersonId => selectedPersonId;
    public void SelectPerson(string personId) => selectedPersonId = personId;
    public void SetSelection(string cityId, string personId) => (selectedCityId, selectedPersonId) = (cityId, personId);
    public bool HasActiveBattle() => app.GetView().HasActiveBattle;

    public MainStrategicRefresh Refresh(MainCommandInput input)
    {
        var view = app.GetView();
        selectedCityId = view.Cities.FirstOrDefault(city => city.Id == selectedCityId)?.Id ?? view.Cities[0].Id;
        var city = view.Cities.Single(city => city.Id == selectedCityId);
        var persons = view.Persons.Where(person => person.CityId == city.Id).ToList();
        if (persons.Count > 0 && persons.All(person => person.Id != selectedPersonId)) selectedPersonId = persons[0].Id;
        else if (persons.Count == 0) selectedPersonId = "";
        var person = persons.FirstOrDefault(item => item.Id == selectedPersonId);
        var preview = app.PreviewCommand(input.ToRequest(city.Id, person?.Id ?? ""));
        var hint = input.CommandId == MainCommandCatalog.SearchOrderId && person is not null
            ? app.GetSearchDiagnostics(city.Id, person.Id).Message
            : null;
        return new MainStrategicRefresh(view, city, person,
            city.Belong == view.PlayerRulerId ? "我方" : PersonName(view, city.Belong), PersonName(view, view.PlayerRulerId),
            app.GetDiscoveredTools(city.Id), app.GetTravelTargets(city.Id), app.GetCommandTargetPersonIds(input.CommandId, city.Id),
            preview, person is null ? "" : app.GetToolName(person.Tool1Id), hint);
    }

    public MainCommandResult Confirm(MainCommandInput input)
    {
        var result = app.IssueCommand(input.ToRequest(selectedCityId, selectedPersonId));
        if (result.Accepted && input.CommandId == MainCommandCatalog.BattleOrderId && result.OrderId is not null)
            app.EnterBattle(result.OrderId);
        return result;
    }

    public IReadOnlyList<string> EndMonth(bool runAi) => app.EndMonth(runAi);

    public static string OutcomeName(MainGameView view) => view.Outcome switch
    {
        Game.Domain.MainGameOutcome.Won => "胜利",
        Game.Domain.MainGameOutcome.Lost => "失败",
        _ => "进行中"
    };

    private static string PersonName(MainGameView view, string id) => view.Persons.FirstOrDefault(person => person.Id == id)?.Name ?? "无所属";
}

public sealed record MainCommandInput(int CommandId, string? TargetCityId, string? ToolId, MainTradeDirection TradeDirection,
    int Quantity, int Food, int Money, int Arms, string? TargetPersonId, List<string>? ParticipantIds)
{
    public MainCommandRequest ToRequest(string cityId, string personId) => new(CommandId, cityId, personId,
        TargetCityId, ToolId, TradeDirection, Quantity, Food, Money, Arms, TargetPersonId, ParticipantIds);
}

public sealed record MainStrategicRefresh(MainGameView View, MainCityView City, MainPersonView? Person,
    string OwnerName, string RulerName, IReadOnlyList<(string Id, string Name)> DiscoveredTools,
    IReadOnlyList<(string Id, string Name, int Distance)> TravelTargets, IReadOnlyList<string> TargetPersonIds,
    MainCommandPreview Preview, string ToolName, string? SearchHint);
