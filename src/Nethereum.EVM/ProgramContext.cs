using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
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
        public BigInteger GasPrice { get; internal set; } = 0;
        public BigInteger GasLimit { get; set; } = 10000000;
        public BigInteger Difficulty { get; set; } = 1;

        public ExecutionStateService ExecutionStateService { get; protected set; }

       
        public ProgramContext(CallInput callInput, ExecutionStateService executionStateService, string addressOrigin = null, long blockNumber = 1, long timestamp = 1438269988, string coinbase = "0x0000000000000000000000000000000000000000", long baseFee = 1)
        {
            if (addressOrigin == null) addressOrigin = callInput.From;
            AddressContractEncoded = new AddressType().Encode(callInput.To);
            AddressCallerEncoded = new AddressType().Encode(callInput.From);
            AddressOriginEncoded = new AddressType().Encode(addressOrigin);
            AddressCoinbaseEncoded = new AddressType().Encode(coinbase);
            DataInput = callInput.Data.HexToByteArray();
            AddressOrigin = addressOrigin;
            ExecutionStateService = executionStateService;
           
            BlockNumber = blockNumber;
            Timestamp = timestamp;
            Coinbase = coinbase;
            BaseFee = baseFee;

            if(callInput.Gas == null)
            {
                callInput.Gas = new Hex.HexTypes.HexBigInteger(1000000);
            }
            
            
            this.callInput = callInput;
        }

        public void InitialiaseContractBalanceFromCallInputValue()
        {
            this.ExecutionStateService.UpsertInternalBalance(this.AddressContract, this.Value);
        }

        public Task<byte[]> GetFromStorageAsync(BigInteger key)
        {
            return ExecutionStateService.GetFromStorageAsync(AddressContract, key);
        }

        public void SaveToStorage(BigInteger key, byte[] storageValue)
        {
            ExecutionStateService.SaveToStorage(AddressContract, key, storageValue);
        }

        public Dictionary<string, string> GetProgramContextStorageAsHex()
        {
            return ExecutionStateService.CreateOrGetAccountExecutionState(AddressContract).GetContractStorageAsHex();
        }


    }
}