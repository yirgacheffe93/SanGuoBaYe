using Game.Domain;

namespace Game.Application;

public static class MainRandom
{
    public static int Next(MainWorld world, int modulo)
    {
        var state = world.RandomState;
        var result = MainRng.NextInt(ref state, modulo);
        world.RandomState = state;
        return result;
    }
}
