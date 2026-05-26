using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.AppChain.Anchoring.AppChainAnchor;
using Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition;
using Nethereum.AppChain.Anchoring.AppChainProofManager;
using Nethereum.AppChain.Anchoring.AppChainProofManager.ContractDefinition;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.DevChain;
using Nethereum.DevChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Web3;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests
{
    [Collection(DevChainAnchorFixture.COLLECTION_NAME)]
    public class AppChainProofManagerTests
    {
        private readonly DevChainAnchorFixture _fixture;
        private readonly ITestOutputHelper _output;
        private static int _testCounter;

        public AppChainProofManagerTests(DevChainAnchorFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        private async Task<(ulong chainId, byte[] genesisHash)> RegisterChain(byte[] genesisStateRoot)
        {
            var id = Interlocked.Increment(ref _testCounter);
            var genesisHash = new Nethereum.Util.Sha3Keccack()
                .CalculateHash(System.Text.Encoding.UTF8.GetBytes($"pm-chain-{id}-{DateTime.UtcNow.Ticks}"));
            var chainId = (ulong)(40000 + id);

            await _fixture.AnchorService.RegisterAppChainRequestAndWaitForReceiptAsync(
                new RegisterAppChainFunction
                {
                    ChainId = chainId, GenesisHash = genesisHash, GenesisBlock = 1,
                    GenesisStateRoot = genesisStateRoot,
                    MinimumProofSystem = 0, MinimumAnchorVersion = 1,
                    Authority = _fixture.AuthorityService.ContractAddress
                });
            await _fixture.AuthorityService.SetOperatorRequestAndWaitForReceiptAsync(
                new Nethereum.AppChain.Anchoring.SimpleAuthority.ContractDefinition.SetOperatorFunction
                { ChainId = chainId, NewOperator = _fixture.OperatorAccount.Address });
            return (chainId, genesisHash);
        }

        private async Task<DevChainNode> ProduceL2(int blocks)
        {
            var l2 = DevChainNode.CreateInMemory(new DevChainConfig
            { ChainId = 31337, BlockGasLimit = 30_000_000, AutoMine = false });
            await l2.StartAsync(new[] { _fixture.OperatorAccount.Address });
            var signer = new Nethereum.Signer.LegacyTransactionSigner();
            var pkBytes = _fixture.OperatorPrivateKey.Substring(2).HexToByteArray();
            ulong n = 0;
            for (int b = 0; b < blocks; b++)
            {
                var txHex = signer.SignTransaction(pkBytes, (BigInteger)31337,
                    $"0x{(b + 1):x40}", 1000, n++, 1_000_000_000, 21_000, "");
                await l2.SendTransactionAsync(Nethereum.Model.TransactionFactory.CreateTransaction(txHex));
                await l2.MineBlockAsync();
            }
            return l2;
        }

        // ═══════════════════════════════════════════
        //  CONFIGURATION
        // ═══════════════════════════════════════════

        [Fact]
        public async Task Config_SetProofBond_PerChain()
        {
            var l2 = await ProduceL2(5);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, _) = await RegisterChain(block1.StateRoot);

                var bond = Web3.Web3.Convert.ToWei(0.05m);
                await _fixture.ProofManagerService.SetProofBondRequestAndWaitForReceiptAsync(
                    new SetProofBondFunction { ChainId = chainId, NewBond = bond });

                var stored = await _fixture.ProofManagerService.ProofBondQueryAsync(chainId);
                Assert.Equal(bond, stored);
                _output.WriteLine($"Bond set to {bond} wei for chain {chainId}");
            }
            finally { l2.Dispose(); }
        }

        [Fact]
        public async Task Config_AuthorizeAndRevokeProver()
        {
            var l2 = await ProduceL2(5);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, _) = await RegisterChain(block1.StateRoot);

                await _fixture.AuthorityService.AuthorizeProverRequestAndWaitForReceiptAsync(
                    new Nethereum.AppChain.Anchoring.SimpleAuthority.ContractDefinition.AuthorizeProverFunction
                    { ChainId = chainId, Prover = _fixture.ChallengerAccount.Address });
                Assert.True(await _fixture.AuthorityService.AuthorizedProversQueryAsync(
                    chainId, _fixture.ChallengerAccount.Address));

                await _fixture.AuthorityService.RevokeProverRequestAndWaitForReceiptAsync(
                    new Nethereum.AppChain.Anchoring.SimpleAuthority.ContractDefinition.RevokeProverFunction
                    { ChainId = chainId, Prover = _fixture.ChallengerAccount.Address });
                Assert.False(await _fixture.AuthorityService.AuthorizedProversQueryAsync(
                    chainId, _fixture.ChallengerAccount.Address));
            }
            finally { l2.Dispose(); }
        }

        [Fact]
        public async Task Config_RejectNonOperatorConfig()
        {
            var l2 = await ProduceL2(5);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, _) = await RegisterChain(block1.StateRoot);

                var challengerPM = _fixture.CreateProofManagerServiceAs(_fixture.ChallengerWeb3);
                await Assert.ThrowsAsync<SmartContractRevertException>(
                    () => challengerPM.SetProofBondRequestAndWaitForReceiptAsync(
                        new SetProofBondFunction { ChainId = chainId, NewBond = 1000 }));
            }
            finally { l2.Dispose(); }
        }

        // ═══════════════════════════════════════════
        //  PROOF REQUESTS
        // ═══════════════════════════════════════════

        [Fact]
        public async Task Request_PostBond_Success()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(5);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, genesis) = await RegisterChain(block1.StateRoot);

                var builder = new AnchorBuilder(chainId, genesis, 1, 0,
                    initialPreviousPostStateRoot: block1.StateRoot);
                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(
                    await builder.BuildAsync(l2, 1, 5, ws));

                var bond = Web3.Web3.Convert.ToWei(0.05m);
                await _fixture.ProofManagerService.SetProofBondRequestAndWaitForReceiptAsync(
                    new SetProofBondFunction { ChainId = chainId, NewBond = bond });

                var reqFn = new RequestBlockProofFunction { ChainId = chainId, BlockNumber = 3 };
                reqFn.AmountToSend = bond;
                var challengerPM = _fixture.CreateProofManagerServiceAs(_fixture.ChallengerWeb3);
                await challengerPM.RequestBlockProofRequestAndWaitForReceiptAsync(reqFn);

                var req = await _fixture.ProofManagerService.ProofRequestsQueryAsync(chainId, 3);
                Assert.Equal(_fixture.ChallengerAccount.Address.ToLower(), req.Requester.ToLower());
                Assert.Equal(bond, req.Bond);
                Assert.False(req.Fulfilled);
                _output.WriteLine("Proof requested for block 3 with bond");
            }
            finally { l2.Dispose(); }
        }

        [Fact]
        public async Task Request_RejectWrongBond()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(5);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, genesis) = await RegisterChain(block1.StateRoot);

                var builder = new AnchorBuilder(chainId, genesis, 1, 0,
                    initialPreviousPostStateRoot: block1.StateRoot);
                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(
                    await builder.BuildAsync(l2, 1, 5, ws));

                var bond = Web3.Web3.Convert.ToWei(0.05m);
                await _fixture.ProofManagerService.SetProofBondRequestAndWaitForReceiptAsync(
                    new SetProofBondFunction { ChainId = chainId, NewBond = bond });

                var reqFn = new RequestBlockProofFunction { ChainId = chainId, BlockNumber = 3 };
                reqFn.AmountToSend = Web3.Web3.Convert.ToWei(0.01m);
                await Assert.ThrowsAsync<SmartContractRevertException>(
                    () => _fixture.ProofManagerService.RequestBlockProofRequestAndWaitForReceiptAsync(reqFn));
            }
            finally { l2.Dispose(); }
        }

        // ═══════════════════════════════════════════
        //  TIMEOUT
        // ═══════════════════════════════════════════

        [Fact]
        public async Task Timeout_BondReturnedToRequester()
        {
            var ws = new InMemoryWitnessStore();
            var l2 = await ProduceL2(5);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, genesis) = await RegisterChain(block1.StateRoot);

                var builder = new AnchorBuilder(chainId, genesis, 1, 0,
                    initialPreviousPostStateRoot: block1.StateRoot);
                await _fixture.AnchorService.SubmitAnchorRequestAndWaitForReceiptAsync(
                    await builder.BuildAsync(l2, 1, 5, ws));

                var bond = Web3.Web3.Convert.ToWei(0.05m);
                await _fixture.ProofManagerService.SetProofBondRequestAndWaitForReceiptAsync(
                    new SetProofBondFunction { ChainId = chainId, NewBond = bond });

                var challengerPM = _fixture.CreateProofManagerServiceAs(_fixture.ChallengerWeb3);
                var reqFn = new RequestBlockProofFunction { ChainId = chainId, BlockNumber = 3 };
                reqFn.AmountToSend = bond;
                await challengerPM.RequestBlockProofRequestAndWaitForReceiptAsync(reqFn);

                _fixture.L1Node.DevConfig.AddTimeOffset(86401);
                await _fixture.L1Node.MineBlockAsync();

                try
                {
                    var challengerBalBefore = await _fixture.L1Node.GetBalanceAsync(_fixture.ChallengerAccount.Address);
                    var claimFn = new ClaimProofTimeoutFunction { ChainId = chainId, BlockNumber = 3 };
                    claimFn.Gas = 200000;
                    await _fixture.ProofManagerService.ClaimProofTimeoutRequestAndWaitForReceiptAsync(claimFn);

                    var pending = await _fixture.ProofManagerService.PendingWithdrawalsQueryAsync(
                        _fixture.ChallengerAccount.Address);
                    Assert.Equal(bond, pending);
                    _output.WriteLine($"Timeout claimed, bond {pending} in pending withdrawals");
                }
                finally
                {
                    _fixture.L1Node.DevConfig.AddTimeOffset(-86401);
                }
            }
            finally { l2.Dispose(); }
        }

        // ═══════════════════════════════════════════
        //  QUERIES
        // ═══════════════════════════════════════════

        [Fact]
        public async Task Query_IsBlockProven_FalseByDefault()
        {
            var l2 = await ProduceL2(5);
            try
            {
                var block1 = await l2.GetBlockByNumberAsync(1);
                var (chainId, _) = await RegisterChain(block1.StateRoot);

                var proven = await _fixture.ProofManagerService.IsBlockProvenQueryAsync(chainId, 3);
                Assert.False(proven);
            }
            finally { l2.Dispose(); }
        }
    }
}
