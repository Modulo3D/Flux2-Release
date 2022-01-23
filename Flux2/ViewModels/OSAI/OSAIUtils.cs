namespace Flux.ViewModels
{
    public static class OSAIUtils
    {
        public static bool IsBitSet(this ushort word, int bitIndex)
        {
            return (word & (1 << bitIndex)) != 0;
        }

        public static bool IsOnlyBitSet(this ushort word, int bitIndex)
        {
            var index = GetBitSet(word);
            return index == bitIndex;
        }

        public static short GetBitSet(this ushort word)
        {
            return word switch
            {
                0 => -1,
                1 => 0,
                2 => 1,
                4 => 2,
                8 => 3,
                16 => 4,
                32 => 5,
                64 => 6,
                128 => 7,
                256 => 8,
                512 => 9,
                1024 => 10,
                2048 => 11,
                4096 => 12,
                8192 => 13,
                16384 => 14,
                32768 => 15,
                _ => -1
            };
        }
    }
}
