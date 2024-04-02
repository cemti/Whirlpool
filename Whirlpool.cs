using System.Buffers.Binary;
using System.Text;

partial class Whirlpool
{
    private readonly byte[] _digest = new byte[DigestBytes];
    protected readonly byte[] _buffer = new byte[64];
    protected readonly ulong[] _hash = new ulong[8];

    public IReadOnlyList<byte> Digest => _digest.AsReadOnly();

    public Whirlpool(string input = "")
    {
        Init(Encoding.ASCII.GetBytes(input));
    }

    protected virtual void ProcessBuffer()
    {
        ulong[]
            k = new ulong[8],
            l = new ulong[8],
            block = new ulong[8],
            state = new ulong[8];

        for (int i = 0; i < 8; ++i)
            for (int t = 0; t < 8; ++t)
                block[i] ^= (ulong)_buffer[8 * i + t] << (56 - 8 * t);

        for (int i = 0; i < 8; ++i)
        {
            state[i] = block[i] ^ _hash[i];
            k[i] = _hash[i];
        }

        for (int r = 0; r < Rounds; ++r)
        {
            for (int i = 0; i < 8; ++i)
            {
                l[i] = 0;

                for (int t = 0; t < 8; ++t)
                    l[i] ^= C[t, (byte)(k[(i - t) & 7] >> (56 - 8 * t))];
            }

            l.CopyTo(k, 0);

            k[0] ^= Rc[r];

            for (int i = 0; i < 8; ++i)
            {
                l[i] = k[i];

                for (int t = 0; t < 8; ++t)
                    l[i] ^= C[t, (byte)(state[(i - t) & 7] >> (56 - 8 * t))];
            }

            l.CopyTo(state, 0);
        }

        for (int i = 0; i < 8; ++i)
            _hash[i] ^= state[i] ^ block[i];
    }

    protected virtual void Init(byte[] source)
    {
        int sourceBits = 8 * source.Length, bufferBits = 0, bufferPos = 0;

        if (source.Length > 0)
            Add();

        Finalize();
        return;

        void Add()
        {
            int sourceGap = (8 - (sourceBits & 7)) & 7, sourcePos = 0;
            byte b;

            void ProcessBufferIfFull()
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

                _buffer[bufferPos] = (byte)(b << 8);
            }

            while (sourceBits > 8)
            {
                b = (byte)(source[sourcePos] << sourceGap | source[sourcePos + 1] >> (8 - sourceGap));
                ++sourcePos;

                _buffer[bufferPos] |= b;

                ProcessBufferIfFull();
                sourceBits -= 8;
            }

            if (sourceBits > 0)
            {
                b = (byte)(source[sourcePos] << sourceGap);
                _buffer[bufferPos] |= b;

                if (sourceBits < 8)
                    bufferBits += sourceBits;
                else
                    ProcessBufferIfFull();
            }
        }

        void Finalize()
        {
            _buffer[bufferPos++] |= (byte)(0x80 >>> (bufferBits & 7));

            if (bufferPos > 32)
            {
                if (bufferPos < 64)
                    Array.Fill(_buffer, (byte)0, bufferPos, 64 - bufferPos);

                ProcessBuffer();
                bufferPos = 0;
            }

            if (bufferPos < 32)
                Array.Fill(_buffer, (byte)0, bufferPos, 32 - bufferPos);
            
            Array.Fill(_buffer, (byte)0, 32, 28);
            BinaryPrimitives.WriteInt32BigEndian(_buffer.AsSpan(32 + 28), 8 * source.Length);

            ProcessBuffer();

            for (int i = 0; i < 8; ++i)
                for (int t = 0; t < 8; ++t)
                    _digest[8 * i + t] = (byte)(_hash[i] >> (56 - 8 * t));
        }
    }
}