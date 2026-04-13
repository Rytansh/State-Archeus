public static class StableHash32
{
    private const uint OffsetBasis = 2166136261u;
    private const uint Prime = 16777619u;

    public static uint HashFromString(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        uint hash = OffsetBasis;

        for (int i = 0; i < text.Length; i++)
        {
            hash ^= text[i];
            hash *= Prime;
        }

        return hash;
    }

    public static uint CombineHash(uint a, uint b)
    {
        uint hash = OffsetBasis;
        hash ^= a;
        hash *= Prime;
        hash ^= b;
        hash *= Prime;
        return hash;
    }
}

