using System.Numerics;
using Nethereum.CoreChain.Genesis;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json.Linq;

namespace Nethereum.EEST.ConformanceRunner
{
    public static class EestGenesisBuilder
    {
        public static StandardGenesisDocument BuildGenesisDocument(BlockchainTestLoader.BlockchainTest test)
        {
            var genesisJson = new JObject
            {
                ["config"] = new JObject { ["chainId"] = test.ChainId.ToString() },
                ["alloc"] = BuildAllocJson(test),
                ["gasLimit"] = ((BigInteger)test.GenesisBlockHeader.GasLimit).ToString(),
                ["timestamp"] = ((BigInteger)test.GenesisBlockHeader.Timestamp).ToString(),
                ["extraData"] = "0x" + test.GenesisBlockHeader.ExtraData.ToHex(),
                ["mixHash"] = "0x" + test.GenesisBlockHeader.MixHash.ToHex(),
                ["nonce"] = "0x" + test.GenesisBlockHeader.Nonce.ToHex(),
                ["difficulty"] = test.GenesisBlockHeader.Difficulty.ToString(),
                ["coinbase"] = "0x" + test.GenesisBlockHeader.Coinbase.ToHex(),
            };

            if (test.GenesisBlockHeader.BaseFee.HasValue)
                genesisJson["baseFeePerGas"] = test.GenesisBlockHeader.BaseFee.Value.ToString();
            if (test.GenesisBlockHeader.ExcessBlobGas.HasValue)
                genesisJson["excessBlobGas"] = test.GenesisBlockHeader.ExcessBlobGas.Value.ToString();
            if (test.GenesisBlockHeader.BlobGasUsed.HasValue)
                genesisJson["blobGasUsed"] = test.GenesisBlockHeader.BlobGasUsed.Value.ToString();
            if (test.GenesisBlockHeader.SlotNumber.HasValue)
                genesisJson["slotNumber"] = test.GenesisBlockHeader.SlotNumber.Value.ToString();

            return StandardGenesisLoader.Parse(genesisJson);
        }

        private static JObject BuildAllocJson(BlockchainTestLoader.BlockchainTest test)
        {
            var alloc = new JObject();
            foreach (var kvp in test.Pre)
            {
                var acct = kvp.Value;
                var entry = new JObject
                {
                    ["balance"] = acct.Balance.ToString(),
                    ["nonce"] = "0x" + acct.Nonce.ToString("x"),
                };

                if (acct.Code is { Length: > 0 })
                    entry["code"] = "0x" + acct.Code.ToHex();

                if (acct.Storage.Count > 0)
                {
                    var storage = new JObject();
                    foreach (var slot in acct.Storage)
                        storage[ToPaddedHex(slot.Key)] = ToPaddedHex(slot.Value);
                    entry["storage"] = storage;
                }

                alloc[kvp.Key] = entry;
            }

            return alloc;
        }

        private static string ToPaddedHex(BigInteger value)
        {
            var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
            return "0x" + bytes.ToHex();
        }
    }
}
