using System.Collections.Generic;
using Nethereum.EVM.BlockchainState;
using Nethereum.Util;
using Nethereum.EVM.Gas;
using Nethereum.Model;

namespace Nethereum.EVM
{
    public enum ExecutionMode
    {
        Transaction,
        Call,
        SystemCall
    }

    public class TransactionExecutionContext
    {
        public ExecutionMode Mode { get; set; } = ExecutionMode.Transaction;
        public bool IsCallMode => Mode == ExecutionMode.Call || Mode == ExecutionMode.SystemCall;
        public string Sender { get; set; }
        public string To { get; set; }
        public byte[] Data { get; set; }

        // All value fields — EvmUInt256 (was BigInteger)
        public EvmUInt256 GasLimit { get; set; }
        public EvmUInt256 Value { get; set; }
        public EvmUInt256 GasPrice { get; set; }
        public EvmUInt256 MaxFeePerGas { get; set; }
        public EvmUInt256 MaxPriorityFeePerGas { get; set; }
        public EvmUInt256 EffectiveGasPrice { get; set; }
        public EvmUInt256 Nonce { get; set; }
        public bool IsEip1559 { get; set; }
        public bool IsContractCreation { get; set; }
        public bool IsType3Transaction { get; set; }
        public bool IsType4Transaction { get; set; }
        public List<string> BlobVersionedHashes { get; set; }
        public EvmUInt256 MaxFeePerBlobGas { get; set; }
        public List<AccessListEntry> AccessList { get; set; }
        public List<Authorisation7702Signed> AuthorisationList { get; set; }

        /// <summary>
        /// Pre-recovered authority addresses for EIP-7702, parallel to
        /// <see cref="AuthorisationList"/>. Populated from the witness in sync/zkVM
        /// mode (where signature recovery isn't available in-guest); entries may be
        /// <c>null</c> for tuples whose signature failed host-side validation. In
        /// async mode this stays null and authorities are recovered lazily from the
        /// signature via <c>Nethereum.Signer</c>.
        /// </summary>
        public List<string> AuthorisationAuthorities { get; set; }

        public EvmUInt256 BlockNumber { get; set; }
        public EvmUInt256 Timestamp { get; set; }
        public string Coinbase { get; set; }
        public EvmUInt256 BaseFee { get; set; }
        public EvmUInt256 Difficulty { get; set; }
        public EvmUInt256 BlockGasLimit { get; set; }
        public EvmUInt256 ExcessBlobGas { get; set; }
        public EvmUInt256 BlobBaseFee { get; set; } = EvmUInt256.One;
        public EvmUInt256 ChainId { get; set; }
        public EvmUInt256 Fee { get; set; }

        // Gas counters — long (hot path, need signed for underflow)
        public long IntrinsicGas { get; set; }
        public long FloorGas { get; set; }
        public long MinGasRequired { get; set; }
        public long AuthRefund { get; set; }
        public EvmUInt256 BlobGasCost { get; set; }

        public string ContractAddress { get; set; }
        public EvmUInt256 SenderNonceBeforeIncrement { get; set; }

        public int TransactionSnapshotId { get; set; }
        public bool HasCollision { get; set; }

        public ExecutionStateService ExecutionState { get; set; }
        public AccountExecutionState SenderAccount { get; set; }
        public byte[] Code { get; set; }
        public string DelegateAddress { get; set; }

        public bool TraceEnabled { get; set; }
    }
}
