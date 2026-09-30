using System.Numerics;
using Nethereum.CoreChain.IntegrationTests.Contracts;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Merkle.Patricia;
using Nethereum.RPC.Eth.ChainValidation;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Mappers;
using Nethereum.Signer;
using Xunit;
using Nethereum.Merkle.Patricia.ProofVerification;

namespace Nethereum.CoreChain.IntegrationTests.HttpRpc
{
    [Collection("HttpRpc")]
    public class HttpRpcProofValidationTests : IClassFixture<DevChainHttpFixture>
    {
        private readonly DevChainHttpFixture _fixture;
        private static readonly BigInteger OneToken = BigInteger.Parse("1000000000000000000");

        public HttpRpcProofValidationTests(DevChainHttpFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task AccountProof_VerifiesCryptographically()
        {
            await SendEthTransferAsync(DevChainHttpFixture.RecipientAddress, OneToken);

            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                _fixture.Account.Address, Array.Empty<string>(), BlockParameter.CreateLatest());

            var block = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest());
            var stateRoot = block.StateRoot.HexToByteArray();

            var account = proof.ToAccount();
            var valid = ProofVerification.Current.Account.Verify(
                stateRoot, proof.AccountProofs.Select(x => x.HexToByteArray()),
                _fixture.Account.Address, account);

            Assert.True(valid, "Account proof should verify cryptographically against block state root");
        }

        [Fact]
        public async Task StorageProof_VerifiesCryptographically()
        {
            var contractAddress = await DeployAndMintAsync(OneToken * 1000);

            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                contractAddress, new[] { "0x0" }, BlockParameter.CreateLatest());

