using System.Numerics;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.AccountAbstraction
{
    public class AATransactionReceipt : TransactionReceipt
    {
        public string UserOpHash { get; set; }
        public bool UserOpSuccess { get; set; }
        public string RevertReason { get; set; }
        public BigInteger ActualGasCost { get; set; }
        public BigInteger ActualGasUsed { get; set; }
        public string Paymaster { get; set; }
        public string Sender { get; set; }

        public AATransactionReceipt() { }

        public static AATransactionReceipt FromUserOperationReceipt(
            RPC.AccountAbstraction.DTOs.UserOperationReceipt userOpReceipt)
        {
            var receipt = new AATransactionReceipt
            {
                UserOpHash = userOpReceipt.UserOpHash,
                UserOpSuccess = userOpReceipt.Success,
                RevertReason = userOpReceipt.Reason,
                ActualGasCost = userOpReceipt.ActualGasCost?.Value ?? 0,
                ActualGasUsed = userOpReceipt.ActualGasUsed?.Value ?? 0,
                Paymaster = userOpReceipt.Paymaster,
                Sender = userOpReceipt.Sender
            };

            if (userOpReceipt.Receipt != null)
            {
                receipt.TransactionHash = userOpReceipt.Receipt.TransactionHash;
                receipt.TransactionIndex = userOpReceipt.Receipt.TransactionIndex;
                receipt.BlockHash = userOpReceipt.Receipt.BlockHash;
                receipt.BlockNumber = userOpReceipt.Receipt.BlockNumber;
                receipt.CumulativeGasUsed = userOpReceipt.Receipt.CumulativeGasUsed;
                receipt.GasUsed = userOpReceipt.Receipt.GasUsed;
                receipt.EffectiveGasPrice = userOpReceipt.Receipt.EffectiveGasPrice;
                receipt.ContractAddress = userOpReceipt.Receipt.ContractAddress;
                receipt.Status = userOpReceipt.Receipt.Status;
                receipt.Logs = userOpReceipt.Receipt.Logs;
                receipt.LogsBloom = userOpReceipt.Receipt.LogsBloom;
                receipt.From = userOpReceipt.Receipt.From;
                receipt.To = userOpReceipt.Receipt.To;
                receipt.Type = userOpReceipt.Receipt.Type;
                receipt.Root = userOpReceipt.Receipt.Root;
            }

            return receipt;
        }
    }
}
