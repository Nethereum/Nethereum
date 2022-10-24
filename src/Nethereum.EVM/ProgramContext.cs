using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.EVM
{

    public class ProgramContext
    {
        private readonly CallInput callInput;

        public byte[] AddressContractEncoded { get; }
        public byte[] AddressCallerEncoded { get; }
        public byte[] AddressOriginEncoded { get; }
        public byte[] AddressCoinbaseEncoded { get; }
        public byte[] DataInput { get; }
        public string AddressContract => callInput.To;
        public string AddressCaller => callInput.From;
        public BigInteger ChainId => callInput.ChainId;
        public BigInteger Gas => callInput.Gas;
        public BigInteger Value => callInput.Value;
        public string AddressOrigin { get; }
        public BigInteger BlockNumber { get; }
        public BigInteger Timestamp { get; }
        public string Coinbase { get; }
        public BigInteger BaseFee { get; }
        public INodeDataService NodeDataService { get; }

        protected InternalStorageState Storage { get; set; }

        public ProgramContext(CallInput callInput, INodeDataService nodeDataService, InternalStorageState internalStorageState = null, string addressOrigin = null, long blockNumber = 1, long timestamp = 1438269988, string coinbase = "0x0000000000000000000000000000000000000000", long baseFee = 1)
        {
            if (addressOrigin == null) addressOrigin = callInput.From;
            AddressContractEncoded = new AddressType().Encode(callInput.To);
            AddressCallerEncoded = new AddressType().Encode(callInput.From);
            AddressOriginEncoded = new AddressType().Encode(addressOrigin);
            AddressCoinbaseEncoded = new AddressType().Encode(coinbase);
            DataInput = callInput.Data.HexToByteArray();
            AddressOrigin = addressOrigin;
            NodeDataService = nodeDataService;
            BlockNumber = blockNumber;
            Timestamp = timestamp;
            Coinbase = coinbase;
            BaseFee = baseFee;
            if(internalStorageState == null)
            {
                Storage = new InternalStorageState();
            }
            else
            {
                Storage = internalStorageState;
            }
            
            this.callInput = callInput;
        }

        public async Task<byte[]> GetFromStorageAsync(BigInteger key)
        {
            if (!Storage.ContainsKey(AddressContract, key))
            {
                var storageValue = await NodeDataService.GetStorageAtAsync(AddressContractEncoded, key);
                Storage.UpsertValue(AddressContract, key, storageValue);
            }
          
            return Storage.GetValue(AddressContract, key);
        }

        public void SaveToStorage(BigInteger key, byte[] storageValue)
        {
            Storage.UpsertValue(AddressContract, key, storageValue);
        }

        public Dictionary<BigInteger, byte[]> GetProgramContextStorage()
        {
            return Storage.GetStorage(AddressContract);
        }

        public Dictionary<string, string> GetProgramContextStorageAsHex()
        {
            return Storage.GetContractStorageAsHex(AddressContract);
        }


    }
}