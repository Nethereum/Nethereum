using Nethereum.Hex.HexConvertors.Extensions;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.EVM.BlockchainState
{
    public class AccountExecutionState
    {
        public string Address { get; set; }
        public AccountExecutionBalance Balance { get; set; } = new AccountExecutionBalance();
        public Dictionary<BigInteger, byte[]> Storage { get; set; } = new Dictionary<BigInteger, byte[]>();
        public BigInteger? Nonce { get; set; }
        public byte[] Code { get; set; }

        public bool StorageContainsKey(BigInteger key)
        {
            return Storage.ContainsKey(key);
        }

        public void UpsertStorageValue(BigInteger key, byte[] value)
        {
            if (!Storage.ContainsKey(key))
            {
                Storage.Add(key, value);
            }
            else
            {
                Storage[key] = value;
            }
        }

        public byte[] GetStorageValue(BigInteger key)
        {
            if (StorageContainsKey(key))
            {
                return Storage[key];
            }
            return null;
        }

        public Dictionary<string, string> GetContractStorageAsHex()
        {
            var storage = Storage;
            if (storage == null) return null;
            var dictionary = new Dictionary<string, string>();
            foreach (var item in storage)
            {
                if (item.Value != null)
                {
                    dictionary.Add(item.Key.ToString(), item.Value.ToHex());
                }
            }
            return dictionary;
        }
    }
}