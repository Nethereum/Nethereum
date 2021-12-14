using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Contracts.TransactionHandlers.MultiSend
{
    [Function("multiSend")]
    public class MultiSendFunction : FunctionMessage
    {
        [Parameter("bytes", "transactions", 1)]
        public byte[] Transactions { get; set; }

        public MultiSendFunction()
        {

        }

        public MultiSendFunction(params IMultiSendInput[] multiSendInputs)
        {
            Transactions = MultiSendEncoder.EncodeMultiSendList(multiSendInputs);
        }
    }

    public interface IMultiSendInput
    {
        string Target { get; set; }
        BigInteger Value { get; set; }
        byte[] GetCallData();

        byte[] GetTransactionEncoded();
    }

    public class MultiSendEncoder
    {
        public static byte[] EncodeMultiSend(IMultiSendInput multiSendInput)
        {
            var callData = multiSendInput.GetCallData();
            var abiEncoder = new ABIEncode();
            return abiEncoder.GetABIEncodedPacked(
                new ABIValue("uint8", (int)ContractOperationType.Call),
                new ABIValue("address", multiSendInput.Target),
                new ABIValue("uint256", multiSendInput.Value),
                new ABIValue("uint256", callData.Length),
                new ABIValue("bytes", callData));

        }

        public static byte[] EncodeMultiSendList(params IMultiSendInput[] multiSendInputs)
        {
            var returnList = new List<byte>();
            foreach (var multiSendInput in multiSendInputs)
            {
                returnList.AddRange(multiSendInput.GetTransactionEncoded());
            }

            return returnList.ToArray();
        }
    }



    public class MultiSendFunctionInput<TFunctionMessage> : IMultiSendInput
        where TFunctionMessage : FunctionMessage, new()
    {
        public MultiSendFunctionInput(TFunctionMessage functionMessage, string contractAddressTarget, BigInteger value)
        {
            this.Target = contractAddressTarget;
            this.Input = functionMessage;
            this.Value = value;
        }

        public MultiSendFunctionInput(TFunctionMessage functionMessage, string contractAddressTarget) : this(functionMessage, contractAddressTarget, 0)
        {

        }

        public string Target { get; set; }
        public TFunctionMessage Input { get; set; }

        public BigInteger Value { get; set; }

        public byte[] GetCallData()
        {
            return Input.GetCallData();
        }

        public byte[] GetTransactionEncoded()
        {
            return MultiSendEncoder.EncodeMultiSend(this);
        }

    }
}
