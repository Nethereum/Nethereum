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
using Nethereum.ENS.DNSRegister.ContractDefinition;

namespace Nethereum.ENS
{
    public partial class DNSRegisterService
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.Web3 web3, DNSRegisterDeployment dNSRegisterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<DNSRegisterDeployment>().SendRequestAndWaitForReceiptAsync(dNSRegisterDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.Web3 web3, DNSRegisterDeployment dNSRegisterDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<DNSRegisterDeployment>().SendRequestAsync(dNSRegisterDeployment);
        }

        public static async Task<DNSRegisterService> DeployContractAndGetServiceAsync(Nethereum.Web3.Web3 web3, DNSRegisterDeployment dNSRegisterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, dNSRegisterDeployment, cancellationTokenSource);
            return new DNSRegisterService(web3, receipt.ContractAddress);
        }

        protected Nethereum.Web3.Web3 Web3{ get; }

        public ContractHandler ContractHandler { get; }

        public DNSRegisterService(Nethereum.Web3.Web3 web3, string contractAddress)
        {
            Web3 = web3;
            ContractHandler = web3.Eth.GetContractHandler(contractAddress);
        }

        public Task<string> ClaimRequestAsync(ClaimFunction claimFunction)
        {
             return ContractHandler.SendRequestAsync(claimFunction);
        }

        public Task<TransactionReceipt> ClaimRequestAndWaitForReceiptAsync(ClaimFunction claimFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(claimFunction, cancellationToken);
        }

        public Task<string> ClaimRequestAsync(byte[] name, byte[] proof)
        {
            var claimFunction = new ClaimFunction();
                claimFunction.Name = name;
                claimFunction.Proof = proof;
            
             return ContractHandler.SendRequestAsync(claimFunction);
        }

        public Task<TransactionReceipt> ClaimRequestAndWaitForReceiptAsync(byte[] name, byte[] proof, CancellationTokenSource cancellationToken = null)
        {
            var claimFunction = new ClaimFunction();
                claimFunction.Name = name;
                claimFunction.Proof = proof;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(claimFunction, cancellationToken);
        }

        public Task<string> EnsQueryAsync(EnsFunction ensFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(ensFunction, blockParameter);
        }

        
        public Task<string> EnsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EnsFunction, string>(null, blockParameter);
        }

        public Task<string> OracleQueryAsync(OracleFunction oracleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OracleFunction, string>(oracleFunction, blockParameter);
        }

        
        public Task<string> OracleQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OracleFunction, string>(null, blockParameter);
        }

        public Task<string> ProveAndClaimRequestAsync(ProveAndClaimFunction proveAndClaimFunction)
        {
             return ContractHandler.SendRequestAsync(proveAndClaimFunction);
        }

        public Task<TransactionReceipt> ProveAndClaimRequestAndWaitForReceiptAsync(ProveAndClaimFunction proveAndClaimFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(proveAndClaimFunction, cancellationToken);
        }

        public Task<string> ProveAndClaimRequestAsync(byte[] name, byte[] input, byte[] proof)
        {
            var proveAndClaimFunction = new ProveAndClaimFunction();
                proveAndClaimFunction.Name = name;
                proveAndClaimFunction.Input = input;
                proveAndClaimFunction.Proof = proof;
            
             return ContractHandler.SendRequestAsync(proveAndClaimFunction);
        }

        public Task<TransactionReceipt> ProveAndClaimRequestAndWaitForReceiptAsync(byte[] name, byte[] input, byte[] proof, CancellationTokenSource cancellationToken = null)
        {
            var proveAndClaimFunction = new ProveAndClaimFunction();
                proveAndClaimFunction.Name = name;
                proveAndClaimFunction.Input = input;
                proveAndClaimFunction.Proof = proof;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(proveAndClaimFunction, cancellationToken);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        
        public Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceID, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
                supportsInterfaceFunction.InterfaceID = interfaceID;
            
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }
    }
}
