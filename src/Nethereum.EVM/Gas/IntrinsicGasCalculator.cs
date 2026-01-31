using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.EVM.Gas
{
    public static class IntrinsicGasCalculator
    {
        public const int G_TRANSACTION = 21000;
        public const int G_TXDATAZERO = 4;
        public const int G_TXDATANONZERO = 16;
        public const int G_TXCREATE = 32000;
        public const int G_CODEDEPOSIT = 200;
        public const int G_ACCESS_LIST_ADDRESS = 2400;
        public const int G_ACCESS_LIST_STORAGE = 1900;
        public const int G_INITCODE_WORD = 2;
        public const int G_FLOOR_PER_TOKEN = 10;
        public const int G_TOKENS_PER_NONZERO = 4;
        public const int GAS_PER_BLOB = 131072;
        public const int MIN_BASE_FEE_PER_BLOB_GAS = 1;
        public const int BLOB_BASE_FEE_UPDATE_FRACTION = 3338477;

        public static BigInteger CalculateIntrinsicGas(
            byte[] data,
            bool isContractCreation,
            IList<AccessListEntry> accessList)
        {
            BigInteger gas = G_TRANSACTION;

            if (isContractCreation)
            {
                gas += G_TXCREATE;

                // EIP-3860: Initcode word gas (always enabled - post-Shanghai)
                if (data != null && data.Length > 0)
                {
                    int initcodeWords = (data.Length + 31) / 32;
                    gas += initcodeWords * G_INITCODE_WORD;
                }
            }

            if (data != null && data.Length > 0)
            {
                foreach (var b in data)
                {
                    gas += b == 0 ? G_TXDATAZERO : G_TXDATANONZERO;
                }
            }

            if (accessList != null)
            {
                foreach (var entry in accessList)
                {
                    gas += G_ACCESS_LIST_ADDRESS;
                    if (entry.StorageKeys != null)
                    {
                        gas += entry.StorageKeys.Count * G_ACCESS_LIST_STORAGE;
                    }
                }
            }

            return gas;
        }

        public static BigInteger CalculateTokensInCalldata(byte[] data)
        {
            if (data == null || data.Length == 0)
                return 0;

            int zeroBytes = 0;
            int nonZeroBytes = 0;
            foreach (var b in data)
            {
                if (b == 0) zeroBytes++;
                else nonZeroBytes++;
            }
            return zeroBytes + (nonZeroBytes * G_TOKENS_PER_NONZERO);
        }

        public static BigInteger CalculateFloorGasLimit(byte[] data, bool isContractCreation)
        {
            var tokens = CalculateTokensInCalldata(data);
            BigInteger floor = G_TRANSACTION + (G_FLOOR_PER_TOKEN * tokens);
            if (isContractCreation)
                floor += G_TXCREATE;
            return floor;
        }

        public static BigInteger CalculateBlobBaseFee(BigInteger excessBlobGas)
        {
            return FakeExponential(MIN_BASE_FEE_PER_BLOB_GAS, excessBlobGas, BLOB_BASE_FEE_UPDATE_FRACTION);
        }

        public static BigInteger CalculateBlobGasCost(int blobCount, BigInteger blobBaseFee)
        {
            var blobGasUsed = blobCount * GAS_PER_BLOB;
            return blobGasUsed * blobBaseFee;
        }

        private static BigInteger FakeExponential(BigInteger factor, BigInteger numerator, BigInteger denominator)
        {
            int i = 1;
            BigInteger output = 0;
            BigInteger numeratorAccum = factor * denominator;
            while (numeratorAccum > 0)
            {
                output += numeratorAccum;
                numeratorAccum = (numeratorAccum * numerator) / (denominator * i);
                i++;
            }
            return output / denominator;
        }

        public static BigInteger CalculateCodeDepositGas(int codeLength)
        {
            return codeLength * G_CODEDEPOSIT;
        }

        public static BigInteger CalculateMaxRefund(BigInteger gasUsed)
        {
            return gasUsed / GasConstants.REFUND_QUOTIENT;
        }
    }

    public class AccessListEntry
    {
        public string Address { get; set; }
        public IList<string> StorageKeys { get; set; }
    }
}
