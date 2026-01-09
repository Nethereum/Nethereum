using Nethereum.ABI.ABIRepository;
using Nethereum.DataServices.ABIInfoStorage;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Decoding;
using Nethereum.EVM.StateChanges;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Uniswap.UniversalRouter;
using Nethereum.Uniswap.UniversalRouter.Commands;
using Nethereum.Uniswap.V3;
using Nethereum.Util;
using Nethereum.XUnitEthereumClients;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.EVM
{
    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class StateChangesExtractorIntegrationTests
    {
        private readonly EthereumClientIntegrationFixture _fixture;

        private const string VITALIK = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";
        private const string WETH = "0xC02aaA39b223FE8D0A0e5C4F27eAD9083C756Cc2";
        private const string USDC = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
        private const string UNIVERSAL_ROUTER = "0x66a9893cc07d91d95644aedd05d03f95e1dba8af";
        private const string ADDRESS_THIS = "0x0000000000000000000000000000000000000002";

        public StateChangesExtractorIntegrationTests(EthereumClientIntegrationFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ShouldExtractStateChangesFromUniswapSwapSimulation()
        {
            var web3 = _fixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var amountIn = UnitConversion.Convert.ToWei(0.1m);

            var path = V3PathEncoder.EncodePath(WETH, 500, USDC);

            var builder = new UniversalRouterBuilder();
            builder.AddCommand(new WrapEthCommand
            {
                Recipient = ADDRESS_THIS,
                Amount = amountIn
            });
            builder.AddCommand(new V3SwapExactInCommand
            {
                Recipient = VITALIK,
                AmountIn = amountIn,
                AmountOutMinimum = 0,
                Path = path,
                FundsFromPermit2OrUniversalRouter = true
            });

            var executeFunction = builder.GetExecuteFunction(amountIn);
            var callInput = executeFunction.CreateCallInput(UNIVERSAL_ROUTER);
            callInput.From = VITALIK;
            callInput.Gas = new HexBigInteger(500000);
            callInput.GasPrice = new HexBigInteger(UnitConversion.Convert.ToWei(50, UnitConversion.EthUnit.Gwei));
            callInput.ChainId = new HexBigInteger(1);

            var blockNumber = await web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var block = await web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber.SendRequestAsync(blockNumber);
            var code = await web3.Eth.GetCode.SendRequestAsync(UNIVERSAL_ROUTER);

            var nodeDataService = new RpcNodeDataService(web3.Eth, new BlockParameter(blockNumber));
            var executionStateService = new ExecutionStateService(nodeDataService);

            var programContext = new ProgramContext(
                callInput,
                executionStateService,
                null,
                null,
                (long)blockNumber.Value,
                (long)block.Timestamp.Value);

            var program = new Program(code.HexToByteArray(), programContext);
            var evmSimulator = new EVMSimulator();

            program = await evmSimulator.ExecuteAsync(program, 0, 0, true);

            // Use SourcifyABIInfoStorage to fetch real ABIs (no fallbacks - Sourcify only)
            var abiStorage = new SourcifyABIInfoStorage();

            // Pre-fetch ABIs for all contracts involved in the swap
            var addressesToFetch = new[] { UNIVERSAL_ROUTER, WETH, USDC };
            await Task.WhenAll(addressesToFetch.Select(addr =>
                abiStorage.GetABIInfoAsync(1, addr)));

            // Also collect and fetch addresses from logs
            foreach (var log in program.ProgramResult.Logs)
            {
                if (!string.IsNullOrEmpty(log.Address))
                    await abiStorage.GetABIInfoAsync(1, log.Address);
            }

            var decoder = new ProgramResultDecoder(abiStorage);
            var decodedResult = decoder.Decode(program, callInput, BigInteger.One);

            Assert.NotNull(decodedResult);

            // Verify events are decoded with proper parameter names (not generic param_1_address style)
            var decodedLogs = decodedResult.DecodedLogs.Where(l => l.IsDecoded).ToList();
            Assert.True(decodedLogs.Count > 0, "Expected at least one decoded event");

            foreach (var log in decodedLogs)
            {
                var paramNames = log.Parameters?.Select(p => p.Parameter?.Name).Where(n => n != null).ToList();
                if (paramNames != null && paramNames.Count > 0)
                {
                    // Output event info for verification
                    System.Diagnostics.Debug.WriteLine($"Event: {log.Event?.Name} - Parameters: {string.Join(", ", paramNames)}");

                    Assert.False(paramNames.Any(n => n.StartsWith("param_")),
                        $"Event '{log.Event?.Name}' has generic parameter names: {string.Join(", ", paramNames)}. " +
                        "This indicates the ABI was not loaded from Sourcify.");
                }
            }

            // Output ALL logs (decoded and not) for debugging
            var allLogsInfo = string.Join("\n", decodedResult.DecodedLogs.Select(l =>
                $"  {(l.IsDecoded ? l.Event?.Name : "NOT DECODED")}: {string.Join(", ", l.Parameters?.Select(p => p.Parameter?.Name) ?? new string[0])}"));

            // Check if there are undecoded logs
            var undecodedLogs = decodedResult.DecodedLogs.Where(l => !l.IsDecoded).ToList();

            // For now, just verify we have properly decoded events with real parameter names
            Assert.True(decodedLogs.Count > 0,
                $"Expected decoded events. Total logs: {decodedResult.DecodedLogs.Count}, Decoded: {decodedLogs.Count}\n{allLogsInfo}");

            // Verify the Deposit event has real parameter names (not generic)
            var depositLog = decodedLogs.FirstOrDefault(l => l.Event?.Name == "Deposit");
            if (depositLog != null)
            {
                var paramNames = depositLog.Parameters?.Select(p => p.Parameter?.Name).ToList();
                Assert.Contains("dst", paramNames);
                Assert.Contains("wad", paramNames);
            }

            var extractor = new StateChangesExtractor();
            var stateChanges = extractor.ExtractFromDecodedResult(
                decodedResult,
                executionStateService,
                VITALIK);

            Assert.False(stateChanges.HasError);

            if (!program.ProgramResult.IsRevert)
            {
                Assert.True(stateChanges.HasBalanceChanges);

                var vitalikChanges = stateChanges.BalanceChanges.Where(c => c.IsCurrentUser).ToList();
                Assert.True(vitalikChanges.Count > 0);

                var ethChange = vitalikChanges.FirstOrDefault(c => c.Type == BalanceChangeType.Native);
                if (ethChange != null)
                {
                    Assert.True(ethChange.Change < 0);
                }

                var usdcChange = vitalikChanges.FirstOrDefault(c =>
                    c.Type == BalanceChangeType.ERC20 &&
                    c.TokenAddress?.ToLowerInvariant() == USDC.ToLowerInvariant());
                if (usdcChange != null)
                {
                    Assert.True(usdcChange.Change > 0);
                }
            }
        }

        [Fact]
        public async Task ShouldExtractStateChangesFromSimpleEthTransfer()
        {
            var web3 = _fixture.GetInfuraWeb3(InfuraNetwork.Mainnet);
            var recipient = "0x0000000000000000000000000000000000000001";
            var amount = UnitConversion.Convert.ToWei(1);

            var callInput = new CallInput
            {
                From = VITALIK,
                To = recipient,
                Value = new HexBigInteger(amount),
                Gas = new HexBigInteger(21000)
            };

            var decodedResult = new DecodedProgramResult
            {
                RootCall = new DecodedCall
                {
                    From = VITALIK,
                    To = recipient,
                    Value = amount,
                    IsDecoded = true
                },
                DecodedLogs = new System.Collections.Generic.List<DecodedLog>()
            };

            var extractor = new StateChangesExtractor();
            var stateChanges = extractor.ExtractFromDecodedResult(
                decodedResult,
                null,
                VITALIK);

            Assert.False(stateChanges.HasError);
            Assert.True(stateChanges.HasBalanceChanges);
            Assert.Equal(2, stateChanges.BalanceChanges.Count);

            var senderChange = stateChanges.BalanceChanges.FirstOrDefault(c => c.IsCurrentUser);
            Assert.NotNull(senderChange);
            Assert.Equal(BalanceChangeType.Native, senderChange.Type);
            Assert.Equal(-amount, senderChange.Change);

            var recipientChange = stateChanges.BalanceChanges.FirstOrDefault(c => !c.IsCurrentUser);
            Assert.NotNull(recipientChange);
            Assert.Equal(BalanceChangeType.Native, recipientChange.Type);
            Assert.Equal(amount, recipientChange.Change);
        }
    }
}
