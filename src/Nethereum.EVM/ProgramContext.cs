using Nethereum.ABI;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using System;
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
        public string CodeAddress { get; }
        public BigInteger BlockNumber { get; }
        public BigInteger Timestamp { get; }
        public string Coinbase { get; }
        public BigInteger BaseFee { get; }
        public BigInteger BlobBaseFee { get; set; } = 0;
        public byte[][] BlobHashes { get; set; } = new byte[0][];
        public BigInteger GasPrice { get; internal set; } = 0;
        public BigInteger GasLimit { get; set; } = 10000000;
        public BigInteger Difficulty { get; set; } = 1;
        public BigInteger Fee { get; set; }
        public Dictionary<BigInteger, byte[]> TransientStorage { get; } = new();
        public bool IsStatic { get; set; } = false;
        public int Depth { get; set; } = 0;
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString().Substring(0, 8);
        public bool EnforceGasSentry { get; set; } = false;

        public ExecutionStateService ExecutionStateService { get; protected set; }
        public AccessListTracker AccessListTracker { get; protected set; }

       
        public ProgramContext(CallInput callInput, ExecutionStateService executionStateService, string addressOrigin = null, string codeAddress = null, long blockNumber = 1, long timestamp = 1438269988, string coinbase = "0x0000000000000000000000000000000000000000", long baseFee = 1, bool trackAccessList = false)
        {
            if (addressOrigin == null) addressOrigin = callInput.From;
            AccessListTracker = new AccessListTracker(callInput.From, callInput.To, trackAccessList);
            AddressContractEncoded = new AddressType().Encode(callInput.To);
            AddressCallerEncoded = new AddressType().Encode(callInput.From);
            AddressOriginEncoded = new AddressType().Encode(addressOrigin);
            AddressCoinbaseEncoded = new AddressType().Encode(coinbase);
            DataInput = callInput.Data.HexToByteArray();
            AddressOrigin = addressOrigin;

            //When delegating the state of the contract is not the same as the execution / program source one.
            //The addressContractExecutionSource is the one matching the byteCode of the program, whilst To can be just the storage or both storage and execution
            //so we have an extra field to track this.
            if(string.IsNullOrEmpty(codeAddress))
            {
                codeAddress = callInput.To;
            }
            CodeAddress = codeAddress;
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

            if (callInput.GasPrice != null)
            {
                GasPrice = callInput.GasPrice;
            }
        }

        public void InitialiaseContractBalanceFromCallInputValue()
        {
            this.ExecutionStateService.UpsertInternalBalance(this.AddressContract, this.Value);
        }

        public Task<byte[]> GetFromStorageAsync(BigInteger key)
        {
            AccessListTracker?.RecordStorageAccess(AddressContract, key);
            return ExecutionStateService.GetFromStorageAsync(AddressContract, key);
        }

        public void SaveToStorage(BigInteger key, byte[] storageValue)
        {
            AccessListTracker?.RecordStorageAccess(AddressContract, key);
            ExecutionStateService.SaveToStorage(AddressContract, key, storageValue);
        }

        public Dictionary<string, string> GetProgramContextStorageAsHex()
        {
            return ExecutionStateService.CreateOrGetAccountExecutionState(AddressContract).GetContractStorageAsHex();
        }

        public List<AccessListItem> GetAccessList()
        {
            return AccessListTracker?.GetAccessList() ?? new List<AccessListItem>();
        }

        public void RecordAddressAccess(string address)
        {
            AccessListTracker?.RecordAddressAccess(address);
        }

        public void SetAccessListTracker(AccessListTracker tracker)
        {
            AccessListTracker = tracker;
        }
    }
}