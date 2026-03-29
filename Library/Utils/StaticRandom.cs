using System;

namespace OCB
{
    public static class StaticRandom
    {

        public static readonly Random rng = new Random();

        public static ulong DOUBLE2ULONG(double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return BitConverter.ToUInt64(bytes, 0);
        }

        public static double ULONG2DOUBLE(ulong value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return BitConverter.ToDouble(bytes, 0);
        }

        public static ulong RandomSeed()
        {
            var buffer = new byte[sizeof(ulong)];
            rng.NextBytes(buffer); // Fill in bits
            return BitConverter.ToUInt64(buffer, 0);
        }

        public static ulong HashSeed(ulong seed, float value)
        {
            seed += DOUBLE2ULONG(value);
            seed ^= seed << 12;
            seed ^= seed >> 25;
            seed ^= seed << 27;
            seed *= 0x2545F4914F6CDD1DUL;
            return seed;
        }

        public static void HashSeed(ref ulong seed, float value)
        {
            seed = HashSeed(seed, value);
        }

        public static float Range01(ulong seed)
        {
            return seed / (ulong.MaxValue);
        }

        public static float Range(float min, float max, ulong seed)
        {
            float range = Math.Abs(max - min);
            return Math.Min(min, max) +
                range * seed / (ulong.MaxValue);
        }

        public static float RangeSquare(float min, float max, ulong seed)
        {
            float range = Math.Abs(max - min);
            double rnd = (double)seed / (ulong.MaxValue) - 0.5;
            rnd = rnd * rnd * 2 * Math.Sign(rnd) + 0.5;
            return Math.Min(min, max) + range * (float)rnd;
        }

        public static int Range(int min, int max, ulong seed)
        {
            return (int)Math.Floor(Range(Math.Min(min, max),
                Math.Max(min, max) + 0.999999999f, seed));
        }

        public static int RangeSquare(int min, int max, ulong seed)
        {
            return (int)Math.Floor(RangeSquare(Math.Min(min, max),
                Math.Max(min, max) + 0.999999999f, seed));
        }

    }
}
