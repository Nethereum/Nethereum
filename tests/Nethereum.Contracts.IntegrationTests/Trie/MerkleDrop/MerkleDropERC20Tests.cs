using Nethereum.ABI.ByteArrayConvertors;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Mekle.Contracts.MerkleERC20Drop.ContractDefinition;
using Nethereum.Merkle;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util.HashProviders;
using Nethereum.Web3.Accounts;
using Nethereum.XUnitEthereumClients;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Xunit;
// ReSharper disable ConsiderUsingConfigureAwait  
// ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Contracts.IntegrationTests.Trie.MerkleDrop
{

    [Collection(EthereumClientIntegrationFixture.ETHEREUM_CLIENT_COLLECTION_DEFAULT)]
    public class MerkleDropERC20Tests
    {
        private readonly EthereumClientIntegrationFixture _ethereumClientIntegrationFixture;

        public MerkleDropERC20Tests(EthereumClientIntegrationFixture ethereumClientIntegrationFixture)
        {
            _ethereumClientIntegrationFixture = ethereumClientIntegrationFixture;
        }

        [Struct("MerklePaymentDropItem")]
        public class MerklePaymentItem
        {
            [Parameter("address", "sender", 1)]
            public string Sender { get; set; }

            [Parameter("address", "receiver", 2)]
            public string Receiver { get; set; }

            [Parameter("uint256", "amount", 3)]
            public BigInteger Amount { get; set; }
        }

        [Fact]
        public async void ShouldCreateAMerkleDropDeployContractAndClaimIt()
        {
            var web3 = _ethereumClientIntegrationFixture.GetWeb3();
            
            var account1 = new Account("2ddc04030d24227de118e0e525d886f0b3429bf0302962ae7bea79a297473e04", EthereumClientIntegrationFixture.ChainId);
            var account2 = new Account("2ddc04030d24227de118e0e525d886f0b3429bf0302962ae7bea79a297473e0c", EthereumClientIntegrationFixture.ChainId);
            var account3 = new Account("2ddc04030d24227de118e0e525d886f0b3429bf0302962ae7bea79a297473e27", EthereumClientIntegrationFixture.ChainId);

            var merkleDropAccount1 = new MerkleDropItem() { Address = account1.Address, Amount = 100 };
            var merkleDropAccount2 = new MerkleDropItem() { Address = account2.Address, Amount = 60 };

            var merkleDropAccounts = new List<MerkleDropItem>
            {
                merkleDropAccount1, merkleDropAccount2
            };

            var merkleDropTree = new MerkleDropMerkleTree();
            merkleDropTree.BuildTree(merkleDropAccounts);
            var rootMerkleDrop = merkleDropTree.Root;
          
            var account1MerkleDropProof = merkleDropTree.GetProof(merkleDropAccount1);
            var account2MerkleDropProof = merkleDropTree.GetProof(merkleDropAccount2);

            //Another trie using the generic Abi Struct
            //just for demo the contract includes the proof of the merkle of the sender(s)
            //sender any address..
            var merklePaymentDropItem1 = new MerklePaymentItem() { Sender= EthereumClientIntegrationFixture.AccountAddress,  Receiver = account1.Address, Amount = 100 };
           

            var merklePaymentList = new List<MerklePaymentItem>
            {
                merklePaymentDropItem1
            };

            var merklePaymentTree = new AbiStructMerkleTree<MerklePaymentItem>();
            merklePaymentTree.BuildTree(merklePaymentList);
            var rootMerklePayment = merklePaymentTree.Root;

            var merklePayment1Proof = merklePaymentTree.GetProof(merklePaymentDropItem1);
        

            var deployment = new MerkleERC20DropDeployment();
            deployment.Decimals = 2;
            deployment.RootMerkleDrop = rootMerkleDrop.Hash;
            deployment.RootMerklePayment = rootMerklePayment.Hash;
            deployment.InitialSupply = 200;
            deployment.Name = "Merkle";
            deployment.Symbol = "MKD";
            
            //less calls
            web3.TransactionManager.UseLegacyAsDefault = true;


            var transactionReceiptDeployment = await web3.Eth.GetContractDeploymentHandler<MerkleERC20DropDeployment>().SendRequestAndWaitForReceiptAsync(deployment);
            var contractAddress = transactionReceiptDeployment.ContractAddress;

            var service = new MerkleERC20DropService(web3, contractAddress);

            //Validate we are encoding packing
            var encoder = new AbiStructEncoderPackedByteConvertor<MerkleDropItem>();
            var encodedPacked = encoder.ConvertToByteArray(merkleDropAccount1);
            var encodedPackedContract = await service.ComputeEncodedPackedDropQueryAsync(merkleDropAccount1.Address, merkleDropAccount1.Amount);
            Assert.True(encodedPacked.ToHex().IsTheSameHex(encodedPackedContract.ToHex()));

            //Validate we are using the right hash
            var leafAccount1 = new Sha3KeccackHashProvider().ComputeHash(encodedPacked);
            var leafAccount1Contract = await service.ComputeLeafDropQueryAsync(merkleDropAccount1.Address, merkleDropAccount1.Amount);

            Assert.True(leafAccount1.ToHex().IsTheSameHex(leafAccount1Contract.ToHex()));

            //Validate hash pairing is the same
            var rootComputed = await service.HashPairQueryAsync(account1MerkleDropProof.First(), leafAccount1);
            Assert.True(rootMerkleDrop.Hash.ToHex().IsTheSameHex(rootComputed.ToHex()));

            //Validate we are assigning the root
            var rootInContract = await service.RootMerkleDropQueryAsync();
            Assert.True(rootMerkleDrop.Hash.ToHex().IsTheSameHex(rootInContract.ToHex()));

            //check if is valid account 1
            var validAccount1 = await service.VerifyClaimQueryAsync(merkleDropAccount1.Address, merkleDropAccount1.Amount, account1MerkleDropProof);
            Assert.True(validAccount1);

            //check invalid  account 1
            var invalidAccount1 = await service.VerifyClaimQueryAsync(merkleDropAccount1.Address, merkleDropAccount1.Amount + 10, account1MerkleDropProof);
            Assert.False(invalidAccount1);

            var claimReceipt = await service.ClaimRequestAndWaitForReceiptAsync(merkleDropAccount1.Address, merkleDropAccount1.Amount, account1MerkleDropProof);
            var balanceAccount1 = await service.BalanceOfQueryAsync(merkleDropAccount1.Address);
            Assert.Equal(merkleDropAccount1.Amount, balanceAccount1);


            //Finally check that the payment is included using the proof and amounts
            var validPaymentIncluded = await service.VerifyPaymentIncludedQueryAsync(merklePaymentDropItem1.Sender, merklePaymentDropItem1.Receiver, merklePaymentDropItem1.Amount, merklePayment1Proof);
            Assert.True(validPaymentIncluded);
        }


      
    }
}