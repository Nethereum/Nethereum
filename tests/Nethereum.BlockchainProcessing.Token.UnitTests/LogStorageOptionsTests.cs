using Nethereum.BlockchainProcessing.BlockProcessing;

namespace Nethereum.BlockchainProcessing.Token.UnitTests
{
    public class LogStorageOptionsTests
    {
        [Fact]
        public void ShouldStoreLog_AllMode_ReturnsTrue()
        {
            var options = new LogStorageOptions { Mode = LogStorageMode.All };
            Assert.True(options.ShouldStoreLog("0xanysig", "0xanycontract"));
        }

        [Fact]
        public void ShouldStoreLog_SelectiveMode_MatchesDefaultSignatures()
        {
            var options = new LogStorageOptions { Mode = LogStorageMode.Selective };
            var transferSig = "0xddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef";

            Assert.True(options.ShouldStoreLog(transferSig, "0xanycontract"));
        }

        [Fact]
        public void ShouldStoreLog_SelectiveMode_RejectsUnknownSignature()
        {
            var options = new LogStorageOptions { Mode = LogStorageMode.Selective };

            Assert.False(options.ShouldStoreLog("0x1234", "0xanycontract"));
        }

        [Fact]
        public void ShouldStoreLog_SelectiveMode_MatchesCaseInsensitive()
        {
            var options = new LogStorageOptions { Mode = LogStorageMode.Selective };
            var transferSigUpper = "0xDDF252AD1BE2C89B69C2B068FC378DAA952BA7F163C4A11628F55A4DF523B3EF";

            Assert.True(options.ShouldStoreLog(transferSigUpper, "0xanycontract"));
        }

        [Fact]
        public void ShouldStoreLog_SelectiveMode_ReturnsFalseForNullSignature()
        {
            var options = new LogStorageOptions { Mode = LogStorageMode.Selective };

            Assert.False(options.ShouldStoreLog(null, "0xanycontract"));
            Assert.False(options.ShouldStoreLog("", "0xanycontract"));
        }

        [Fact]
        public void ShouldStoreLog_ContractOverrides_MatchesPerContract()
        {
            var options = new LogStorageOptions
            {
                Mode = LogStorageMode.Selective,
                EventSignatures = new System.Collections.Generic.List<string>(),
                ContractOverrides = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>
                {
                    ["0xmycontract"] = new System.Collections.Generic.List<string> { "0xcustomsig" }
                }
            };

            Assert.True(options.ShouldStoreLog("0xcustomsig", "0xMyContract"));
            Assert.False(options.ShouldStoreLog("0xcustomsig", "0xothercontract"));
        }

        [Fact]
        public void ShouldStoreLog_SelectiveMode_IncludesTransferSingleAndBatch()
        {
            var options = new LogStorageOptions { Mode = LogStorageMode.Selective };
            var transferSingleSig = "0xc3d58168c5ae7397731d063d5bbf3d657854427343f4c083240f7aacaa2d0f62";
            var transferBatchSig = "0x4a39dc06d4c0dbc64b70af90fd698a233a518aa5d07e595d983b8c0526c8f7fb";

            Assert.True(options.ShouldStoreLog(transferSingleSig, "0xany"));
            Assert.True(options.ShouldStoreLog(transferBatchSig, "0xany"));
        }
    }
}
