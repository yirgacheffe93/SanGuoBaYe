namespace Game.Domain;

public static class MainRng
{
    public static uint Next(ref ulong state)
    {
        unchecked
        {
            var next = (uint)state * 1103515245u + 12345u;
            state = next;
            var value = next;
            value ^= value >> 11;
            value ^= (value << 7) & 0x9d2c5680u;
            value ^= (value << 15) & 0xefc60000u;
            value ^= value >> 18;
            return value >> 1;
        }
    }

    public static int NextInt(ref ulong state, int modulo)
    {
        if (modulo <= 0) throw new ArgumentOutOfRangeException(nameof(modulo));
        return (int)(Next(ref state) % (uint)modulo);
    }
}
