using Game.Domain;

namespace Game.Application;

public sealed record MainTravelRoute(int Distance, IReadOnlyList<string> CityIds);

public static class MainTravelRules
{
    public static MainTravelRoute? Find(MainWorld world, string sourceId, string targetId)
    {
        var source = world.Cities.Single(city => city.Id == sourceId);
        var target = world.Cities.Single(city => city.Id == targetId);
        if (source.Id == target.Id || target.Belong != source.Belong) return null;
        var visited = new HashSet<string>(StringComparer.Ordinal) { source.Id };
        var queue = new Queue<string>();
        queue.Enqueue(source.Id);
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var city = world.Cities.Single(item => item.Id == currentId);
            foreach (var nextId in city.Connections)
            {
                var next = world.Cities.Single(item => item.Id == nextId);
                if (next.Belong != source.Belong || !visited.Add(nextId)) continue;
                parents[nextId] = city.Id;
                if (nextId == target.Id)
                {
                    var path = new List<string> { target.Id };
                    while (path[0] != source.Id) path.Insert(0, parents[path[0]]);
                    var distance = Math.Abs(source.NativeX - target.NativeX) + Math.Abs(source.NativeY - target.NativeY);
                    return new MainTravelRoute(distance, path);
                }
                queue.Enqueue(nextId);
            }
        }
        return null;
    }
}
