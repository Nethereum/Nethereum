using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    public class BlockchainTestVectors
    {
        public class TestCase
        {
            public string Name { get; set; } = "";
            public string Network { get; set; } = "";
            public BigInteger ChainId { get; set; }
            public GenesisHeader Genesis { get; set; } = new();
            public BlockData Block { get; set; } = new();
            public Dictionary<string, AccountState> PreState { get; set; } = new();
            public Dictionary<string, AccountState> PostState { get; set; } = new();
            public byte[] ExpectedLastBlockHash { get; set; } = Array.Empty<byte>();
        }

        public class GenesisHeader
        {
            public byte[] Hash { get; set; } = Array.Empty<byte>();
            public byte[] ParentHash { get; set; } = Array.Empty<byte>();
            public byte[] StateRoot { get; set; } = Array.Empty<byte>();
            public byte[] TransactionsRoot { get; set; } = Array.Empty<byte>();
            public byte[] ReceiptsRoot { get; set; } = Array.Empty<byte>();
            public byte[] Coinbase { get; set; } = Array.Empty<byte>();
            public BigInteger Difficulty { get; set; }
            public BigInteger Number { get; set; }
            public BigInteger GasLimit { get; set; }
            public BigInteger GasUsed { get; set; }
            public BigInteger Timestamp { get; set; }
            public BigInteger BaseFee { get; set; }
            public byte[] ExtraData { get; set; } = Array.Empty<byte>();
            public byte[] MixHash { get; set; } = Array.Empty<byte>();
            public byte[] Nonce { get; set; } = Array.Empty<byte>();
            public byte[] UncleHash { get; set; } = Array.Empty<byte>();
            public byte[] LogsBloom { get; set; } = Array.Empty<byte>();
            public byte[] WithdrawalsRoot { get; set; } = Array.Empty<byte>();
        }

        public class BlockData
        {
            public BlockHeader Header { get; set; } = new();
            public List<TransactionData> Transactions { get; set; } = new();
            public byte[] Rlp { get; set; } = Array.Empty<byte>();
        }

        public class BlockHeader
        {
            public byte[] Hash { get; set; } = Array.Empty<byte>();
            public byte[] ParentHash { get; set; } = Array.Empty<byte>();
            public byte[] StateRoot { get; set; } = Array.Empty<byte>();
            public byte[] TransactionsRoot { get; set; } = Array.Empty<byte>();
            public byte[] ReceiptsRoot { get; set; } = Array.Empty<byte>();
            public byte[] Coinbase { get; set; } = Array.Empty<byte>();
            public BigInteger Difficulty { get; set; }
            public BigInteger Number { get; set; }
            public BigInteger GasLimit { get; set; }
            public BigInteger GasUsed { get; set; }
            public BigInteger Timestamp { get; set; }
            public BigInteger BaseFee { get; set; }
            public byte[] ExtraData { get; set; } = Array.Empty<byte>();
            public byte[] MixHash { get; set; } = Array.Empty<byte>();
            public byte[] Nonce { get; set; } = Array.Empty<byte>();
            public byte[] UncleHash { get; set; } = Array.Empty<byte>();
            public byte[] LogsBloom { get; set; } = Array.Empty<byte>();
            public byte[] WithdrawalsRoot { get; set; } = Array.Empty<byte>();
        }

        public class TransactionData
        {
            public byte[] Data { get; set; } = Array.Empty<byte>();
            public BigInteger GasLimit { get; set; }
            public BigInteger GasPrice { get; set; }
            public BigInteger Nonce { get; set; }
            public string? To { get; set; }
            public BigInteger Value { get; set; }
            public byte[] R { get; set; } = Array.Empty<byte>();
            public byte[] S { get; set; } = Array.Empty<byte>();
            public BigInteger V { get; set; }
            public string Sender { get; set; } = "";
        }

        public class AccountState
        {
            public BigInteger Balance { get; set; }
            public BigInteger Nonce { get; set; }
            public byte[] Code { get; set; } = Array.Empty<byte>();
            public Dictionary<BigInteger, BigInteger> Storage { get; set; } = new();
        }

        public static TestCase GetShanghaiCancunTestCase()
        {
            return new TestCase
            {
                Name = "shanghaiExample_Cancun",
                Network = "Cancun",
                ChainId = 1,
                Genesis = new GenesisHeader
                {
                    Hash = "0x286a26a6c05ea12f11b541486c5eb8ef0a36ce29b61e86f2a98886a3886b202c".HexToByteArray(),
                    ParentHash = new byte[32],
                    StateRoot = "0xc9f38211bd47d18248e2bd461131b4b454dde6dd63ab70d57e157d2fe058b342".HexToByteArray(),
                    TransactionsRoot = "0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
                    ReceiptsRoot = "0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
                    Coinbase = "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba".HexToByteArray(),
                    Difficulty = 0,
                    Number = 0,
                    GasLimit = BigInteger.Parse("9223372036854775807"),
                    GasUsed = 0,
                    Timestamp = 0x03b6,
                    BaseFee = 10,
                    ExtraData = "0x42".HexToByteArray(),
                    MixHash = "0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
                    Nonce = new byte[8],
                    UncleHash = "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray(),
                    LogsBloom = new byte[256],
                    WithdrawalsRoot = "0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray()
                },
                Block = new BlockData
                {
                    Header = new BlockHeader
                    {
                        Hash = "0x644dd6bb4cfe4af99adde4001986e8b7245ad70d93231a9629cf0cbab586a7e0".HexToByteArray(),
                        ParentHash = "0x286a26a6c05ea12f11b541486c5eb8ef0a36ce29b61e86f2a98886a3886b202c".HexToByteArray(),
                        StateRoot = "0xa328ab2b4b2e0195194262a116e904f804eef0d336b8114fc4106925e0326ffd".HexToByteArray(),
                        TransactionsRoot = "0x71e515dd89e8a7973402c2e11646081b4e2209b2d3a1550df5095289dabcb3fb".HexToByteArray(),
                        ReceiptsRoot = "0xed9c51ea52c968e552e370a77a41dac98606e98b915092fb5f949d6452fce1c4".HexToByteArray(),
                        Coinbase = "0x2adc25665018aa1fe0e6bc666dac8fc2697ff9ba".HexToByteArray(),
                        Difficulty = 0,
                        Number = 1,
                        GasLimit = BigInteger.Parse("9223372036854775807"),
                        GasUsed = 0x0125b8,
                        Timestamp = 0x079e,
                        BaseFee = 9,
                        ExtraData = "0x42".HexToByteArray(),
                        MixHash = "0x56e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b421".HexToByteArray(),
                        Nonce = new byte[8],
                        UncleHash = "0x1dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347".HexToByteArray(),
                        LogsBloom = new byte[256],
                        WithdrawalsRoot = "0x27f166f1d7c789251299535cb176ba34116e44894476a7886fe5d73d9be5c973".HexToByteArray()
                    },
                    Transactions = new List<TransactionData>
                    {
                        new TransactionData
                        {
                            Data = "0x600160015500".HexToByteArray(),
                            GasLimit = 0x061a80,
                            GasPrice = 0x28,
                            Nonce = 0,
                            To = null,
                            Value = 0,
                            R = "0x0b46eb2e2c914b99416e723a37be923605238a81c83c25b5f842544bebea8816".HexToByteArray(),
                            S = "0x65730cb3fb806bd5260c1db09198459a2a2499e51b43a3780b48c1a3594133f2".HexToByteArray(),
                            V = 0x1b,
                            Sender = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b"
                        }
                    },
                    Rlp = "0xf902b5f9023fa0286a26a6c05ea12f11b541486c5eb8ef0a36ce29b61e86f2a98886a3886b202ca01dcc4de8dec75d7aab85b567b6ccd41ad312451b948a7413f0a142fd40d49347942adc25665018aa1fe0e6bc666dac8fc2697ff9baa0a328ab2b4b2e0195194262a116e904f804eef0d336b8114fc4106925e0326ffda071e515dd89e8a7973402c2e11646081b4e2209b2d3a1550df5095289dabcb3fba0ed9c51ea52c968e552e370a77a41dac98606e98b915092fb5f949d6452fce1c4b90100000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000008001887fffffffffffffff830125b882079e42a056e81f171bcc55a6ff8345e692c0f86e5b48e01b996cadc001622fb5e363b42188000000000000000009a027f166f1d7c789251299535cb176ba34116e44894476a7886fe5d73d9be5c9738080a00000000000000000000000000000000000000000000000000000000000000000f854f852802883061a808080866001600155001ba00b46eb2e2c914b99416e723a37be923605238a81c83c25b5f842544bebea8816a065730cb3fb806bd5260c1db09198459a2a2499e51b43a3780b48c1a3594133f2c0dbda808094c94f5374fce5edbc8e2a8697c15331677e6ebf0b822710".HexToByteArray()
                },
                PreState = new Dictionary<string, AccountState>
                {
                    ["0x000f3df6d732807ef1319fb7b8bb8522d0beac02"] = new AccountState
                    {
                        Balance = 0,
                        Nonce = 1,
                        Code = "0x3373fffffffffffffffffffffffffffffffffffffffe14604d57602036146024575f5ffd5b5f35801560495762001fff810690815414603c575f5ffd5b62001fff01545f5260205ff35b5f5ffd5b62001fff42064281555f359062001fff015500".HexToByteArray(),
                        Storage = new Dictionary<BigInteger, BigInteger>
                        {
                            [0x03b6] = 0x03b6
                        }
                    },
                    ["0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b"] = new AccountState
                    {
                        Balance = BigInteger.Parse("100000000000000000000"),
                        Nonce = 0,
                        Code = Array.Empty<byte>(),
                        Storage = new Dictionary<BigInteger, BigInteger>()
                    }
                },
                ExpectedLastBlockHash = "0x644dd6bb4cfe4af99adde4001986e8b7245ad70d93231a9629cf0cbab586a7e0".HexToByteArray()
            };
        }

        public static IEnumerable<object[]> GetTestCases()
        {
            yield return new object[] { GetShanghaiCancunTestCase() };
        }
    }
}
