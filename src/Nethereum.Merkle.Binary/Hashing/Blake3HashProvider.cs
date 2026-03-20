using System;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Binary.Hashing
{
    public class Blake3HashProvider : IHashProvider
    {
        private readonly IBlake3Strategy _strategy;

        public Blake3HashProvider() : this(new ManagedBlake3Strategy()) { }

        public Blake3HashProvider(IBlake3Strategy strategy)
        {
            _strategy = strategy;
        }

        public byte[] ComputeHash(byte[] data)
        {
            return _strategy.ComputeHash(data);
        }
    }

    public interface IBlake3Strategy
    {
        byte[] ComputeHash(byte[] data);
    }

    public class ManagedBlake3Strategy : IBlake3Strategy
    {
        public byte[] ComputeHash(byte[] data)
        {
            return Blake3Managed.Hash(data ?? Array.Empty<byte>());
        }
    }

    internal static class Blake3Managed
    {
        private const int OUT_LEN = 32;
        private const int BLOCK_LEN = 64;
        private const int CHUNK_LEN = 1024;

        private const uint CHUNK_START = 1 << 0;
        private const uint CHUNK_END = 1 << 1;
        private const uint PARENT = 1 << 2;
        private const uint ROOT = 1 << 3;

        private static readonly uint[] IV =
        {
            0x6A09E667, 0xBB67AE85, 0x3C6EF372, 0xA54FF53A,
            0x510E527F, 0x9B05688C, 0x1F83D9AB, 0x5BE0CD19
        };

        private static readonly int[][] MSG_SCHEDULE =
        {
            new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
            new[] { 2, 6, 3, 10, 7, 0, 4, 13, 1, 11, 12, 5, 9, 14, 15, 8 },
            new[] { 3, 4, 10, 12, 13, 2, 7, 14, 6, 5, 9, 0, 11, 15, 8, 1 },
            new[] { 10, 7, 12, 9, 14, 3, 13, 15, 4, 0, 11, 2, 5, 8, 1, 6 },
            new[] { 12, 13, 9, 11, 15, 10, 14, 8, 7, 2, 5, 3, 0, 1, 6, 4 },
            new[] { 9, 14, 11, 5, 8, 12, 15, 1, 13, 3, 0, 10, 2, 6, 4, 7 },
            new[] { 11, 15, 5, 0, 1, 9, 8, 6, 14, 10, 2, 12, 3, 4, 7, 13 },
        };

        public static byte[] Hash(byte[] input)
        {
            var hasher = new Hasher();
            hasher.Update(input);
            return hasher.Finalize();
        }

        private static uint[] Compress(
            uint[] chainingValue, uint[] blockWords,
            ulong counter, uint blockLen, uint flags)
        {
            var state = new uint[16];
            state[0] = chainingValue[0];
            state[1] = chainingValue[1];
            state[2] = chainingValue[2];
            state[3] = chainingValue[3];
            state[4] = chainingValue[4];
            state[5] = chainingValue[5];
            state[6] = chainingValue[6];
            state[7] = chainingValue[7];
            state[8] = IV[0];
            state[9] = IV[1];
            state[10] = IV[2];
            state[11] = IV[3];
            state[12] = (uint)(counter);
            state[13] = (uint)(counter >> 32);
            state[14] = blockLen;
            state[15] = flags;

            for (int round = 0; round < 7; round++)
            {
                var s = MSG_SCHEDULE[round];
                G(state, 0, 4, 8, 12, blockWords[s[0]], blockWords[s[1]]);
                G(state, 1, 5, 9, 13, blockWords[s[2]], blockWords[s[3]]);
                G(state, 2, 6, 10, 14, blockWords[s[4]], blockWords[s[5]]);
                G(state, 3, 7, 11, 15, blockWords[s[6]], blockWords[s[7]]);
                G(state, 0, 5, 10, 15, blockWords[s[8]], blockWords[s[9]]);
                G(state, 1, 6, 11, 12, blockWords[s[10]], blockWords[s[11]]);
                G(state, 2, 7, 8, 13, blockWords[s[12]], blockWords[s[13]]);
                G(state, 3, 4, 9, 14, blockWords[s[14]], blockWords[s[15]]);
            }

            return state;
        }

        private static uint[] First8Words(uint[] compressOutput)
        {
            var cv = new uint[8];
            for (int i = 0; i < 8; i++)
                cv[i] = compressOutput[i] ^ compressOutput[i + 8];
            return cv;
        }

        private static void G(uint[] state, int a, int b, int c, int d, uint mx, uint my)
        {
            state[a] = state[a] + state[b] + mx;
            state[d] = RotR(state[d] ^ state[a], 16);
            state[c] = state[c] + state[d];
            state[b] = RotR(state[b] ^ state[c], 12);
            state[a] = state[a] + state[b] + my;
            state[d] = RotR(state[d] ^ state[a], 8);
            state[c] = state[c] + state[d];
            state[b] = RotR(state[b] ^ state[c], 7);
        }

        private static uint RotR(uint x, int n) => (x >> n) | (x << (32 - n));

        private static uint[] WordsFromBlock(byte[] block, int offset, int len)
        {
            var words = new uint[16];
            var buf = new byte[BLOCK_LEN];
            int copyLen = Math.Min(len, BLOCK_LEN);
            if (copyLen > 0)
                Array.Copy(block, offset, buf, 0, copyLen);
            for (int i = 0; i < 16; i++)
                words[i] = (uint)buf[i * 4]
                    | ((uint)buf[i * 4 + 1] << 8)
                    | ((uint)buf[i * 4 + 2] << 16)
                    | ((uint)buf[i * 4 + 3] << 24);
            return words;
        }

        private static byte[] CvToBytes(uint[] cv)
        {
            var result = new byte[OUT_LEN];
            for (int i = 0; i < 8; i++)
            {
                result[i * 4] = (byte)cv[i];
                result[i * 4 + 1] = (byte)(cv[i] >> 8);
                result[i * 4 + 2] = (byte)(cv[i] >> 16);
                result[i * 4 + 3] = (byte)(cv[i] >> 24);
            }
            return result;
        }

        private struct Output
        {
            public uint[] InputChainingValue;
            public uint[] BlockWords;
            public ulong Counter;
            public uint BlockLen;
            public uint Flags;

            public uint[] ChainingValue()
            {
                return First8Words(Compress(
                    InputChainingValue, BlockWords, Counter, BlockLen, Flags));
            }

            public byte[] RootOutputBytes()
            {
                var words = Compress(
                    InputChainingValue, BlockWords, 0, BlockLen, Flags | ROOT);
                return CvToBytes(First8Words(words));
            }
        }

        private struct ChunkState
        {
            public uint[] ChainingValue;
            public ulong ChunkCounter;
            public byte[] Block;
            public int BlockLen;
            public int BlocksCompressed;
            public uint Flags;

            public ChunkState(uint[] key, ulong chunkCounter, uint flags)
            {
                ChainingValue = new uint[8];
                Array.Copy(key, ChainingValue, 8);
                ChunkCounter = chunkCounter;
                Block = new byte[BLOCK_LEN];
                BlockLen = 0;
                BlocksCompressed = 0;
                Flags = flags;
            }

            public int Len => BLOCK_LEN * BlocksCompressed + BlockLen;

            private uint StartFlag()
            {
                return BlocksCompressed == 0 ? CHUNK_START : 0;
            }

            public void Update(byte[] input, int offset, int len)
            {
                int pos = 0;
                while (pos < len)
                {
                    if (BlockLen == BLOCK_LEN)
                    {
                        var blockWords = WordsFromBlock(Block, 0, BLOCK_LEN);
                        ChainingValue = First8Words(Compress(
                            ChainingValue, blockWords, ChunkCounter,
                            BLOCK_LEN, Flags | StartFlag()));
                        BlocksCompressed++;
                        Block = new byte[BLOCK_LEN];
                        BlockLen = 0;
                    }

                    int take = Math.Min(BLOCK_LEN - BlockLen, len - pos);
                    Array.Copy(input, offset + pos, Block, BlockLen, take);
                    BlockLen += take;
                    pos += take;
                }
            }

            public Output ToOutput()
            {
                var blockWords = WordsFromBlock(Block, 0, BlockLen);
                return new Output
                {
                    InputChainingValue = ChainingValue,
                    BlockWords = blockWords,
                    Counter = ChunkCounter,
                    BlockLen = (uint)BlockLen,
                    Flags = Flags | StartFlag() | CHUNK_END,
                };
            }
        }

        private static Output ParentOutput(uint[] leftCv, uint[] rightCv, uint[] key, uint flags)
        {
            var blockWords = new uint[16];
            Array.Copy(leftCv, 0, blockWords, 0, 8);
            Array.Copy(rightCv, 0, blockWords, 8, 8);
            return new Output
            {
                InputChainingValue = key,
                BlockWords = blockWords,
                Counter = 0,
                BlockLen = BLOCK_LEN,
                Flags = flags | PARENT,
            };
        }

        private static uint[] ParentCv(uint[] leftCv, uint[] rightCv, uint[] key, uint flags)
        {
            return ParentOutput(leftCv, rightCv, key, flags).ChainingValue();
        }

        private class Hasher
        {
            private ChunkState _chunkState;
            private readonly uint[] _key;
            private readonly uint[][] _cvStack;
            private int _cvStackLen;
            private readonly uint _flags;

            public Hasher()
            {
                _key = new uint[8];
                Array.Copy(IV, _key, 8);
                _chunkState = new ChunkState(_key, 0, 0);
                _cvStack = new uint[54][];
                _cvStackLen = 0;
                _flags = 0;
            }

            private void PushStack(uint[] cv)
            {
                _cvStack[_cvStackLen] = cv;
                _cvStackLen++;
            }

            private uint[] PopStack()
            {
                _cvStackLen--;
                return _cvStack[_cvStackLen];
            }

            private void AddChunkChainingValue(uint[] newCv, ulong totalChunks)
            {
                while ((totalChunks & 1) == 0)
                {
                    newCv = ParentCv(PopStack(), newCv, _key, _flags);
                    totalChunks >>= 1;
                }
                PushStack(newCv);
            }

            public void Update(byte[] input)
            {
                int pos = 0;
                while (pos < input.Length)
                {
                    if (_chunkState.Len == CHUNK_LEN)
                    {
                        var chunkCv = _chunkState.ToOutput().ChainingValue();
                        ulong totalChunks = _chunkState.ChunkCounter + 1;
                        AddChunkChainingValue(chunkCv, totalChunks);
                        _chunkState = new ChunkState(_key, totalChunks, _flags);
                    }

                    int take = Math.Min(CHUNK_LEN - _chunkState.Len, input.Length - pos);
                    _chunkState.Update(input, pos, take);
                    pos += take;
                }
            }

            public byte[] Finalize()
            {
                var output = _chunkState.ToOutput();
                int parentNodesRemaining = _cvStackLen;
                while (parentNodesRemaining > 0)
                {
                    parentNodesRemaining--;
                    output = ParentOutput(
                        _cvStack[parentNodesRemaining],
                        output.ChainingValue(),
                        _key, _flags);
                }
                return output.RootOutputBytes();
            }
        }
    }
}
