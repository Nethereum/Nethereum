using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Nethereum.ABI;
using Nethereum.ABI.Encoders;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.BaseAccount.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.ERC7579
{
    public class BatchCallsInput
    {
        [Parameter("tuple[]", "calls", 1)]
        public List<Call> Calls { get; set; }
    }

    public static class ERC7579ExecutionLib
    {
        public static byte[] EncodeSingle(string target, BigInteger value, byte[] callData)
        {
            var addressEncoder = new AddressTypeEncoder();
            var addressBytes = addressEncoder.EncodePacked(target);

            var valueBytes = ByteUtil.PadBytesLeft(value.ToByteArray(isUnsigned: true, isBigEndian: true), 32);

            var result = new byte[addressBytes.Length + valueBytes.Length + (callData?.Length ?? 0)];
            Array.Copy(addressBytes, 0, result, 0, addressBytes.Length);
            Array.Copy(valueBytes, 0, result, addressBytes.Length, valueBytes.Length);
            if (callData != null && callData.Length > 0)
            {
                Array.Copy(callData, 0, result, addressBytes.Length + valueBytes.Length, callData.Length);
            }

            return result;
        }

        public static byte[] EncodeBatch(Call[] calls)
        {
            if (calls == null || calls.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var abiEncode = new ABIEncode();
            return abiEncode.GetABIParamsEncoded(new BatchCallsInput { Calls = calls.ToList() });
        }

        public static (string target, BigInteger value, byte[] callData) DecodeSingle(byte[] data)
        {
            if (data == null || data.Length < 52)
            {
                throw new ArgumentException("Data must be at least 52 bytes (20 address + 32 value)", nameof(data));
            }

            var addressBytes = new byte[20];
            Array.Copy(data, 0, addressBytes, 0, 20);
            var target = addressBytes.ToHex(true);

            var valueBytes = new byte[32];
            Array.Copy(data, 20, valueBytes, 0, 32);
            var value = new BigInteger(valueBytes, isUnsigned: true, isBigEndian: true);

            byte[] callData = null;
            if (data.Length > 52)
            {
                callData = new byte[data.Length - 52];
                Array.Copy(data, 52, callData, 0, callData.Length);
            }

            return (target, value, callData ?? Array.Empty<byte>());
        }

        public static List<Call> DecodeBatch(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return new List<Call>();
            }

            var abiEncode = new ABIEncode();
            var decoded = abiEncode.DecodeEncodedComplexType<BatchCallsInput>(data);
            return decoded.Calls ?? new List<Call>();
        }
    }
}
