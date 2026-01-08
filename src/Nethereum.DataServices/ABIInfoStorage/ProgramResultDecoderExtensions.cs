using Nethereum.ABI.ABIRepository;
using Nethereum.EVM;
using Nethereum.EVM.Decoding;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Numerics;

namespace Nethereum.DataServices.ABIInfoStorage
{
    public static class ProgramResultDecoderExtensions
    {
        public static DecodedProgramResult DecodeWithSourcify(
            this Program program,
            CallInput call,
            long chainId)
        {
            var storage = ABIInfoStorageFactory.CreateWithSourcifyOnly();
            var decoder = new ProgramResultDecoder(storage);
            return decoder.Decode(program, call, chainId);
        }

        public static DecodedProgramResult DecodeWithEtherscan(
            this Program program,
            CallInput call,
            long chainId,
            string etherscanApiKey)
        {
            var storage = ABIInfoStorageFactory.CreateWithEtherscanOnly(etherscanApiKey);
            var decoder = new ProgramResultDecoder(storage);
            return decoder.Decode(program, call, chainId);
        }

        public static DecodedProgramResult Decode(
            this Program program,
            CallInput call,
            long chainId,
            string etherscanApiKey = null)
        {
            var storage = ABIInfoStorageFactory.CreateDefault(etherscanApiKey);
            var decoder = new ProgramResultDecoder(storage);
            return decoder.Decode(program, call, chainId);
        }

        public static DecodedProgramResult DecodeWithStorage(
            this Program program,
            CallInput call,
            long chainId,
            IABIInfoStorage storage)
        {
            var decoder = new ProgramResultDecoder(storage);
            return decoder.Decode(program, call, chainId);
        }

        public static DecodedProgramResult DecodeWithSourcify(
            this ProgramResult result,
            List<ProgramTrace> trace,
            CallInput call,
            long chainId)
        {
            var storage = ABIInfoStorageFactory.CreateWithSourcifyOnly();
            var decoder = new ProgramResultDecoder(storage);
            return decoder.Decode(result, trace, call, chainId);
        }

        public static DecodedProgramResult Decode(
            this ProgramResult result,
            List<ProgramTrace> trace,
            CallInput call,
            long chainId,
            string etherscanApiKey = null)
        {
            var storage = ABIInfoStorageFactory.CreateDefault(etherscanApiKey);
            var decoder = new ProgramResultDecoder(storage);
            return decoder.Decode(result, trace, call, chainId);
        }

        public static DecodedProgramResult DecodeWithStorage(
            this ProgramResult result,
            List<ProgramTrace> trace,
            CallInput call,
            long chainId,
            IABIInfoStorage storage)
        {
            var decoder = new ProgramResultDecoder(storage);
            return decoder.Decode(result, trace, call, chainId);
        }
    }
}
