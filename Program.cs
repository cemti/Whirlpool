using System.Text;

class Whirlpool
{
    public static readonly int DigestBits = 512;
    public static readonly int DigestBytes = DigestBits >>> 3;

    public byte[] Digest { get; } = new byte[DigestBytes];

    protected static readonly int Rounds = 10;

    private static readonly byte[] sbox =
    [
        0x18, 0x23, 0xC6, 0xE8, 0x87, 0xB8, 0x01, 0x4F, 0x36, 0xA6, 0xD2, 0xF5, 0x79, 0x6F, 0x91, 0x52,
            0x60, 0xBC, 0x9B, 0x8E, 0xA3, 0x0C, 0x7B, 0x35, 0x1D, 0xE0, 0xD7, 0xC2, 0x2E, 0x4B, 0xFE, 0x57,
            0x15, 0x77, 0x37, 0xE5, 0x9F, 0xF0, 0x4A, 0xDA, 0x58, 0xC9, 0x29, 0x0A, 0xB1, 0xA0, 0x6B, 0x85,
            0xBD, 0x5D, 0x10, 0xF4, 0xCB, 0x3E, 0x05, 0x67, 0xE4, 0x27, 0x41, 0x8B, 0xA7, 0x7D, 0x95, 0xD8,
            0xFB, 0xEE, 0x7C, 0x66, 0xDD, 0x17, 0x47, 0x9E, 0xCA, 0x2D, 0xBF, 0x07, 0xAD, 0x5A, 0x83, 0x33,
            0x63, 0x02, 0xAA, 0x71, 0xC8, 0x19, 0x49, 0xD9, 0xF2, 0xE3, 0x5B, 0x88, 0x9A, 0x26, 0x32, 0xB0,
            0xE9, 0x0F, 0xD5, 0x80, 0xBE, 0xCD, 0x34, 0x48, 0xFF, 0x7A, 0x90, 0x5F, 0x20, 0x68, 0x1A, 0xAE,
            0xB4, 0x54, 0x93, 0x22, 0x64, 0xF1, 0x73, 0x12, 0x40, 0x08, 0xC3, 0xEC, 0xDB, 0xA1, 0x8D, 0x3D,
            0x97, 0x00, 0xCF, 0x2B, 0x76, 0x82, 0xD6, 0x1B, 0xB5, 0xAF, 0x6A, 0x50, 0x45, 0xF3, 0x30, 0xEF,
            0x3F, 0x55, 0xA2, 0xEA, 0x65, 0xBA, 0x2F, 0xC0, 0xDE, 0x1C, 0xFD, 0x4D, 0x92, 0x75, 0x06, 0x8A,
            0xB2, 0xE6, 0x0E, 0x1F, 0x62, 0xD4, 0xA8, 0x96, 0xF9, 0xC5, 0x25, 0x59, 0x84, 0x72, 0x39, 0x4C,
            0x5E, 0x78, 0x38, 0x8C, 0xD1, 0xA5, 0xE2, 0x61, 0xB3, 0x21, 0x9C, 0x1E, 0x43, 0xC7, 0xFC, 0x04,
            0x51, 0x99, 0x6D, 0x0D, 0xFA, 0xDF, 0x7E, 0x24, 0x3B, 0xAB, 0xCE, 0x11, 0x8F, 0x4E, 0xB7, 0xEB,
            0x3C, 0x81, 0x94, 0xF7, 0xB9, 0x13, 0x2C, 0xD3, 0xE7, 0x6E, 0xC4, 0x03, 0x56, 0x44, 0x7F, 0xA9,
            0x2A, 0xBB, 0xC1, 0x53, 0xDC, 0x0B, 0x9D, 0x6C, 0x31, 0x74, 0xF6, 0x46, 0xAC, 0x89, 0x14, 0xE1,
            0x16, 0x3A, 0x69, 0x09, 0x70, 0xB6, 0xD0, 0xED, 0xCC, 0x42, 0x98, 0xA4, 0x28, 0x5C, 0xF8, 0x86
    ];

    private static readonly ulong[,] C = new ulong[8, 256];
    private static readonly ulong[] rc = new ulong[Rounds + 1];

    protected byte[] buffer = new byte[64];
    protected ulong[] hash = new ulong[8];

    public Whirlpool(string input = "")
    {
        Init(Encoding.ASCII.GetBytes(input));
    }

