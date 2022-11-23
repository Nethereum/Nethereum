using System;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.Signer;

namespace Nethereum.EVM.Execution
{
    public class EvmPreCompiledContractsExecution
    {
        public virtual bool IsPrecompiledAdress(string address)
        {
            switch (address.ToHexCompact()) //easy case statement to copy / paste for execution implementation
            {
                case "1":
                case "2":
                case "3":
                case "4":
                case "5":
                case "6":
                case "7":
                case "8":
                    return true;
            }

            return false;
        }
        public virtual byte[] ExecutePreCompile(string address, byte[] data)
        {
            switch (address.ToHexCompact())
            {
                case "1": //ecrecover
                    return EcRecover(data);

                case "4": //datacopy
                    return DataCopy(data);
                case "2":
                case "3":
                case "5":
                case "6":
                case "7":
                case "8":
                    throw new NotImplementedException($"Precompiled contract: {address}, not implemented yet");

            }

            return null;
        }

        public byte[] EcRecover(byte[] data)
        {
            data = data.PadTo128Bytes();
            var hash = data.Slice(0, 32);
            var v = data[63];
            var r = data.Slice(64, 96);
            var s = data.Slice(96, 128);

            var recoveredAddress = EthECKey.RecoverFromSignature(EthECDSASignatureFactory.FromComponents(r, s, new byte[] { v }), hash).GetPublicAddressAsBytes();
            return recoveredAddress.PadTo32Bytes();
        }

        public byte[] DataCopy(byte[] data)
        {
            return data;
        }
    }
}