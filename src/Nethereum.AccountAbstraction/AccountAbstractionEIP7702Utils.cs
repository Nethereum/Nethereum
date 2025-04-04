using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction
{
    public class AccountAbstractionEIP7702Utils
        {
            public static readonly byte[] INITCODE_EIP7702_MARKER = new byte[] { 0x77, 0x02 };

            public static bool IsEip7702UserOp(byte[] initCode)
            {
                return initCode.Length >= 2 && initCode.Take(2).SequenceEqual(INITCODE_EIP7702_MARKER);
            }

            public static bool IsEip7702UserOp(UserOperation userOperation)
            {
                return IsEip7702UserOp(userOperation.InitCode);
            }

            public static byte[] GetEip7702Delegate(byte[] initCode)
            {
                if (IsEip7702UserOp(initCode))
                {
                    return initCode.Skip(2).Take(20).ToArray();
                }
                return Array.Empty<byte>();
            }

            public static byte[] CreateEip7702InitCode(byte[] delegateAddress)
            {
                if (delegateAddress.Length != 20)
                    throw new ArgumentException("Delegate address must be 20 bytes.");

                return INITCODE_EIP7702_MARKER.Concat(delegateAddress).ToArray();
            }

            public static byte[] CreateEip7702InitCode(string delegateAddressHex)
            {
                var addressBytes = AddressUtil.Current.ConvertToValid20ByteAddress(delegateAddressHex).HexToByteArray();
                return CreateEip7702InitCode(addressBytes);
            }

            /// <summary>
            /// Adjust initCode for hashing as per EIP-7702 logic
            /// If initCode is short, return delegate only; else, replace first 20 bytes with delegate.
            /// </summary>
            public static byte[] UpdateInitCodeForHashing(byte[] initCode, byte[] delegateAddress)
            {
                if (!IsEip7702UserOp(initCode))
                    throw new ArgumentException("initCode must start with EIP-7702 marker");

                if (delegateAddress.Length != 20)
                    throw new ArgumentException("Delegate address must be 20 bytes.");

                if (initCode.Length < 20)
                {
                    return delegateAddress;
                }

                // Replace the first 20 bytes with the new delegate, keep the rest
                return delegateAddress.Concat(initCode.Skip(20)).ToArray();
            }

            public static byte[] UpdateInitCodeForHashing(byte[] initCode, string delegateAddressHex)
            {
                var delegateBytes = AddressUtil.Current
                    .ConvertToValid20ByteAddress(delegateAddressHex)
                    .HexToByteArray();
                return UpdateInitCodeForHashing(initCode, delegateBytes);
            }


            public static byte[] CreateEip7702InitCode(byte[] delegateAddress, byte[] extraData)
            {
                if (delegateAddress.Length != 20)
                    throw new ArgumentException("Delegate address must be 20 bytes.");

                return INITCODE_EIP7702_MARKER
                    .Concat(delegateAddress)
                    .Concat(extraData ?? Array.Empty<byte>())
                    .ToArray();
            }

            public static byte[] CreateEip7702InitCode(string delegateAddressHex, byte[] extraData)
            {
                var delegateBytes = AddressUtil.Current
                    .ConvertToValid20ByteAddress(delegateAddressHex)
                    .HexToByteArray();

                return CreateEip7702InitCode(delegateBytes, extraData);
            }
    }
}
