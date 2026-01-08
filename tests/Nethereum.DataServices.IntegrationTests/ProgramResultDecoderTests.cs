using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.ABIRepository;
using Nethereum.DataServices.ABIInfoStorage;
using Nethereum.EVM.Decoding;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.DataServices.IntegrationTests
{
    public class ProgramResultDecoderTests
    {
        private const string WETH_CONTRACT = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
        private const string USDC_CONTRACT = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
        private const long MAINNET_CHAIN_ID = 1;

        [Fact]
        public void ShouldDecodeTransferCallWithSourcify()
        {
            var abiStorage = ABIInfoStorageFactory.CreateWithSourcifyOnly();
            var decoder = new ProgramResultDecoder(abiStorage);

            var toAddress = "0x1234567890123456789012345678901234567890";
            var transferData = "0xa9059cbb" +
                "000000000000000000000000" + toAddress.Substring(2) +
                "00000000000000000000000000000000000000000000000000000000000f4240";

            var callInput = new CallInput
            {
                From = "0x0000000000000000000000000000000000000001",
                To = WETH_CONTRACT,
                Data = transferData
            };

            var decoded = decoder.DecodeCall(callInput, MAINNET_CHAIN_ID, 0);

            Assert.True(decoded.IsDecoded);
            Assert.NotNull(decoded.Function);
            Assert.Equal("transfer", decoded.Function.Name);
            Assert.Equal(2, decoded.InputParameters.Count);

            var toParam = decoded.InputParameters[0];
            var amountParam = decoded.InputParameters[1];

            Assert.Equal("dst", toParam.Parameter.Name);
            Assert.Equal("wad", amountParam.Parameter.Name);
        }

        [Fact]
        public void ShouldDecodeTransferEventWithSourcify()
        {
            var abiStorage = ABIInfoStorageFactory.CreateWithSourcifyOnly();
            var decoder = new ProgramResultDecoder(abiStorage);

            var transferEventSignature = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";
            var fromAddress = "0x0000000000000000000000001234567890123456789012345678901234567890";
            var toAddress = "0x0000000000000000000000000987654321098765432109876543210987654321";
            var amount = "0x00000000000000000000000000000000000000000000000000000000000f4240";

            var log = new FilterLog
            {
                Address = WETH_CONTRACT,
                Topics = new object[] {
                    transferEventSignature,
                    fromAddress,
                    toAddress
                },
                Data = amount,
                LogIndex = new HexBigInteger(0)
            };

            var decoded = decoder.DecodeLog(log, MAINNET_CHAIN_ID);

            Assert.True(decoded.IsDecoded);
            Assert.NotNull(decoded.Event);
            Assert.Equal("Transfer", decoded.Event.Name);
            Assert.True(decoded.Parameters.Count >= 3);
        }

        [Fact]
        public void ShouldGenerateHumanReadableOutput()
        {
            var abiStorage = ABIInfoStorageFactory.CreateWithSourcifyOnly();
            var decoder = new ProgramResultDecoder(abiStorage);

            var toAddress = "0x1234567890123456789012345678901234567890";
            var transferData = "0xa9059cbb" +
                "000000000000000000000000" + toAddress.Substring(2) +
                "00000000000000000000000000000000000000000000000000000000000f4240";

            var callInput = new CallInput
            {
                From = "0x0000000000000000000000000000000000000001",
                To = WETH_CONTRACT,
                Data = transferData
            };

            var programResult = new EVM.ProgramResult
            {
                IsRevert = false,
                Result = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01 }
            };

            var decoded = decoder.Decode(programResult, null, callInput, MAINNET_CHAIN_ID);
            var humanReadable = decoded.ToHumanReadableString();

            Assert.NotNull(humanReadable);
            Assert.Contains("EVM Execution Result", humanReadable);
            Assert.Contains("transfer", humanReadable.ToLower());
            Assert.Contains("SUCCESS", humanReadable);
        }

        [Fact]
        public void ShouldHandleUnknownContract()
        {
            var abiStorage = ABIInfoStorageFactory.CreateLocalOnly();
            var decoder = new ProgramResultDecoder(abiStorage);

            var unknownContract = "0x0000000000000000000000000000000000099999";
            var callInput = new CallInput
            {
                From = "0x0000000000000000000000000000000000000001",
                To = unknownContract,
                Data = "0xa9059cbb0000000000000000000000001234567890123456789012345678901234567890" +
                       "00000000000000000000000000000000000000000000000000000000000f4240"
            };

            var decoded = decoder.DecodeCall(callInput, MAINNET_CHAIN_ID, 0);

            Assert.False(decoded.IsDecoded);
            Assert.NotNull(decoded.RawInput);
            Assert.Null(decoded.Function);
        }

        [Fact]
        public void ShouldDecodeApproveCallWithSourcify()
        {
            var abiStorage = ABIInfoStorageFactory.CreateWithSourcifyOnly();
            var decoder = new ProgramResultDecoder(abiStorage);

            var spenderAddress = "0x1234567890123456789012345678901234567890";
            var approveData = "0x095ea7b3" +
                "000000000000000000000000" + spenderAddress.Substring(2) +
                "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

            var callInput = new CallInput
            {
                From = "0x0000000000000000000000000000000000000001",
                To = WETH_CONTRACT,
                Data = approveData
            };

            var decoded = decoder.DecodeCall(callInput, MAINNET_CHAIN_ID, 0);

            Assert.True(decoded.IsDecoded);
            Assert.NotNull(decoded.Function);
            Assert.Equal("approve", decoded.Function.Name);
            Assert.Equal(2, decoded.InputParameters.Count);

            var spenderParam = decoded.InputParameters[0];
            var valueParam = decoded.InputParameters[1];

            Assert.Equal("guy", spenderParam.Parameter.Name);
            Assert.Equal("wad", valueParam.Parameter.Name);
        }

        [Fact]
        public void ShouldDecodeBalanceOfCallWithSourcify()
        {
            var abiStorage = ABIInfoStorageFactory.CreateWithSourcifyOnly();
            var decoder = new ProgramResultDecoder(abiStorage);

            var ownerAddress = "0x1234567890123456789012345678901234567890";
            var balanceOfData = "0x70a08231" +
                "000000000000000000000000" + ownerAddress.Substring(2);

            var callInput = new CallInput
            {
                From = "0x0000000000000000000000000000000000000001",
                To = WETH_CONTRACT,
                Data = balanceOfData
            };

            var decoded = decoder.DecodeCall(callInput, MAINNET_CHAIN_ID, 0);

            Assert.True(decoded.IsDecoded);
            Assert.NotNull(decoded.Function);
            Assert.Equal("balanceOf", decoded.Function.Name);
            Assert.Single(decoded.InputParameters);
        }

        [Fact]
        public void ShouldCacheABIAfterFirstLookup()
        {
            var abiStorage = ABIInfoStorageFactory.CreateWithSourcifyOnly();
            var decoder = new ProgramResultDecoder(abiStorage);

            var callInput = new CallInput
            {
                From = "0x0000000000000000000000000000000000000001",
                To = WETH_CONTRACT,
                Data = "0x70a08231000000000000000000000000" + "1234567890123456789012345678901234567890"
            };

            var decoded1 = decoder.DecodeCall(callInput, MAINNET_CHAIN_ID, 0);
            Assert.True(decoded1.IsDecoded);

            var decoded2 = decoder.DecodeCall(callInput, MAINNET_CHAIN_ID, 0);
            Assert.True(decoded2.IsDecoded);
            Assert.Equal(decoded1.Function.Name, decoded2.Function.Name);
        }

        [Fact]
        public void ShouldDecodeProxyContractTransferCall()
        {
            var abiStorage = ABIInfoStorageFactory.CreateWithSourcifyOnly();
            var decoder = new ProgramResultDecoder(abiStorage);

            var toAddress = "0x1234567890123456789012345678901234567890";
            var transferData = "0xa9059cbb" +
                "000000000000000000000000" + toAddress.Substring(2) +
                "00000000000000000000000000000000000000000000000000000000000f4240";

            var callInput = new CallInput
            {
                From = "0x0000000000000000000000000000000000000001",
                To = USDC_CONTRACT,
                Data = transferData
            };

            var decoded = decoder.DecodeCall(callInput, MAINNET_CHAIN_ID, 0);

            Assert.True(decoded.IsDecoded, "Proxy contract call should be decoded using implementation ABI");
            Assert.NotNull(decoded.Function);
            Assert.Equal("transfer", decoded.Function.Name);
            Assert.Equal(2, decoded.InputParameters.Count);
        }

        [Fact]
        public void ShouldDecodeProxyContractTransferEvent()
        {
            var abiStorage = ABIInfoStorageFactory.CreateWithSourcifyOnly();
            var decoder = new ProgramResultDecoder(abiStorage);

            var transferEventSignature = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";
            var fromAddress = "0x0000000000000000000000001234567890123456789012345678901234567890";
            var toAddress = "0x0000000000000000000000000987654321098765432109876543210987654321";
            var amount = "0x00000000000000000000000000000000000000000000000000000000000f4240";

            var log = new FilterLog
            {
                Address = USDC_CONTRACT,
                Topics = new object[] {
                    transferEventSignature,
                    fromAddress,
                    toAddress
                },
                Data = amount,
                LogIndex = new HexBigInteger(0)
            };

            var decoded = decoder.DecodeLog(log, MAINNET_CHAIN_ID);

            Assert.True(decoded.IsDecoded, "Proxy contract event should be decoded using implementation ABI");
            Assert.NotNull(decoded.Event);
            Assert.Equal("Transfer", decoded.Event.Name);
            Assert.True(decoded.Parameters.Count >= 3);
        }

        [Fact]
        public void ShouldDecodeProxyContractApproveCall()
        {
            var abiStorage = ABIInfoStorageFactory.CreateWithSourcifyOnly();
            var decoder = new ProgramResultDecoder(abiStorage);

            var spenderAddress = "0x1234567890123456789012345678901234567890";
            var approveData = "0x095ea7b3" +
                "000000000000000000000000" + spenderAddress.Substring(2) +
                "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";

            var callInput = new CallInput
            {
                From = "0x0000000000000000000000000000000000000001",
                To = USDC_CONTRACT,
                Data = approveData
            };

            var decoded = decoder.DecodeCall(callInput, MAINNET_CHAIN_ID, 0);

            Assert.True(decoded.IsDecoded, "Proxy contract approve call should be decoded");
            Assert.NotNull(decoded.Function);
            Assert.Equal("approve", decoded.Function.Name);
            Assert.Equal(2, decoded.InputParameters.Count);
        }
    }
}
