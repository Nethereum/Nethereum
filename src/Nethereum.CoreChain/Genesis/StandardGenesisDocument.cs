using System.Numerics;
using Newtonsoft.Json.Linq;

namespace Nethereum.CoreChain.Genesis
{
    public sealed class StandardGenesisForkConfig
    {
        public BigInteger? ChainId { get; set; }
        public long? HomesteadBlock { get; set; }
        public long? DaoForkBlock { get; set; }
        public long? Eip150Block { get; set; }
        public long? Eip155Block { get; set; }
        public long? Eip158Block { get; set; }
        public long? ByzantiumBlock { get; set; }
        public long? ConstantinopleBlock { get; set; }
        public long? PetersburgBlock { get; set; }
        public long? IstanbulBlock { get; set; }
        public long? MuirGlacierBlock { get; set; }
        public long? BerlinBlock { get; set; }
        public long? LondonBlock { get; set; }
        public long? ArrowGlacierBlock { get; set; }
        public long? GrayGlacierBlock { get; set; }
        public long? MergeNetsplitBlock { get; set; }
        public ulong? ShanghaiTime { get; set; }
        public ulong? CancunTime { get; set; }
        public ulong? PragueTime { get; set; }
        public ulong? OsakaTime { get; set; }
        public ulong? AmsterdamTime { get; set; }
        public BigInteger? TerminalTotalDifficulty { get; set; }
        public JObject BlobSchedule { get; set; }
        public string DepositContractAddress { get; set; }
    }

    public sealed class StandardGenesisDocument
    {
        public StandardGenesisForkConfig Config { get; set; } = new StandardGenesisForkConfig();
        public JObject Alloc { get; set; } = new JObject();
        public BigInteger GasLimit { get; set; }
        public BigInteger? BaseFeePerGas { get; set; }
        public long Timestamp { get; set; }
        public byte[] ExtraData { get; set; }
        public byte[] MixHash { get; set; }
        public byte[] Nonce { get; set; }
        public BigInteger Difficulty { get; set; }
        public string Coinbase { get; set; }
        public ulong? ExcessBlobGas { get; set; }
        public ulong? BlobGasUsed { get; set; }
        public ulong? SlotNumber { get; set; }
    }
}
