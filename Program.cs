using System.Text;

partial class Whirlpool
{
    public byte[] Digest { get; } = new byte[DigestBytes];

    protected byte[] buffer = new byte[64];
    protected ulong[] hash = new ulong[8];

    public Whirlpool(string input = "")
    {
        Init(Encoding.ASCII.GetBytes(input));
    }

    protected virtual void ProcessBuffer()
    {
        ulong[]
            K = new ulong[8],
            L = new ulong[8],
            block = new ulong[8],
            state = new ulong[8];

        for (int i = 0; i < 8; ++i)
            for (int t = 0; t < 8; ++t)
                block[i] ^= (ulong)buffer[8 * i + t] << (56 - 8 * t);

        for (int i = 0; i < 8; ++i)
        {
            state[i] = block[i] ^ hash[i];
            K[i] = hash[i];
        }

        for (int r = 0; r < Rounds; ++r)
        {
            for (int i = 0; i < 8; ++i)
            {
                L[i] = 0;

                for (int t = 0; t < 8; ++t)
                    L[i] ^= C[t, (byte)(K[(i - t) & 7] >> (56 - 8 * t))];
            }

            L.CopyTo(K, 0);

            K[0] ^= Rc[r];

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
        int sourceBits = 8 * source.Length, bufferBits = 0, bufferPos = 0;

        if (source.Length > 0)
        {
            ComputeBitLength();
            Add();
        }

        Finalize();
        return;

        void ComputeBitLength()
        {
            int value = sourceBits, carry = 0;

            for (int i = 31; i >= 0; --i)
            {
                carry += (byte)value;
                bitLength[i] = (byte)carry;
                carry >>>= 8;
                value >>>= 8;

                if (value == 0)
                    break;
            }
        }

        void Add()
        {
            int sourceGap = (8 - (sourceBits & 7)) & 7, sourcePos = 0;
            byte b;

            while (sourceBits > 8)
            {
                b = (byte)(source[sourcePos] << sourceGap | source[sourcePos + 1] >> (8 - sourceGap));
                ++sourcePos;

                buffer[bufferPos] |= b;

                if (bufferBits + 8 == 512)
                {
                    ProcessBuffer();
                    bufferBits = bufferPos = 0;
                }
                else
                {
                    ++bufferPos;
                    bufferBits += 8;
                }

                buffer[bufferPos] = (byte)(b << 8);
                sourceBits -= 8;
            }

            if (sourceBits > 0)
            {
                b = (byte)(source[sourcePos] << sourceGap);
                buffer[bufferPos] |= b;
            }
            else
            {
                b = 0;
            }

            if (sourceBits < 8)
            {
                bufferBits += sourceBits;
            }
            else
            {
                if (bufferBits + 8 == 512)
                {
                    ProcessBuffer();
                    bufferBits = bufferPos = 0;
                }
                else
                {
                    ++bufferPos;
                    bufferBits += 8;
                }

                buffer[bufferPos] = (byte)(b << 8);
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