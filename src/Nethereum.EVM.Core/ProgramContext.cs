using Nethereum.Util;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Types;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
#if !EVM_SYNC
using Nethereum.RPC.Eth.DTOs;
#endif
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM
{

    public class ProgramContext
    {
        private readonly EvmCallContext callInput;

        public byte[] AddressContractEncoded { get; }
        public byte[] AddressCallerEncoded { get; }
        public byte[] AddressOriginEncoded { get; }
        public byte[] AddressCoinbaseEncoded { get; }
        public byte[] DataInput { get; }
        public string AddressContract => callInput.To;
        public string AddressCaller => callInput.From;
        public EvmUInt256 ChainId { get; }
        public long Gas { get; }
        public EvmUInt256 Value => callInput.Value;
        public string AddressOrigin { get; }
        public string CodeAddress { get; }
        public EvmUInt256 BlockNumber { get; }
        public EvmUInt256 Timestamp { get; }
        public string Coinbase { get; }
        public EvmUInt256 BaseFee { get; }
        public EvmUInt256 BlobBaseFee { get; set; } = EvmUInt256.Zero;
        public byte[][] BlobHashes { get; set; } = new byte[0][];
        public EvmUInt256 GasPrice { get; internal set; } = EvmUInt256.Zero;
        public EvmUInt256 GasLimit { get; set; } = new EvmUInt256(10000000);
        public EvmUInt256 Difficulty { get; set; } = EvmUInt256.One;
        public EvmUInt256 Fee { get; set; }
        public Dictionary<string, Dictionary<EvmUInt256, byte[]>> TransientStorage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool IsStatic { get; set; } = false;
        public int Depth { get; set; } = 0;
        public string ExecutionId { get; set; } = Guid.NewGuid().ToString().Substring(0, 8);
        public bool EnforceGasSentry { get; set; } = false;

        // Per-fork refund constants (set from HardforkConfig by EVMSimulator)
        public long SstoreClearsSchedule { get; set; } = 4800;

        public ExecutionStateService ExecutionStateService { get; protected set; }
        public AccessListTracker AccessListTracker { get; protected set; }


        public ProgramContext(EvmCallContext callInput, ExecutionStateService executionStateService, string addressOrigin = null, string codeAddress = null, EvmUInt256 blockNumber = default, EvmUInt256 timestamp = default, string coinbase = "0x0000000000000000000000000000000000000000", EvmUInt256 baseFee = default, bool trackAccessList = false)
        {
            if (addressOrigin == null) addressOrigin = callInput.From;
            AccessListTracker = new AccessListTracker(callInput.From, callInput.To, trackAccessList);
            AddressContractEncoded = AddressUtil.EncodeAddressTo32Bytes(callInput.To);
            AddressCallerEncoded = AddressUtil.EncodeAddressTo32Bytes(callInput.From);
            AddressOriginEncoded = AddressUtil.EncodeAddressTo32Bytes(addressOrigin);
            AddressCoinbaseEncoded = AddressUtil.EncodeAddressTo32Bytes(coinbase);
            DataInput = callInput.Data ?? new byte[0];
            AddressOrigin = addressOrigin;

            if(string.IsNullOrEmpty(codeAddress))
            {
                codeAddress = callInput.To;
            }
            CodeAddress = codeAddress;
            ExecutionStateService = executionStateService;

            BlockNumber = blockNumber;
            Timestamp = timestamp;
            Coinbase = coinbase;
            BaseFee = baseFee.IsZero ? EvmUInt256.One : baseFee;

            Gas = callInput.Gas;

            this.callInput = callInput;

            ChainId = callInput.ChainId;
            GasPrice = callInput.GasPrice;
        }

#if !EVM_SYNC
        // Backwards-compatible overload for CallInput (RPC DTO)
        public ProgramContext(CallInput callInput, ExecutionStateService executionStateService, string addressOrigin = null, string codeAddress = null, EvmUInt256 blockNumber = default, EvmUInt256 timestamp = default, string coinbase = "0x0000000000000000000000000000000000000000", EvmUInt256 baseFee = default, bool trackAccessList = false)
            : this(CallInputToEvmCallContext(callInput), executionStateService, addressOrigin, codeAddress, blockNumber, timestamp, coinbase, baseFee, trackAccessList)
        {
        }

        private static EvmCallContext CallInputToEvmCallContext(CallInput callInput)
        {
            return new EvmCallContext
            {
                From = callInput.From,
                To = callInput.To,
                Data = callInput.Data?.HexToByteArray(),
                Value = callInput.Value != null ? EvmUInt256BigIntegerExtensions.FromBigInteger(callInput.Value.Value) : EvmUInt256.Zero,
                Gas = callInput.Gas != null ? (long)(BigInteger)callInput.Gas : 1000000,
                ChainId = callInput.ChainId != null ? EvmUInt256BigIntegerExtensions.FromBigInteger(callInput.ChainId.Value) : EvmUInt256.Zero,
                GasPrice = callInput.GasPrice != null ? EvmUInt256BigIntegerExtensions.FromBigInteger(callInput.GasPrice.Value) : EvmUInt256.Zero,
            };
        }
#endif

        public void InitialiaseContractBalanceFromCallInputValue()
        {
            this.ExecutionStateService.CreditBalance(this.AddressContract, this.Value);
        }

#if EVM_SYNC
        public byte[] GetFromStorage(EvmUInt256 key)
        {
            AccessListTracker?.RecordStorageAccess(AddressContract, key);
            return ExecutionStateService.GetFromStorage(AddressContract, key);
        }
#else
        public Task<byte[]> GetFromStorageAsync(EvmUInt256 key)
        {
            AccessListTracker?.RecordStorageAccess(AddressContract, key);
            return ExecutionStateService.GetFromStorageAsync(AddressContract, key);
        }
#endif

        public void SaveToStorage(EvmUInt256 key, byte[] storageValue)
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
