using System;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.RPC.TransactionManagers
{
    public class EtherTransferTransactionInputBuilder
    {
        public static TransactionInput CreateTransactionInput(string fromAddress, string toAddress, decimal etherAmount, decimal? gasPriceGwei = null, BigInteger? gas = null, BigInteger? nonce = null)
        {
            if (string.IsNullOrEmpty(toAddress)) throw new ArgumentNullException(nameof(toAddress));
            if (etherAmount <= 0) throw new ArgumentOutOfRangeException(nameof(etherAmount));
            if (gasPriceGwei <= 0) throw new ArgumentOutOfRangeException(nameof(gasPriceGwei));

            var transactionInput = new TransactionInput()
            {
                To = toAddress,
                From = fromAddress,
                GasPrice = gasPriceGwei == null ? null : new HexBigInteger(UnitConversion.Convert.ToWei(gasPriceGwei.Value, UnitConversion.EthUnit.Gwei)),
                Value = new HexBigInteger(UnitConversion.Convert.ToWei(etherAmount)),
                Gas = gas == null ? null : new HexBigInteger(gas.Value),
                Nonce = nonce == null ? null : new HexBigInteger(nonce.Value)
            };
            return transactionInput;
        }

        public static TransactionInput CreateTransactionInput(string fromAddress, string toAddress, decimal etherAmount, BigInteger maxPriorityFeePerGas, BigInteger maxFeePerGas,  BigInteger? gas = null, BigInteger? nonce = null)
        {
            if (string.IsNullOrEmpty(toAddress)) throw new ArgumentNullException(nameof(toAddress));
            if (etherAmount <= 0) throw new ArgumentOutOfRangeException(nameof(etherAmount));
            if (maxPriorityFeePerGas <= 0) throw new ArgumentOutOfRangeException(nameof(maxPriorityFeePerGas));
            if (maxFeePerGas <= 0) throw new ArgumentOutOfRangeException(nameof(maxFeePerGas));

            var transactionInput = new TransactionInput()
            {
                Type = new HexBigInteger(0x02),
                To = toAddress,
                From = fromAddress,
                MaxFeePerGas = new HexBigInteger(maxFeePerGas),
                MaxPriorityFeePerGas = new HexBigInteger(maxPriorityFeePerGas),
                Value = new HexBigInteger(UnitConversion.Convert.ToWei(etherAmount)),
                Gas = gas == null ? null : new HexBigInteger(gas.Value),
                Nonce = nonce == null ? null : new HexBigInteger(nonce.Value)
            };
            return transactionInput;
        }
    }
}