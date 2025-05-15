using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Web3;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts;
using System.Threading;
using Nethereum.Mekle.Contracts.MerkleERC20Drop.ContractDefinition;

namespace Nethereum.Contracts.IntegrationTests.Trie.MerkleDrop
{
    public partial class MerkleERC20DropService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Web3.Web3 web3, MerkleERC20DropDeployment merkleERC20DropDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<MerkleERC20DropDeployment>().SendRequestAndWaitForReceiptAsync(merkleERC20DropDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Web3.Web3 web3, MerkleERC20DropDeployment merkleERC20DropDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<MerkleERC20DropDeployment>().SendRequestAsync(merkleERC20DropDeployment);
        }

        public static async Task<MerkleERC20DropService> DeployContractAndGetServiceAsync(Web3.Web3 web3, MerkleERC20DropDeployment merkleERC20DropDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, merkleERC20DropDeployment, cancellationTokenSource);
            return new MerkleERC20DropService(web3, receipt.ContractAddress);
        }

        protected Web3.Web3 Web3{ get; }

        public ContractHandler ContractHandler { get; }

        public MerkleERC20DropService(Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<BigInteger> AllowanceQueryAsync(AllowanceFunction allowanceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllowanceFunction, BigInteger>(allowanceFunction, blockParameter);
        }

        
        public Task<BigInteger> AllowanceQueryAsync(string returnValue1, string returnValue2, BlockParameter blockParameter = null)
        {
            var allowanceFunction = new AllowanceFunction();
                allowanceFunction.ReturnValue1 = returnValue1;
                allowanceFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryAsync<AllowanceFunction, BigInteger>(allowanceFunction, blockParameter);
        }

        public Task<string> ApproveRequestAsync(ApproveFunction approveFunction)
        {
             return ContractHandler.SendRequestAsync(approveFunction);
        }

        public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(ApproveFunction approveFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public Task<string> ApproveRequestAsync(string spender, BigInteger value)
        {
            var approveFunction = new ApproveFunction();
                approveFunction.Spender = spender;
                approveFunction.Value = value;
            
             return ContractHandler.SendRequestAsync(approveFunction);
        }

        public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(string spender, BigInteger value, CancellationTokenSource cancellationToken = null)
        {
            var approveFunction = new ApproveFunction();
                approveFunction.Spender = spender;
                approveFunction.Value = value;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        
        public Task<BigInteger> BalanceOfQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var balanceOfFunction = new BalanceOfFunction();
                balanceOfFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        public Task<string> ClaimRequestAsync(ClaimFunction claimFunction)
        {
             return ContractHandler.SendRequestAsync(claimFunction);
        }

        public Task<TransactionReceipt> ClaimRequestAndWaitForReceiptAsync(ClaimFunction claimFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(claimFunction, cancellationToken);
        }

        public Task<string> ClaimRequestAsync(string claimAddress, BigInteger balance, List<byte[]> merkleProof)
        {
            var claimFunction = new ClaimFunction();
                claimFunction.ClaimAddress = claimAddress;
                claimFunction.Balance = balance;
                claimFunction.MerkleProof = merkleProof;
            
             return ContractHandler.SendRequestAsync(claimFunction);
        }

        public Task<TransactionReceipt> ClaimRequestAndWaitForReceiptAsync(string claimAddress, BigInteger balance, List<byte[]> merkleProof, CancellationTokenSource cancellationToken = null)
        {
            var claimFunction = new ClaimFunction();
                claimFunction.ClaimAddress = claimAddress;
                claimFunction.Balance = balance;
                claimFunction.MerkleProof = merkleProof;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(claimFunction, cancellationToken);
        }

        public Task<string> ClaimSenderRequestAsync(ClaimSenderFunction claimSenderFunction)
        {
             return ContractHandler.SendRequestAsync(claimSenderFunction);
        }

        public Task<TransactionReceipt> ClaimSenderRequestAndWaitForReceiptAsync(ClaimSenderFunction claimSenderFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(claimSenderFunction, cancellationToken);
        }

        public Task<string> ClaimSenderRequestAsync(BigInteger balance, List<byte[]> merkleProof)
        {
            var claimSenderFunction = new ClaimSenderFunction();
                claimSenderFunction.Balance = balance;
                claimSenderFunction.MerkleProof = merkleProof;
            
             return ContractHandler.SendRequestAsync(claimSenderFunction);
        }

        public Task<TransactionReceipt> ClaimSenderRequestAndWaitForReceiptAsync(BigInteger balance, List<byte[]> merkleProof, CancellationTokenSource cancellationToken = null)
        {
            var claimSenderFunction = new ClaimSenderFunction();
                claimSenderFunction.Balance = balance;
                claimSenderFunction.MerkleProof = merkleProof;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(claimSenderFunction, cancellationToken);
        }

        public Task<bool> ClaimedQueryAsync(ClaimedFunction claimedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ClaimedFunction, bool>(claimedFunction, blockParameter);
        }

        
        public Task<bool> ClaimedQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var claimedFunction = new ClaimedFunction();
                claimedFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<ClaimedFunction, bool>(claimedFunction, blockParameter);
        }

        public Task<byte[]> ComputeEncodedPackedDropQueryAsync(ComputeEncodedPackedDropFunction computeEncodedPackedDropFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ComputeEncodedPackedDropFunction, byte[]>(computeEncodedPackedDropFunction, blockParameter);
        }

        
        public Task<byte[]> ComputeEncodedPackedDropQueryAsync(string claimAddress, BigInteger balance, BlockParameter blockParameter = null)
        {
            var computeEncodedPackedDropFunction = new ComputeEncodedPackedDropFunction();
                computeEncodedPackedDropFunction.ClaimAddress = claimAddress;
                computeEncodedPackedDropFunction.Balance = balance;
            
            return ContractHandler.QueryAsync<ComputeEncodedPackedDropFunction, byte[]>(computeEncodedPackedDropFunction, blockParameter);
        }

        public Task<byte[]> ComputeLeafDropQueryAsync(ComputeLeafDropFunction computeLeafDropFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ComputeLeafDropFunction, byte[]>(computeLeafDropFunction, blockParameter);
        }

        
        public Task<byte[]> ComputeLeafDropQueryAsync(string claimAddress, BigInteger balance, BlockParameter blockParameter = null)
        {
            var computeLeafDropFunction = new ComputeLeafDropFunction();
                computeLeafDropFunction.ClaimAddress = claimAddress;
                computeLeafDropFunction.Balance = balance;
            
            return ContractHandler.QueryAsync<ComputeLeafDropFunction, byte[]>(computeLeafDropFunction, blockParameter);
        }

        public Task<byte> DecimalsQueryAsync(DecimalsFunction decimalsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DecimalsFunction, byte>(decimalsFunction, blockParameter);
        }

        
        public Task<byte> DecimalsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DecimalsFunction, byte>(null, blockParameter);
        }

        public Task<byte[]> HashPairQueryAsync(HashPairFunction hashPairFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HashPairFunction, byte[]>(hashPairFunction, blockParameter);
        }

        
        public Task<byte[]> HashPairQueryAsync(byte[] a, byte[] b, BlockParameter blockParameter = null)
        {
            var hashPairFunction = new HashPairFunction();
                hashPairFunction.A = a;
                hashPairFunction.B = b;
            
            return ContractHandler.QueryAsync<HashPairFunction, byte[]>(hashPairFunction, blockParameter);
        }

        public Task<string> NameQueryAsync(NameFunction nameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }

        
        public Task<string> NameQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(null, blockParameter);
        }

        public Task<byte[]> RootMerkleDropQueryAsync(RootMerkleDropFunction rootMerkleDropFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RootMerkleDropFunction, byte[]>(rootMerkleDropFunction, blockParameter);
        }

        
        public Task<byte[]> RootMerkleDropQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RootMerkleDropFunction, byte[]>(null, blockParameter);
        }

        public Task<byte[]> RootMerklePaymentQueryAsync(RootMerklePaymentFunction rootMerklePaymentFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RootMerklePaymentFunction, byte[]>(rootMerklePaymentFunction, blockParameter);
        }

        
        public Task<byte[]> RootMerklePaymentQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RootMerklePaymentFunction, byte[]>(null, blockParameter);
        }

        public Task<string> SymbolQueryAsync(SymbolFunction symbolFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(symbolFunction, blockParameter);
        }

        
        public Task<string> SymbolQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> TotalSupplyQueryAsync(TotalSupplyFunction totalSupplyFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(totalSupplyFunction, blockParameter);
        }

        
        public Task<BigInteger> TotalSupplyQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> TransferRequestAsync(TransferFunction transferFunction)
        {
             return ContractHandler.SendRequestAsync(transferFunction);
        }

        public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(TransferFunction transferFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, cancellationToken);
        }

        public Task<string> TransferRequestAsync(string to, BigInteger value)
        {
            var transferFunction = new TransferFunction();
                transferFunction.To = to;
                transferFunction.Value = value;
            
             return ContractHandler.SendRequestAsync(transferFunction);
        }

        public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(string to, BigInteger value, CancellationTokenSource cancellationToken = null)
        {
            var transferFunction = new TransferFunction();
                transferFunction.To = to;
                transferFunction.Value = value;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFunction, cancellationToken);
        }

        public Task<string> TransferFromRequestAsync(TransferFromFunction transferFromFunction)
        {
             return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(TransferFromFunction transferFromFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }

        public Task<string> TransferFromRequestAsync(string from, string to, BigInteger value)
        {
            var transferFromFunction = new TransferFromFunction();
                transferFromFunction.From = from;
                transferFromFunction.To = to;
                transferFromFunction.Value = value;
            
             return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger value, CancellationTokenSource cancellationToken = null)
        {
            var transferFromFunction = new TransferFromFunction();
                transferFromFunction.From = from;
                transferFromFunction.To = to;
                transferFromFunction.Value = value;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }

        public Task<bool> VerifyClaimQueryAsync(VerifyClaimFunction verifyClaimFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifyClaimFunction, bool>(verifyClaimFunction, blockParameter);
        }

        
        public Task<bool> VerifyClaimQueryAsync(string claimAddress, BigInteger balance, List<byte[]> merkleProof, BlockParameter blockParameter = null)
        {
            var verifyClaimFunction = new VerifyClaimFunction();
                verifyClaimFunction.ClaimAddress = claimAddress;
                verifyClaimFunction.Balance = balance;
                verifyClaimFunction.MerkleProof = merkleProof;
            
            return ContractHandler.QueryAsync<VerifyClaimFunction, bool>(verifyClaimFunction, blockParameter);
        }

        public Task<bool> VerifyPaymentIncludedQueryAsync(VerifyPaymentIncludedFunction verifyPaymentIncludedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<VerifyPaymentIncludedFunction, bool>(verifyPaymentIncludedFunction, blockParameter);
        }

        
        public Task<bool> VerifyPaymentIncludedQueryAsync(string sender, string claimAddress, BigInteger balance, List<byte[]> merkleProof, BlockParameter blockParameter = null)
        {
            var verifyPaymentIncludedFunction = new VerifyPaymentIncludedFunction();
                verifyPaymentIncludedFunction.Sender = sender;
                verifyPaymentIncludedFunction.ClaimAddress = claimAddress;
                verifyPaymentIncludedFunction.Balance = balance;
                verifyPaymentIncludedFunction.MerkleProof = merkleProof;
            
            return ContractHandler.QueryAsync<VerifyPaymentIncludedFunction, bool>(verifyPaymentIncludedFunction, blockParameter);
        }
    }
}
