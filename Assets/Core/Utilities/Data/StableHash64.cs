
public static class StableHash64
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong HashFromString(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        ulong hash = OffsetBasis;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            // low byte
            hash ^= (byte)c;
            hash *= Prime;

            // high byte
            hash ^= (byte)(c >> 8);
            hash *= Prime;
        }

        return hash;
    }

    public static ulong CombineHash(ulong a, ulong b)
    {
        ulong hash = OffsetBasis;

        hash ^= a;
        hash *= Prime;

        hash ^= b;
        hash *= Prime;

        return hash;
    }

    public static ulong HashFromTwoStrings(string a, string b)
    {
        return CombineHash(HashFromString(a), HashFromString(b));
    }

    public static ulong HashFromBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return 0;

        ulong hash = OffsetBasis;

        for (int i = 0; i < bytes.Length; i++)
        {
            hash ^= bytes[i];
            hash *= Prime;
        }
        return hash;
    }
}