            var block = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest());
            var stateRoot = block.StateRoot.HexToByteArray();

            var account = proof.ToAccount();
            var accountValid = ProofVerification.Current.Account.Verify(
                stateRoot, proof.AccountProofs.Select(x => x.HexToByteArray()),
                contractAddress, account);
            Assert.True(accountValid, "Account proof for contract should verify");

            Assert.NotEmpty(proof.StorageProof);
            var sp = proof.StorageProof[0];
            if (sp.Proof != null && sp.Proof.Count > 0)
            {
                var storageValid = ProofVerification.Current.Storage.Verify(
                    proof.StorageHash.HexToByteArray(),
                    sp.Proof.Select(x => x.HexToByteArray()).ToList(),
                    sp.Key.HexValue.HexToByteArray(),
                    sp.Value.HexValue.HexToByteArray());
                Assert.True(storageValid, "Storage proof should verify cryptographically");
            }
        }

        [Fact]
        public async Task ProofValidationService_GetAndValidateAccountProof()
        {
            await SendEthTransferAsync(DevChainHttpFixture.RecipientAddress, OneToken);

            var validationService = new EthChainProofValidationService(_fixture.Web3.Client);

            var accountProof = await validationService.GetAndValidateAccountProof(
                _fixture.Account.Address);

            Assert.NotNull(accountProof);
            Assert.True(accountProof.Balance.Value > 0);
            Assert.NotNull(accountProof.AccountProofs);
            Assert.NotEmpty(accountProof.AccountProofs);
        }

        [Fact]
        public async Task ProofValidationService_GetAndValidateValueFromStorage()
        {
            var contractAddress = await DeployAndMintAsync(OneToken * 500);

            var validationService = new EthChainProofValidationService(_fixture.Web3.Client);

            var value = await validationService.GetAndValidateValueFromStorage(
                contractAddress, "0x2");

            Assert.NotNull(value);
        }

        [Fact]
        public async Task AccountProof_MultipleAccounts_AllVerify()
        {
            var addresses = new List<string>();
            for (int i = 0; i < 5; i++)
            {
                var key = EthECKey.GenerateKey();
                var addr = key.GetPublicAddress();
                addresses.Add(addr);
                await SendEthTransferAsync(addr, OneToken);
            }

            var block = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest());
            var stateRoot = block.StateRoot.HexToByteArray();

            foreach (var addr in addresses)
            {
                var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                    addr, Array.Empty<string>(), BlockParameter.CreateLatest());

                var account = proof.ToAccount();
                var valid = ProofVerification.Current.Account.Verify(
                    stateRoot, proof.AccountProofs.Select(x => x.HexToByteArray()),
                    addr, account);

                Assert.True(valid, $"Account proof for {addr} should verify");
                Assert.True(proof.Balance.Value > 0, $"Balance for {addr} should be non-zero");
            }
        }

        [Fact]
        public async Task Given_AFreshAccountFundedByATransfer_When_ItsProofIsRequestedAtTheFundingBlock_Then_ItVerifiesAgainstThatHeaderStateRootAndCarriesTheTransferredBalance()
        {
            var fresh = EthECKey.GenerateKey().GetPublicAddress();
            var receipt = await SendEthTransferAsync(fresh, OneToken);
            var fundingBlock = new BlockParameter(receipt.BlockNumber);

            var header = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(fundingBlock);
            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                fresh, Array.Empty<string>(), fundingBlock);
            var rpcBalance = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(fresh, fundingBlock);

            Assert.Equal(OneToken, rpcBalance.Value);
            Assert.Equal(OneToken, proof.Balance.Value);
            Assert.NotEmpty(proof.AccountProofs);
            Assert.Equal(header.StateRoot,
                new Nethereum.Util.Sha3Keccack().CalculateHash(proof.AccountProofs[0].HexToByteArray()).ToHex(true));
            Assert.True(ProofVerification.Current.Account.Verify(
                header.StateRoot.HexToByteArray(), proof.AccountProofs.Select(x => x.HexToByteArray()),
                fresh, proof.ToAccount()));
        }

        [Fact]
        public async Task StorageProof_MultipleSlots_AllVerify()
        {
            var contractAddress = await DeployAndMintAsync(OneToken * 10000);

            var recipient1 = EthECKey.GenerateKey().GetPublicAddress();
            var recipient2 = EthECKey.GenerateKey().GetPublicAddress();
            var recipient3 = EthECKey.GenerateKey().GetPublicAddress();

            var transferHandler = _fixture.Web3.Eth.GetContractTransactionHandler<TransferFunction>();
            await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress,
                new TransferFunction { To = recipient1, Value = OneToken * 100 });
            await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress,
                new TransferFunction { To = recipient2, Value = OneToken * 200 });
            await transferHandler.SendRequestAndWaitForReceiptAsync(contractAddress,
                new TransferFunction { To = recipient3, Value = OneToken * 300 });

            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                contractAddress, new[] { "0x0", "0x1", "0x2" }, BlockParameter.CreateLatest());

            var block = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest());
            var stateRoot = block.StateRoot.HexToByteArray();

            var account = proof.ToAccount();
            var accountValid = ProofVerification.Current.Account.Verify(
                stateRoot, proof.AccountProofs.Select(x => x.HexToByteArray()),
                contractAddress, account);
            Assert.True(accountValid, "Account proof should verify for multi-slot request");

            Assert.Equal(3, proof.StorageProof.Count);
            foreach (var sp in proof.StorageProof)
            {
                if (sp.Proof != null && sp.Proof.Count > 0)
                {
                    var valid = ProofVerification.Current.Storage.Verify(
                        proof.StorageHash.HexToByteArray(),
                        sp.Proof.Select(x => x.HexToByteArray()).ToList(),
                        sp.Key.HexValue.HexToByteArray(),
                        sp.Value.HexValue.HexToByteArray());
                    Assert.True(valid, $"Storage proof for key {sp.Key.Value} should verify");
                }
            }
        }

        [Fact]
        public async Task AccountProof_AfterMultipleBlocks_StillValid()
        {
            for (int i = 0; i < 5; i++)
            {
                var key = EthECKey.GenerateKey();
                await SendEthTransferAsync(key.GetPublicAddress(), OneToken);
            }

            var latestBlock = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest());
            Assert.True(latestBlock.Number.Value >= 5, "Should have produced multiple blocks");

            var stateRoot = latestBlock.StateRoot.HexToByteArray();

            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                _fixture.Account.Address, Array.Empty<string>(), BlockParameter.CreateLatest());

            var account = proof.ToAccount();
            var valid = ProofVerification.Current.Account.Verify(
                stateRoot, proof.AccountProofs.Select(x => x.HexToByteArray()),
                _fixture.Account.Address, account);

            Assert.True(valid, "Account proof should verify after multiple blocks of state changes");
        }

        [Fact]
        public async Task TamperedAccountProof_FailsVerification()
        {
            await SendEthTransferAsync(DevChainHttpFixture.RecipientAddress, OneToken);

            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                _fixture.Account.Address, Array.Empty<string>(), BlockParameter.CreateLatest());

            var block = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest());
            var stateRoot = block.StateRoot.HexToByteArray();

            var account = proof.ToAccount();
            account.Balance += 1;

            var valid = ProofVerification.Current.Account.Verify(
                stateRoot, proof.AccountProofs.Select(x => x.HexToByteArray()),
                _fixture.Account.Address, account);

            Assert.False(valid, "Tampered balance should cause account proof verification to fail");
        }

        [Fact]
        public async Task TamperedProofNode_FailsVerification()
        {
            await SendEthTransferAsync(DevChainHttpFixture.RecipientAddress, OneToken);

            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                _fixture.Account.Address, Array.Empty<string>(), BlockParameter.CreateLatest());

            var block = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest());
            var stateRoot = block.StateRoot.HexToByteArray();

            var proofNodes = proof.AccountProofs.Select(x => x.HexToByteArray()).ToList();
            var lastNode = proofNodes[proofNodes.Count - 1];
            lastNode[lastNode.Length - 1] ^= 0xFF;

            var account = proof.ToAccount();
            var valid = ProofVerification.Current.Account.Verify(
                stateRoot, proofNodes,
                _fixture.Account.Address, account);

            Assert.False(valid, "Flipped byte in proof node should cause verification to fail");
        }

        [Fact]
        public async Task TamperedStorageProof_FailsVerification()
        {
            var contractAddress = await DeployAndMintAsync(OneToken * 1000);

            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                contractAddress, new[] { "0x2" }, BlockParameter.CreateLatest());

            Assert.NotEmpty(proof.StorageProof);
            var sp = proof.StorageProof[0];
            Assert.NotNull(sp.Proof);
            Assert.NotEmpty(sp.Proof);

            var tamperedValue = sp.Value.HexValue.HexToByteArray();
            if (tamperedValue.Length > 0)
                tamperedValue[tamperedValue.Length - 1] ^= 0x01;
            else
                tamperedValue = new byte[] { 0x01 };

            var storageValid = ProofVerification.Current.Storage.Verify(
                proof.StorageHash.HexToByteArray(),
                sp.Proof.Select(x => x.HexToByteArray()).ToList(),
                sp.Key.HexValue.HexToByteArray(),
                tamperedValue);

            Assert.False(storageValid, "Tampered storage value should cause storage proof verification to fail");
        }

        [Fact]
        public async Task WrongStateRoot_FailsVerification()
        {
            await SendEthTransferAsync(DevChainHttpFixture.RecipientAddress, OneToken);

            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                _fixture.Account.Address, Array.Empty<string>(), BlockParameter.CreateLatest());

            var fakeStateRoot = new byte[32];
            fakeStateRoot[0] = 0xDE;
            fakeStateRoot[1] = 0xAD;

            var account = proof.ToAccount();
            var valid = ProofVerification.Current.Account.Verify(
                fakeStateRoot, proof.AccountProofs.Select(x => x.HexToByteArray()),
                _fixture.Account.Address, account);

            Assert.False(valid, "Wrong state root should cause account proof verification to fail");
        }

        [Fact]
        public async Task AbsenceProof_NonExistentAccount_ReturnsZeroState()
        {
            await SendEthTransferAsync(DevChainHttpFixture.RecipientAddress, OneToken);

            var nonExistentAddress = "0x0000000000000000000000000000000000000001";

            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                nonExistentAddress, Array.Empty<string>(), BlockParameter.CreateLatest());

            Assert.NotNull(proof);
            Assert.Equal(BigInteger.Zero, proof.Balance.Value);
            Assert.Equal(BigInteger.Zero, proof.Nonce.Value);

            if (proof.AccountProofs != null && proof.AccountProofs.Count > 0)
            {
                var block = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                    .SendRequestAsync(BlockParameter.CreateLatest());
                var stateRoot = block.StateRoot.HexToByteArray();

                var account = proof.ToAccount();
                var valid = ProofVerification.Current.Account.Verify(
                    stateRoot, proof.AccountProofs.Select(x => x.HexToByteArray()),
                    nonExistentAddress, account);
                Assert.False(valid, "Non-existent account proof should not verify as a real account");
            }
        }

        private async Task<TransactionReceipt> DeployERC20Async()
        {
            var gas = await _fixture.Web3.Eth.TransactionManager.EstimateGasAsync(new Nethereum.RPC.Eth.DTOs.CallInput
            {
                From = _fixture.Account.Address,
                Data = Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.EnsureHexPrefix(ERC20Contract.BYTECODE)
            });
            return await _fixture.Web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                ERC20Contract.BYTECODE, _fixture.Account.Address, gas);
        }

        private async Task<string> DeployAndMintAsync(BigInteger mintAmount)
        {
            var receipt = await DeployERC20Async();
            var contractAddress = receipt.ContractAddress;
            var mintHandler = _fixture.Web3.Eth.GetContractTransactionHandler<MintFunction>();
            await mintHandler.SendRequestAndWaitForReceiptAsync(contractAddress,
                new MintFunction { To = _fixture.Account.Address, Amount = mintAmount });
            return contractAddress;
        }

        private async Task<TransactionReceipt> SendEthTransferAsync(string to, BigInteger value)
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
            return receipt;
        }
    }
}
