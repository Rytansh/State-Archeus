using System;

public static class VMEncoding
{
    public static int EncodeFloat(float value)
    {
        return BitConverter.SingleToInt32Bits(value);
    }

    public static float DecodeFloat(int value)
    {
        return BitConverter.Int32BitsToSingle(value);
    }
}
