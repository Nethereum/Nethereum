using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Contracts.TransactionHandlers.MultiSend;
using Nethereum.Util;

namespace Nethereum.GnosisSafe.ContractDefinition
{
    public partial class EncodeTransactionDataFunction : EncodeTransactionDataFunctionBase { }

    [Function("encodeTransactionData", "bytes")]
    public class EncodeTransactionDataFunctionBase : FunctionMessage
    {
        [Parameter("address", "to", 1)]
        public virtual string To { get; set; }

        [Parameter("uint256", "value", 2)] public virtual BigInteger Value { get; set; } = 0;
        [Parameter("bytes", "data", 3)]
        public virtual byte[] Data { get; set; }

        [Parameter("uint8", "operation", 4)] public virtual byte Operation { get; set; } = (byte)ContractOperationType.Call;
        [Parameter("uint256", "safeTxGas", 5)]
        public virtual BigInteger SafeTxGas { get; set; } = 0;
        [Parameter("uint256", "baseGas", 6)] 
        public virtual BigInteger BaseGas { get; set; } = 0;
        [Parameter("uint256", "gasPrice", 7)]
        public virtual BigInteger SafeGasPrice { get; set; }

        [Parameter("address", "gasToken", 8)]
        public virtual string GasToken { get; set; } = AddressUtil.AddressEmptyAsHex;

        [Parameter("address", "refundReceiver", 9)]
        public virtual string RefundReceiver { get; set; } = AddressUtil.AddressEmptyAsHex;

        [Parameter("uint256", "nonce", 10)] public virtual BigInteger SafeNonce { get; set; } = 0;
    }

}
