using Game.Domain;

namespace Game.Application;

public static class MainPersonnelRules
{
    public static string Reward(MainWorld world, MainContent content, MainCity city, MainPerson person, string toolId)
    {
        var tool = content.Tools.Single(item => item.Id == toolId);
        if (person.Tool1Id.Length > 0 && person.Tool2Id.Length > 0 || !CanUse(tool, person)) return $"{person.Name}无法使用{tool.Name}。";
        person.Force = (person.Force + tool.Attack) & 0xff;
        person.Iq = (person.Iq + tool.Iq) & 0xff;
        ApplyArmsType(tool, person);
        if (tool.UseFlag == 0)
        {
            if (person.Tool1Id.Length == 0) person.Tool1Id = toolId;
            else person.Tool2Id = toolId;
            SyncEquipment(person);
        }
        if (person.Id != world.PlayerRulerId) person.Devotion = Math.Min(100, person.Devotion + 8);
        var entryIndex = city.ToolEntries.FindIndex(entry => entry.ToolId == toolId && entry.Discovered);
        if (entryIndex >= 0) city.ToolEntries.RemoveAt(entryIndex);
        return $"{person.Name}获赐{tool.Name}，武力/智力提升。";
    }

    public static string Confiscate(MainWorld world, MainContent content, MainCity city, MainPerson person)
    {
        var toolId = person.Tool1Id.Length > 0 ? person.Tool1Id : person.Tool2Id;
        var tool = content.Tools.Single(item => item.Id == toolId);
        person.Force = (person.Force - tool.Attack) & 0xff;
        person.Iq = (person.Iq - tool.Iq) & 0xff;
        if (person.Tool1Id.Length > 0) person.Tool1Id = person.Tool2Id;
        person.Tool2Id = "";
        SyncEquipment(person);
        if (person.Id != world.PlayerRulerId) person.Devotion = Math.Max(0, person.Devotion - 20);
        city.ToolEntries.Add(new MainCityToolEntry { ToolId = toolId, Discovered = true });
        return $"已没收{person.Name}的{tool.Name}。";
    }

    public static string Banquet(MainWorld world, MainCity city, MainPerson person)
    {
        person.Thew = Math.Min(100, person.Thew + 50);
        if (person.Id != world.PlayerRulerId && person.Devotion < 100) person.Devotion++;
        return $"{person.Name}宴请完成，体力恢复。";
    }

    private static bool CanUse(MainTool tool, MainPerson person) => tool.Arms switch
    {
        2 => person.Iq > 105,
        3 => person.Force > 105,
        _ => true
    };

    private static void ApplyArmsType(MainTool tool, MainPerson person)
    {
        if (tool.UseFlag == 0) return;
        person.ArmsType = tool.Arms switch
        {
            1 => 3,
            2 => 5,
            3 => 4,
            _ => tool.Arms - 4
        };
    }

    private static void SyncEquipment(MainPerson person)
    {
        person.EquipIds = [person.Tool1Id, person.Tool2Id];
    }
}
