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
using Nethereum.Merkle.Patricia.ProofVerification;

namespace Nethereum.CoreChain.IntegrationTests.HttpRpc
{
    [Trait("Category", "Fork")]
    public class HttpRpcForkProofValidationTests : IClassFixture<DevChainForkHttpFixture>
    {
        private readonly DevChainForkHttpFixture _fixture;

        private const string TotalSupplySelector = "0x18160ddd";
        private const string BalanceOfSelector = "0x70a08231";

        public HttpRpcForkProofValidationTests(DevChainForkHttpFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ForkProof_USDC_AccountProofVerifies()
        {
            if (!_fixture.IsAvailable) return;

            await CallContractAsync(DevChainForkHttpFixture.UsdcAddress, TotalSupplySelector);

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
            var valid = ProofVerification.Current.Account.Verify(
                stateRoot, proof.AccountProofs.Select(x => x.HexToByteArray()),
                DevChainForkHttpFixture.UsdcAddress, account);

            Assert.True(valid, "USDC account proof should verify cryptographically against fork state root");

            Assert.NotEqual(
                "0xc5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470",
                proof.CodeHash.ToLowerInvariant());
        }

        [Fact]
        public async Task ForkProof_USDC_StorageProofVerifies()
        {
            if (!_fixture.IsAvailable) return;

            var totalSupplyResult = await CallContractAsync(
                DevChainForkHttpFixture.UsdcAddress, TotalSupplySelector);

            await SendEthTransferAsync(DevChainForkHttpFixture.Address, 1);

            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                DevChainForkHttpFixture.UsdcAddress,
                new[] { "0x0" },
                BlockParameter.CreateLatest());

            Assert.NotNull(proof);
            Assert.NotEmpty(proof.StorageProof);

            var sp = proof.StorageProof[0];
            if (sp.Proof != null && sp.Proof.Count > 0)
            {
                var storageValid = ProofVerification.Current.Storage.Verify(
                    proof.StorageHash.HexToByteArray(),
                    sp.Proof.Select(x => x.HexToByteArray()).ToList(),
                    sp.Key.HexValue.HexToByteArray(),
                    sp.Value.HexValue.HexToByteArray());
                Assert.True(storageValid, "USDC storage proof should verify cryptographically");
            }
        }

        [Fact]
        public async Task ForkProof_USDC_BalanceOfKnownHolder()
        {
            if (!_fixture.IsAvailable) return;

            var testAddress = DevChainForkHttpFixture.Address;

            var balanceOfData = BalanceOfSelector +
                testAddress.Replace("0x", "").PadLeft(64, '0');
            await CallContractAsync(DevChainForkHttpFixture.UsdcAddress, balanceOfData);

            var sha3 = new Sha3Keccack();
            var slotKey = testAddress.Replace("0x", "").PadLeft(64, '0') +
                          new BigInteger(9).ToString("x64");
            var hashedSlot = sha3.CalculateHash(slotKey.HexToByteArray());
            var storageKey = "0x" + hashedSlot.ToHex();

            await SendEthTransferAsync(DevChainForkHttpFixture.Address, 1);

            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                DevChainForkHttpFixture.UsdcAddress,
                new[] { storageKey },
                BlockParameter.CreateLatest());

            Assert.NotNull(proof);
            Assert.NotEmpty(proof.StorageProof);

            var block = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest());
            var stateRoot = block.StateRoot.HexToByteArray();
            var account = proof.ToAccount();
            var accountValid = ProofVerification.Current.Account.Verify(
                stateRoot, proof.AccountProofs.Select(x => x.HexToByteArray()),
                DevChainForkHttpFixture.UsdcAddress, account);
            Assert.True(accountValid, "Account proof should verify for USDC balanceOf query");
        }

        [Fact]
        public async Task ForkProof_MultipleForkAccounts_AllVerify()
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
                var valid = ProofVerification.Current.Account.Verify(
                    stateRoot, proof.AccountProofs.Select(x => x.HexToByteArray()),
                    addr, account);

                Assert.True(valid, $"Account proof for {addr} should verify against fork state root");
            }
        }

        [Fact]
        public async Task ForkProof_ProofValidationService_WorksWithForkedState()
        {
            if (!_fixture.IsAvailable) return;

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
            var forkValid = ProofVerification.Current.Account.Verify(
                forkBlock.StateRoot.HexToByteArray(), forkProof.AccountProofs.Select(x => x.HexToByteArray()),
                DevChainForkHttpFixture.UsdcAddress, forkAccount);
            Assert.True(forkValid, "Fork proof should verify against fork state root");

            var mainnetAccount = mainnetProof.ToAccount();
            var mainnetValid = ProofVerification.Current.Account.Verify(
                mainnetBlock.StateRoot.HexToByteArray(), mainnetProof.AccountProofs.Select(x => x.HexToByteArray()),
                DevChainForkHttpFixture.UsdcAddress, mainnetAccount);
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
            var mainnetValid = ProofVerification.Current.Account.Verify(
                mainnetStateRoot, mainnetProof.AccountProofs.Select(x => x.HexToByteArray()),
                DevChainForkHttpFixture.UsdcAddress, mainnetAccount);
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
                Value = new HexBigInteger(value)
            };
            txInput.Gas = await _fixture.Web3.Eth.TransactionManager.EstimateGasAsync(txInput);
            var receipt = await _fixture.Web3.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(txInput);
            Assert.False(receipt.HasErrors() == true, $"Transfer of {value} wei to {to} failed in block {receipt.BlockNumber.Value}");
        }
    }
}
