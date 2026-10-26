using Game.Domain;

namespace Game.Application;

public sealed record MainCommandResult(bool Accepted, string Code, string Message, string? OrderId = null);
public sealed record MainCommandPreview(
    bool Available,
    string Code,
    string Message,
    int CommandId,
    int MoneyCost,
    int ThewCost,
    int MoneyAfter,
    int ThewAfter);

public static class MainCommandService
{
    public static MainCommandPreview Preview(MainWorld world, int commandId, string cityId, string personId)
    {
        var (moneyCost, thewCost) = MainCommandCatalog.Costs(commandId);
        var city = world.Cities.FirstOrDefault(item => item.Id == cityId);
        var person = world.Persons.FirstOrDefault(item => item.Id == personId);
        if (city is null || person is null) return Preview(false, "not_found", "城市或武将不存在。");
        if (city.Belong != world.PlayerRulerId || person.Belong != world.PlayerRulerId || person.CityId != city.Id)
            return Preview(false, "invalid_target", "只能选择我方城中的己方武将。");
        if (person.OrderId is not null) return Preview(false, "person_busy", $"{person.Name} 已有命令。");
        if (person.Thew < thewCost) return Preview(false, "thew_low", $"体力不足，需要 {thewCost}。");
        if (city.Money < moneyCost) return Preview(false, "money_low", $"金钱不足，需要 {moneyCost}，当前 {city.Money}。");
        if (commandId == MainCommandCatalog.ReclaimOrderId && city.Farming >= city.FarmingLimit)
            return Preview(false, "farming_cap", "农业已达到上限。");
        if (commandId == MainCommandCatalog.CommerceOrderId && city.Commerce >= city.CommerceLimit)
            return Preview(false, "commerce_cap", "商业已达到上限。");
        if (commandId == MainCommandCatalog.GovernOrderId && city.State == 0 && city.AvoidCalamity >= 100)
            return Preview(false, "govern_unneeded", "城市正常且防灾已满。");
        return Preview(true, "ok", "可执行");

        MainCommandPreview Preview(bool available, string code, string message) => new(
            available, code, message, commandId, moneyCost, thewCost,
            (city?.Money ?? 0) - moneyCost, (person?.Thew ?? 0) - thewCost);
    }

    public static MainCommandResult Issue(MainWorld world, int commandId, string cityId, string personId)
    {
        var preview = Preview(world, commandId, cityId, personId);
        if (!preview.Available) return new MainCommandResult(false, preview.Code, preview.Message);
        return IssueInternal(world, commandId, cityId, personId);
    }

    public static MainCommandResult IssueInternal(MainWorld world, int commandId, string cityId, string personId)
    {
        var city = world.Cities.Single(item => item.Id == cityId);
        var person = world.Persons.Single(item => item.Id == personId);
        var (moneyCost, thewCost) = MainCommandCatalog.Costs(commandId);
        var effect = MainInternalRules.ApplyImmediateEffect(world, commandId, city, person);
        var order = new MainOrder
        {
            Id = $"main.order.{world.NextSequence++}", OrderId = commandId, CityId = cityId, PersonId = personId,
            Money = moneyCost, Consume = thewCost, TimeCount = 0
        };
        city.Money -= moneyCost;
        person.Thew -= thewCost;
        person.OrderId = order.Id;
        person.CityId = "";
        city.PersonIds.Remove(personId);
        world.Orders.Add(order);
        return new MainCommandResult(true, "accepted", $"{person.Name}：{effect}", order.Id);
    }
}
