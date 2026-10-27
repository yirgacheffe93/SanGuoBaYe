using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain;

namespace Game.Application;

public sealed class MainGameSession
{
    public const int ReclaimOrderId = MainCommandCatalog.ReclaimOrderId;
    public const int CommerceOrderId = MainCommandCatalog.CommerceOrderId;
    public const int SearchOrderId = MainCommandCatalog.SearchOrderId;
    public const int GovernOrderId = MainCommandCatalog.GovernOrderId;
    public const int InspectOrderId = MainCommandCatalog.InspectOrderId;
    public const int ReclaimCost = MainCommandCatalog.ReclaimCost;
    public const int ReclaimThewCost = MainCommandCatalog.ReclaimThewCost;
    private static readonly JsonSerializerOptions CanonicalOptions = new() { WriteIndented = false };

    public MainGameSession(MainWorld world, MainContent content)
    {
        World = world ?? throw new ArgumentNullException(nameof(world));
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public MainWorld World { get; private set; }
    public MainContent Content { get; }

    public MainCommandPreview PreviewCommand(int commandId, string cityId, string personId) =>
        PreviewCommand(new MainCommandRequest(commandId, cityId, personId));

    public MainCommandPreview PreviewCommand(MainCommandRequest request) =>
        MainCommandService.Preview(World, Content, request);

    public MainCommandResult IssueCommand(int commandId, string cityId, string personId) =>
        IssueCommand(new MainCommandRequest(commandId, cityId, personId));

    public MainCommandResult IssueCommand(MainCommandRequest request) =>
        MainCommandService.Issue(World, Content, request);

    public MainCommandResult IssueCommandForRuler(MainCommandRequest request, string rulerId) =>
        MainCommandService.Issue(World, Content, request, rulerId);

    public MainCommandResult IssueReclaim(string cityId, string personId) => IssueCommand(ReclaimOrderId, cityId, personId);

    public MainCommandResult CancelOrder(string orderId) =>
        new(false, "not_supported", "main 命令接受后不能取消；请等待月末归队。");

    public IReadOnlyList<string> EndMonth(bool runAi = true)
    {
        if (World.Outcome != MainGameOutcome.InProgress) return [];
        MainFactionRules.UpdateSatraps(World);
        if (runAi) MainAiPlanner.Plan(World);
        var messages = MainOrderExecutor.Execute(World, Content).ToList();
        messages.AddRange(MainMonthService.Update(World, Content));
        var outcome = MainFactionRules.EvaluateOutcome(World);
        if (outcome != World.Outcome)
        {
            World.Outcome = outcome;
            var message = $"战局结束：{MainFactionRules.Name(outcome)}。";
            World.Reports.Add(new ReportEntry { Turn = World.Year * 12 + World.Month, Message = message });
            messages.Add(message);
        }
        return messages;
    }

    public IReadOnlyList<string> ExecuteOrders() => MainOrderExecutor.Execute(World, Content);

    public void ConditionUpdate() => MainMonthService.Update(World, Content);

    public MainGameView GetView() => MainViewFactory.Create(World);

    public MainTravelRoute? GetTravelRoute(string sourceId, string targetId) => MainTravelRules.Find(World, sourceId, targetId);

    public IReadOnlyList<string> GetCommandTargetPersonIds(int commandId, string cityId) => World.Persons.Where(person => commandId switch
    {
        MainCommandCatalog.RecruitOrderId => person.CityId == cityId && person.Status == MainPersonStatus.Captive,
        MainCommandCatalog.AlienateOrderId or MainCommandCatalog.CanvassOrderId => MainDiplomacyRules.IsEnemyOfficer(World, person),
        MainCommandCatalog.CounterespionageOrderId => MainDiplomacyRules.IsEnemySatrap(World, person),
        MainCommandCatalog.InduceOrderId => MainDiplomacyRules.IsEnemyRuler(World, person),
        _ => false
    }).Select(person => person.Id).ToList();

    public MainSearchDiagnostics GetSearchDiagnostics(string cityId, string personId)
    {
        var city = World.Cities.Single(city => city.Id == cityId);
        var person = World.Persons.Single(person => person.Id == personId);
        return MainSearchRules.Describe(World, Content, city, person);
    }

    public string GetToolName(string toolId) => string.IsNullOrEmpty(toolId) ? "无" :
        Content.Tools.FirstOrDefault(tool => tool.Id == toolId)?.Name ?? toolId;

    public string GetStateHash() => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(World, CanonicalOptions)))).ToLowerInvariant();

    public void Restore(MainWorld world) => World = world ?? throw new ArgumentNullException(nameof(world));

    public static string CommandName(int commandId) => MainCommandCatalog.Name(commandId);
}
