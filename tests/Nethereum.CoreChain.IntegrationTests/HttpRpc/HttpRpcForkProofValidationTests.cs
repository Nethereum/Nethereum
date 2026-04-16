using System.Numerics;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Merkle.Patricia;
using Nethereum.RPC.Eth.ChainValidation;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Mappers;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.HttpRpc
{
    [Trait("Category", "Fork")]
    public class HttpRpcForkProofValidationTests : IClassFixture<DevChainForkHttpFixture>
    {
        private readonly DevChainForkHttpFixture _fixture;

        // totalSupply() selector
        private const string TotalSupplySelector = "0x18160ddd";
        // balanceOf(address) selector
        private const string BalanceOfSelector = "0x70a08231";

        public HttpRpcForkProofValidationTests(DevChainForkHttpFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ForkProof_USDC_AccountProofVerifies()
        {
            if (!_fixture.IsAvailable) return; // MAINNET_RPC_URL not set — skip

            // Touch USDC to trigger ForkingNodeDataService to fetch from mainnet
            await CallContractAsync(DevChainForkHttpFixture.UsdcAddress, TotalSupplySelector);

            // Force block production to compute state root over forked state
            await SendEthTransferAsync(DevChainForkHttpFixture.Address, 1);

            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                DevChainForkHttpFixture.UsdcAddress,
                Array.Empty<string>(),
                BlockParameter.CreateLatest());

            Assert.NotNull(proof);
            Assert.NotNull(proof.AccountProofs);
            Assert.NotEmpty(proof.AccountProofs);

            var block = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest());
            var stateRoot = block.StateRoot.HexToByteArray();

            var account = proof.ToAccount();
            var valid = AccountProofVerification.VerifyAccountProofs(
                DevChainForkHttpFixture.UsdcAddress, stateRoot,
                proof.AccountProofs.Select(x => x.HexToByteArray()), account);

            Assert.True(valid, "USDC account proof should verify cryptographically against fork state root");

            // USDC is a contract — it should have code
            Assert.NotEqual(
                "0xc5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470",
                proof.CodeHash.ToLowerInvariant());
        }

        [Fact]
        public async Task ForkProof_USDC_StorageProofVerifies()
        {
            if (!_fixture.IsAvailable) return; // MAINNET_RPC_URL not set — skip

            // Read totalSupply via eth_call
            var totalSupplyResult = await CallContractAsync(
                DevChainForkHttpFixture.UsdcAddress, TotalSupplySelector);

            // Force block production
            await SendEthTransferAsync(DevChainForkHttpFixture.Address, 1);

            // Get proof for storage slot 0x0 (common base slot)
            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                DevChainForkHttpFixture.UsdcAddress,
                new[] { "0x0" },
                BlockParameter.CreateLatest());

            Assert.NotNull(proof);
            Assert.NotEmpty(proof.StorageProof);

            var sp = proof.StorageProof[0];
            if (sp.Proof != null && sp.Proof.Count > 0)
            {
                var storageValid = StorageProofVerification.ValidateValueFromStorageProof(
                    sp.Key.HexValue.HexToByteArray(),
                    sp.Value.HexValue.HexToByteArray(),
                    sp.Proof.Select(x => x.HexToByteArray()).ToList(),
                    proof.StorageHash.HexToByteArray());
                Assert.True(storageValid, "USDC storage proof should verify cryptographically");
            }
        }

        [Fact]
        public async Task ForkProof_USDC_BalanceOfKnownHolder()
        {
            if (!_fixture.IsAvailable) return; // MAINNET_RPC_URL not set — skip

            // Use the fixture's own address as a test — after fork, touch USDC to populate state
            var testAddress = DevChainForkHttpFixture.Address;

            // Read balanceOf(testAddress) to trigger state loading
            var balanceOfData = BalanceOfSelector +
                testAddress.Replace("0x", "").PadLeft(64, '0');
            await CallContractAsync(DevChainForkHttpFixture.UsdcAddress, balanceOfData);

            // Compute the balanceOf mapping slot: keccak256(abi.encode(address, 9))
            // USDC uses slot 9 for _balances mapping (FiatTokenV2_1 proxy pattern)
            var sha3 = new Sha3Keccack();
            var slotKey = testAddress.Replace("0x", "").PadLeft(64, '0') +
                          new BigInteger(9).ToString("x64");
            var hashedSlot = sha3.CalculateHash(slotKey.HexToByteArray());
            var storageKey = "0x" + hashedSlot.ToHex();

            // Force block production
            await SendEthTransferAsync(DevChainForkHttpFixture.Address, 1);

            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                DevChainForkHttpFixture.UsdcAddress,
                new[] { storageKey },
                BlockParameter.CreateLatest());

            Assert.NotNull(proof);
            Assert.NotEmpty(proof.StorageProof);

            // Verify account proof
            var block = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest());
            var stateRoot = block.StateRoot.HexToByteArray();
            var account = proof.ToAccount();
            var accountValid = AccountProofVerification.VerifyAccountProofs(
                DevChainForkHttpFixture.UsdcAddress, stateRoot,
                proof.AccountProofs.Select(x => x.HexToByteArray()), account);
            Assert.True(accountValid, "Account proof should verify for USDC balanceOf query");
        }

        [Fact]
        public async Task ForkProof_MultipleForkAccounts_AllVerify()
        {
            if (!_fixture.IsAvailable) return; // MAINNET_RPC_URL not set — skip

            var contracts = new[]
            {
                DevChainForkHttpFixture.UsdcAddress,
                DevChainForkHttpFixture.WethAddress,
                DevChainForkHttpFixture.DaiAddress
            };

            // Touch all contracts to trigger state loading
            foreach (var addr in contracts)
            {
                await CallContractAsync(addr, TotalSupplySelector);
            }

            // Force block production
            await SendEthTransferAsync(DevChainForkHttpFixture.Address, 1);

            var block = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest());
            var stateRoot = block.StateRoot.HexToByteArray();

            foreach (var addr in contracts)
            {
                var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                    addr, Array.Empty<string>(), BlockParameter.CreateLatest());

                Assert.NotNull(proof);
                Assert.NotEmpty(proof.AccountProofs);

                var account = proof.ToAccount();
                var valid = AccountProofVerification.VerifyAccountProofs(
                    addr, stateRoot,
                    proof.AccountProofs.Select(x => x.HexToByteArray()), account);

                Assert.True(valid, $"Account proof for {addr} should verify against fork state root");
            }
        }

        [Fact]
        public async Task ForkProof_ProofValidationService_WorksWithForkedState()
        {
            if (!_fixture.IsAvailable) return; // MAINNET_RPC_URL not set — skip

            // Touch USDC to load state
            await CallContractAsync(DevChainForkHttpFixture.UsdcAddress, TotalSupplySelector);
            await SendEthTransferAsync(DevChainForkHttpFixture.Address, 1);

            var validationService = new EthChainProofValidationService(_fixture.Web3.Client);

            var accountProof = await validationService.GetAndValidateAccountProof(
                DevChainForkHttpFixture.UsdcAddress);

            Assert.NotNull(accountProof);
            Assert.NotNull(accountProof.AccountProofs);
            Assert.NotEmpty(accountProof.AccountProofs);
        }

        [Fact]
        public async Task CrossValidation_USDC_CodeHashMatchesMainnet()
        {
            if (!_fixture.IsAvailable) return;

            await CallContractAsync(DevChainForkHttpFixture.UsdcAddress, TotalSupplySelector);
            await SendEthTransferAsync(DevChainForkHttpFixture.Address, 1);

            var forkProof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                DevChainForkHttpFixture.UsdcAddress,
                Array.Empty<string>(),
                BlockParameter.CreateLatest());

            var mainnetBlockNumber = await _fixture.MainnetWeb3!.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var mainnetBlockParam = new BlockParameter(mainnetBlockNumber);

            var mainnetBlock = await _fixture.MainnetWeb3!.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(mainnetBlockParam);

            var mainnetProof = await _fixture.MainnetWeb3!.Eth.GetProof.SendRequestAsync(
                DevChainForkHttpFixture.UsdcAddress,
                Array.Empty<string>(),
                mainnetBlockParam);

            Assert.NotNull(forkProof);
            Assert.NotNull(mainnetProof);

            Assert.Equal(
                mainnetProof.CodeHash.ToLowerInvariant(),
                forkProof.CodeHash.ToLowerInvariant());

            Assert.Equal(mainnetProof.Nonce.Value, forkProof.Nonce.Value);

            var forkBlock = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest());
            var forkAccount = forkProof.ToAccount();
            var forkValid = AccountProofVerification.VerifyAccountProofs(
                DevChainForkHttpFixture.UsdcAddress, forkBlock.StateRoot.HexToByteArray(),
                forkProof.AccountProofs.Select(x => x.HexToByteArray()), forkAccount);
            Assert.True(forkValid, "Fork proof should verify against fork state root");

            var mainnetAccount = mainnetProof.ToAccount();
            var mainnetValid = AccountProofVerification.VerifyAccountProofs(
                DevChainForkHttpFixture.UsdcAddress, mainnetBlock.StateRoot.HexToByteArray(),
                mainnetProof.AccountProofs.Select(x => x.HexToByteArray()), mainnetAccount);
            Assert.True(mainnetValid, "Mainnet proof should verify against mainnet state root");
        }

        [Fact]
        public async Task CrossValidation_MultipleContracts_CodeHashesMatch()
        {
            if (!_fixture.IsAvailable) return;

            var contracts = new[]
            {
                DevChainForkHttpFixture.UsdcAddress,
                DevChainForkHttpFixture.WethAddress,
                DevChainForkHttpFixture.DaiAddress
            };

            foreach (var addr in contracts)
            {
                await CallContractAsync(addr, TotalSupplySelector);
            }
            await SendEthTransferAsync(DevChainForkHttpFixture.Address, 1);

            foreach (var addr in contracts)
            {
                var forkProof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                    addr, Array.Empty<string>(), BlockParameter.CreateLatest());
                var mainnetProof = await _fixture.MainnetWeb3!.Eth.GetProof.SendRequestAsync(
                    addr, Array.Empty<string>(), BlockParameter.CreateLatest());

                Assert.Equal(
                    mainnetProof.CodeHash.ToLowerInvariant(),
                    forkProof.CodeHash.ToLowerInvariant());

                Assert.Equal(mainnetProof.Nonce.Value, forkProof.Nonce.Value);
            }
        }

        [Fact]
        public async Task CrossValidation_USDC_TotalSupplyMatchesMainnet()
        {
            if (!_fixture.IsAvailable) return;

            var forkTotalSupply = await CallContractAsync(
                DevChainForkHttpFixture.UsdcAddress, TotalSupplySelector);

            var mainnetCallInput = new CallInput
            {
                To = DevChainForkHttpFixture.UsdcAddress,
                Data = TotalSupplySelector
            };
            var mainnetTotalSupply = await _fixture.MainnetWeb3!.Eth.Transactions.Call
                .SendRequestAsync(mainnetCallInput);

            Assert.NotNull(forkTotalSupply);
            Assert.NotNull(mainnetTotalSupply);

            var forkValue = new HexBigInteger(forkTotalSupply).Value;
            var mainnetValue = new HexBigInteger(mainnetTotalSupply).Value;

            Assert.True(forkValue > 0, "Fork USDC totalSupply should be non-zero");
            Assert.True(mainnetValue > 0, "Mainnet USDC totalSupply should be non-zero");

            var diff = BigInteger.Abs(forkValue - mainnetValue);
            var tolerance = mainnetValue / 100;
            Assert.True(diff <= tolerance,
                $"Fork totalSupply ({forkValue}) should be within 1% of mainnet ({mainnetValue}), diff={diff}");
        }

        [Fact]
        public async Task CrossValidation_MainnetProof_VerifiesIndependently()
        {
            if (!_fixture.IsAvailable) return;

            var blockNumber = await _fixture.MainnetWeb3!.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var blockParam = new BlockParameter(blockNumber);

            var mainnetBlock = await _fixture.MainnetWeb3!.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(blockParam);
            var mainnetStateRoot = mainnetBlock.StateRoot.HexToByteArray();

            var mainnetProof = await _fixture.MainnetWeb3!.Eth.GetProof.SendRequestAsync(
                DevChainForkHttpFixture.UsdcAddress,
                Array.Empty<string>(),
                blockParam);

            Assert.NotNull(mainnetProof);
            Assert.NotEmpty(mainnetProof.AccountProofs);

            var mainnetAccount = mainnetProof.ToAccount();
            var mainnetValid = AccountProofVerification.VerifyAccountProofs(
                DevChainForkHttpFixture.UsdcAddress, mainnetStateRoot,
                mainnetProof.AccountProofs.Select(x => x.HexToByteArray()), mainnetAccount);
            Assert.True(mainnetValid, "Mainnet USDC proof should verify against mainnet state root");

            Assert.NotEqual(
                "0xc5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470",
                mainnetProof.CodeHash.ToLowerInvariant());
        }

        private async Task<string> CallContractAsync(string to, string data)
        {
            var callInput = new CallInput
            {
                To = to,
                Data = data
            };
            return await _fixture.Web3.Eth.Transactions.Call.SendRequestAsync(callInput);
        }

        private async Task SendEthTransferAsync(string to, BigInteger value)
        {
            var txInput = new TransactionInput
            {
                From = _fixture.Account.Address,
                To = to,
                Value = new HexBigInteger(value),
                Gas = new HexBigInteger(21000)
            };
            await _fixture.Web3.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(txInput);
        }
    }
}
