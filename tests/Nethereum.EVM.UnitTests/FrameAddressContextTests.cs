using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    public class FrameAddressContextTests
    {
        private const string Origin = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b";
        private const string Contract = "0xcccccccccccccccccccccccccccccccccccccccc";
        private const string Coinbase = "0x000000000000000000000000000000000000c0b0";
        private const string Echo = "0x1111111111111111111111111111111111111111";

        [Fact]
        public async Task Given_ARootFrame_When_CallerOriginAddressAndCoinbaseAreRead_Then_EachIsItsAddressAsAWord()
        {
            var program = await Run("33" + "32" + "30" + "41");

            Assert.Equal(EvmUInt256.FromHex(Origin), program.StackPeekAtU256(3));
            Assert.Equal(EvmUInt256.FromHex(Origin), program.StackPeekAtU256(2));
            Assert.Equal(EvmUInt256.FromHex(Contract), program.StackPeekAtU256(1));
            Assert.Equal(EvmUInt256.FromHex(Coinbase), program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Given_AChildFrame_When_ItReadsCaller_Then_ItIsTheCallingContractAndNotTheTransactionSender()
        {
            var program = await Run("6020" + "6000" + "6000" + "6000" + "73" + Echo.Substring(2) + "5a" + "fa" + "50" + "6000" + "51");

            Assert.Equal(EvmUInt256.FromHex(Contract), program.StackPeekAtU256(0));
        }

        [Fact]
        public async Task Given_AChildFrame_When_ItReadsOrigin_Then_ItIsStillTheTransactionSender()
        {
            var program = await Run("6020" + "6000" + "6000" + "6000" + "73" + Echo.Substring(2) + "5a" + "fa" + "50" + "6000" + "51", echoOpcode: "32");

            Assert.Equal(EvmUInt256.FromHex(Origin), program.StackPeekAtU256(0));
        }

        private static async Task<Program> Run(string callerCodeHex, string echoOpcode = "33")
        {
            var executionState = new ExecutionStateService(new MockNodeDataService());
            var echo = executionState.CreateOrGetAccountExecutionState(Echo);
            echo.Code = (echoOpcode + "6000" + "52" + "6020" + "6000" + "f3").HexToByteArray();
            echo.Balance.SetInitialChainBalance(1);

            var transaction = new TransactionInput
            {
                From = Origin,
                To = Contract,
                Value = new Nethereum.Hex.HexTypes.HexBigInteger(0),
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(1000000),
                GasPrice = new Nethereum.Hex.HexTypes.HexBigInteger(10),
                Data = "0x",
                ChainId = new Nethereum.Hex.HexTypes.HexBigInteger(1)
            };
            var context = new ProgramContext(transaction, executionState, null,
                blockNumber: 1, timestamp: 1000, coinbase: Coinbase, baseFee: 10);
            var program = new Program(callerCodeHex.HexToByteArray(), context);
            return await new EVMSimulator(DefaultHardforkConfigs.Cancun).ExecuteWithCallStackAsync(program, traceEnabled: false);
        }
    }
}
