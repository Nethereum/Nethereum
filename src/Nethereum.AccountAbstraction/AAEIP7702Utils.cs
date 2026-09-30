using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction
{
    public class AAEIP7702Utils
        {
            public static readonly byte[] INITCODE_EIP7702_MARKER = new byte[] { 0x77, 0x02 };

            public static readonly byte[] INITCODE_EIP7702_MARKER_ADDRESS =
                INITCODE_EIP7702_MARKER.Concat(new byte[18]).ToArray();

            public static bool IsEip7702UserOp(byte[] initCode)
            {
                return initCode.Length >= 2 && initCode.Take(2).SequenceEqual(INITCODE_EIP7702_MARKER);
            }

            public static byte[] BuildInitCode(string factory, byte[] factoryData)
            {
                if (string.IsNullOrEmpty(factory) || factory == "0x" ||
                    factory == Nethereum.Util.AddressUtil.ZERO_ADDRESS)
                {
                    return Array.Empty<byte>();
                }

                var factoryBytes = factory.HexToByteArray();
                var head = IsEip7702UserOp(factoryBytes) && factoryBytes.Length < 20
                    ? INITCODE_EIP7702_MARKER_ADDRESS
                    : factoryBytes;

                var data = factoryData ?? Array.Empty<byte>();
                return head.Concat(data).ToArray();
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
