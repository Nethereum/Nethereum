using Nethereum.Hex.HexConvertors.Extensions;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.EVM
{
    public class InternalStorageState
    {
        public Dictionary<string, Dictionary<BigInteger, byte[]>> Storage { get; protected set; }

        public InternalStorageState()
        {
            Storage = new Dictionary<string, Dictionary<BigInteger, byte[]>>(); 
        }
        public bool ContainsKey(string address, BigInteger key)
        {
            address = address.ToLower();
            return Storage.ContainsKey(address) && Storage[address].ContainsKey(key);
        }

        public void UpsertValue(string address, BigInteger key, byte[] value)
        {
            address = address.ToLower();
            if(!Storage.ContainsKey(address))
            {
                Storage.Add(address, new Dictionary<BigInteger, byte[]>());
            }

            if (!Storage[address].ContainsKey(key)) 
            {
                Storage[address].Add(key, value);
            }
            else
            {
                Storage[address][key] = value;
            }
        }

        public byte[] GetValue(string address, BigInteger key)
        {
            address = address.ToLower();
            if (ContainsKey(address, key))
            {
                return Storage[address][key]; 
            }
            return null;
        }

        public Dictionary<BigInteger, byte[]> GetStorage(string address)
        {
            address = address.ToLower();
            if (!Storage.ContainsKey(address.ToLower())) return null;
            return Storage[address];
        }

        public Dictionary<string, string> GetContractStorageAsHex(string address)
        {
            address = address.ToLower();
            var storage = GetStorage(address);
            if (storage == null) return null;
            var dictionary = new Dictionary<string, string>();
            foreach (var item in storage)
            {
                if (item.Value != null) {
                    dictionary.Add(item.Key.ToString(), item.Value.ToHex());
                }
            }
            return dictionary;
        }
    }
}