    static Whirlpool()
    {
        static ulong maskWithReductionPolynomial(ulong v)
        {
            v <<= 1;
            return v >= 0x100 ? v ^ 0x11d : v;
        }

        for (int x = 0; x < sbox.Length; ++x)
        {
            ulong
                v1 = sbox[x],
                v2 = maskWithReductionPolynomial(v1),
                v4 = maskWithReductionPolynomial(v2),
                v5 = v4 ^ v1,
                v8 = maskWithReductionPolynomial(v4),
                v9 = v8 ^ v1;

            C[0, x] = v1 << 56 | v1 << 48 | v4 << 40 | v1 << 32 | v8 << 24 | v5 << 16 | v2 << 8 | v9;

            for (int t = 1; t < 8; ++t)
                C[t, x] = (C[t - 1, x] >> 8) | (C[t - 1, x] << 56);
        }

        for (int r = 0; r < Rounds; ++r)
            for (int t = 0; t < 8; ++t)
                rc[r + 1] ^= C[t, 8 * r + t] & (0xff00000000000000 >> (8 * t));
    }

    protected virtual void ProcessBuffer()
    {
        ulong[]
            K = new ulong[8],
            L = new ulong[8],
            block = new ulong[8],
            state = new ulong[8];

        for (int i = 0, j = 0; i < 8; ++i, j += 8)
            for (int t = 0; t < 8; ++t)
                block[i] ^= (ulong)buffer[j + t] << (56 - 8 * t);

        for (int i = 0; i < 8; ++i)
        {
            state[i] = block[i] ^ hash[i];
            K[i] = hash[i];
        }

        for (int r = 1; r <= Rounds; ++r)
        {
            for (int i = 0; i < 8; ++i)
            {
                L[i] = 0;

                for (int t = 0; t < 8; ++t)
                    L[i] ^= C[t, (byte)(K[(i - t) & 7] >> (56 - 8 * t))];
            }

            L.CopyTo(K, 0);

            K[0] ^= rc[r];

            for (int i = 0; i < 8; ++i)
            {
                L[i] = K[i];

                for (int t = 0; t < 8; ++t)
                    L[i] ^= C[t, (byte)(state[(i - t) & 7] >> (56 - 8 * t))];
            }

            L.CopyTo(state, 0);
        }

        for (int i = 0; i < 8; ++i)
            hash[i] ^= state[i] ^ block[i];
    }

    protected virtual void Init(byte[] source)
    {
        byte[] bitLength = new byte[32];
        int bufferBits = 0, bufferPos = 0;

        if (source.Length > 0)
            Add();

        Finalize();

        void Add()
        {
            int
                sourceBits = 8 * source.Length,
                sourcePos = 0,
                bufferRem = 0,
                sourceGap = (8 - (sourceBits & 7)) & 7;

            uint b;
            ulong value = (ulong)sourceBits;

            for (int i = 31, carry = 0; i >= 0; --i)
            {
                carry += (byte)value;
                bitLength[i] = (byte)carry;
                carry >>>= 8;
                value >>>= 8;

                if (value == 0)
                    break;
            }

            while (sourceBits > 8)
            {
                b = (uint)((byte)(source[sourcePos] << sourceGap) | source[sourcePos + 1] >> (8 - sourceGap));
                ++sourcePos;

                buffer[bufferPos++] |= (byte)(b >> bufferRem);
                bufferBits += 8 - bufferRem;

                if (bufferBits == 512)
                {
                    ProcessBuffer();
                    bufferBits = bufferPos = 0;
                }

                buffer[bufferPos] = (byte)(b << (8 - bufferRem));
                bufferBits += bufferRem;
                sourceBits -= 8;
            }

            if (sourceBits > 0)
            {
                b = (byte)(source[sourcePos] << sourceGap);
                buffer[bufferPos] |= (byte)(b >> bufferRem);
            }
            else
            {
                b = 0;
            }

            if (bufferRem + sourceBits < 8)
            {
                bufferBits += sourceBits;
            }
            else
            {
                ++bufferPos;
                bufferBits += 8 - bufferRem;
                sourceBits -= 8 - bufferRem;

                if (bufferBits == 512)
                {
                    ProcessBuffer();
                    bufferBits = bufferPos = 0;
                }

                buffer[bufferPos] = (byte)(b << (8 - bufferRem));
                bufferBits += sourceBits;
            }
        }

        void Finalize()
        {
            buffer[bufferPos++] |= (byte)(0x80 >>> (bufferBits & 7));

            if (bufferPos > 32)
            {
                if (bufferPos < 64)
                    Array.Fill(buffer, (byte)0, bufferPos, 64 - bufferPos);

                ProcessBuffer();
                bufferPos = 0;
            }

            if (bufferPos < 32)
                Array.Fill(buffer, (byte)0, bufferPos, 32 - bufferPos);

            bitLength.CopyTo(buffer, 32);

            ProcessBuffer();

            for (int i = 0; i < 8; ++i)
                for (int t = 0; t < 8; ++t)
                    Digest[8 * i + t] = (byte)(hash[i] >> (56 - 8 * t));
        }
    }

    public static void Main()
    {
        string input = Console.ReadLine()!;
        byte[] digest = new Whirlpool(input).Digest;
        string hex = BitConverter.ToString(digest).Replace("-", "");
        Console.WriteLine(hex);
    }
}