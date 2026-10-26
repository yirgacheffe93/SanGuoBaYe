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
    public static MainCommandPreview Preview(MainWorld world, int commandId, string cityId, string personId) =>
        Preview(world, new MainContent(), new MainCommandRequest(commandId, cityId, personId));

    public static MainCommandPreview Preview(MainWorld world, MainContent content, MainCommandRequest request)
    {
        var commandId = request.CommandId;
        var (moneyCost, thewCost) = MainCommandCatalog.Costs(commandId);
        var city = world.Cities.FirstOrDefault(item => item.Id == request.CityId);
        var person = world.Persons.FirstOrDefault(item => item.Id == request.PersonId);
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
        if (commandId == MainCommandCatalog.RewardOrderId)
        {
            if (request.ToolId is null || !city.ToolEntries.Any(entry => entry.ToolId == request.ToolId && entry.Discovered))
                return Preview(false, "tool_unavailable", "没有可赏赐的已发现道具。");
            if (person.Tool1Id.Length > 0 && person.Tool2Id.Length > 0) return Preview(false, "equipment_full", "装备槽已满。");
            var tool = content.Tools.FirstOrDefault(tool => tool.Id == request.ToolId);
            if (tool is not null && tool.Arms == 2 && person.Iq <= 105 || tool is not null && tool.Arms == 3 && person.Force <= 105)
                return Preview(false, "tool_unusable", "该人物无法使用此道具。");
        }
        if (commandId == MainCommandCatalog.ConfiscateOrderId && person.Tool1Id.Length == 0 && person.Tool2Id.Length == 0)
            return Preview(false, "no_equipment", "该人物没有可没收装备。");
        if (commandId == MainCommandCatalog.TradeOrderId)
        {
            if (request.Quantity <= 0) return Preview(false, "invalid_quantity", "交易数量必须大于零。");
            if (request.TradeDirection == MainTradeDirection.Buy && city.Money < request.Quantity * 5)
                return Preview(false, "money_low", "买入粮食的金钱不足。");
            if (request.TradeDirection == MainTradeDirection.Sell && city.Food < request.Quantity)
                return Preview(false, "food_low", "卖出粮食的库存不足。");
        }
        if (commandId is MainCommandCatalog.TransportOrderId or MainCommandCatalog.MoveOrderId)
        {
            var target = world.Cities.FirstOrDefault(item => item.Id == request.TargetCityId);
            if (target is null || target.Id == city.Id || target.Belong != city.Belong)
                return Preview(false, "invalid_city", "目标必须是不同的同归属城市。");
        }
        if (commandId == MainCommandCatalog.TransportOrderId)
        {
            if (request.Food < 0 || request.Money < 0 || request.Arms < 0 ||
                request.Food == 0 && request.Money == 0 && request.Arms == 0)
                return Preview(false, "invalid_quantity", "输送物资必须大于零。");
            if (city.Food < request.Food || city.Money < request.Money || city.MothballArms < request.Arms)
                return Preview(false, "resource_low", "输送数量超过当前库存。");
        }
        return Preview(true, "ok", "可执行");

        MainCommandPreview Preview(bool available, string code, string message) => new(
            available, code, message, commandId, moneyCost, thewCost,
            (city?.Money ?? 0) - moneyCost, (person?.Thew ?? 0) - thewCost);
    }

    public static MainCommandResult Issue(MainWorld world, int commandId, string cityId, string personId) =>
        Issue(world, new MainContent(), new MainCommandRequest(commandId, cityId, personId));

    public static MainCommandResult Issue(MainWorld world, MainContent content, MainCommandRequest request)
    {
        var preview = Preview(world, content, request);
        if (!preview.Available) return new MainCommandResult(false, preview.Code, preview.Message);
        return IssueInternal(world, content, request);
    }

    public static MainCommandResult IssueInternal(MainWorld world, int commandId, string cityId, string personId) =>
        IssueInternal(world, new MainContent(), new MainCommandRequest(commandId, cityId, personId));

    public static MainCommandResult IssueInternal(MainWorld world, MainContent content, MainCommandRequest request)
    {
        var city = world.Cities.Single(item => item.Id == request.CityId);
        var person = world.Persons.Single(item => item.Id == request.PersonId);
        var commandId = request.CommandId;
        var (moneyCost, thewCost) = MainCommandCatalog.Costs(commandId);
        if (commandId == MainCommandCatalog.RewardOrderId)
        {
            var specialEffect = MainPersonnelRules.Reward(world, content, city, person, request.ToolId!);
            return new MainCommandResult(true, "accepted", specialEffect);
        }
        if (commandId == MainCommandCatalog.ConfiscateOrderId)
        {
            var specialEffect = MainPersonnelRules.Confiscate(world, content, city, person);
            return new MainCommandResult(true, "accepted", specialEffect);
        }
        if (commandId == MainCommandCatalog.BanquetOrderId)
        {
            city.Money -= moneyCost;
            return new MainCommandResult(true, "accepted", MainPersonnelRules.Banquet(world, city, person));
        }

        string effect;
        if (commandId == MainCommandCatalog.TradeOrderId)
        {
            if (request.TradeDirection == MainTradeDirection.Buy)
            {
                city.Money -= request.Quantity * 5;
                city.Food += request.Quantity;
                effect = $"买入 {request.Quantity} 粮，月末归队。";
            }
            else
            {
                city.Food -= request.Quantity;
                city.Money = Math.Min(30000, city.Money + request.Quantity * 2);
                effect = $"卖出 {request.Quantity} 粮，月末归队。";
            }
        }
        else if (commandId == MainCommandCatalog.TransportOrderId)
        {
            city.Food -= request.Food;
            city.Money -= request.Money;
            city.MothballArms -= request.Arms;
            effect = "物资已发出，月末确定结果。";
        }
        else if (commandId == MainCommandCatalog.MoveOrderId)
        {
            effect = $"{person.Name}将在月末前往目标城市。";
        }
        else
        {
            effect = MainInternalRules.ApplyImmediateEffect(world, commandId, city, person);
        }
        var order = new MainOrder
        {
            Id = $"main.order.{world.NextSequence++}", OrderId = commandId, CityId = request.CityId, PersonId = request.PersonId,
            ObjectId = request.TargetCityId ?? "", Food = request.Food, Money = request.Money, Arms = request.Arms,
            Consume = thewCost, TimeCount = 0
        };
        city.Money -= moneyCost;
        person.Thew -= thewCost;
        person.OrderId = order.Id;
        person.CityId = "";
        city.PersonIds.Remove(person.Id);
        world.Orders.Add(order);
        return new MainCommandResult(true, "accepted", $"{person.Name}：{effect}", order.Id);
    }
}
