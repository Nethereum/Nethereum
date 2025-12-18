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
using Nethereum.Circles.Contracts.INameRegistry.ContractDefinition;

namespace Nethereum.Circles.Contracts.INameRegistry
{
    public partial class INameRegistryService: ContractWeb3ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, INameRegistryDeployment iNameRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<INameRegistryDeployment>().SendRequestAndWaitForReceiptAsync(iNameRegistryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, INameRegistryDeployment iNameRegistryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<INameRegistryDeployment>().SendRequestAsync(iNameRegistryDeployment);
        }

        public static async Task<INameRegistryService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, INameRegistryDeployment iNameRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, iNameRegistryDeployment, cancellationTokenSource);
            return new INameRegistryService(web3, receipt.ContractAddress);
        }

        public INameRegistryService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<byte[]> GetMetadataDigestQueryAsync(GetMetadataDigestFunction getMetadataDigestFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetMetadataDigestFunction, byte[]>(getMetadataDigestFunction, blockParameter);
        }

        
        public Task<byte[]> GetMetadataDigestQueryAsync(string avatar, BlockParameter blockParameter = null)
        {
            var getMetadataDigestFunction = new GetMetadataDigestFunction();
                getMetadataDigestFunction.Avatar = avatar;
            
            return ContractHandler.QueryAsync<GetMetadataDigestFunction, byte[]>(getMetadataDigestFunction, blockParameter);
        }

        public Task<bool> IsValidNameQueryAsync(IsValidNameFunction isValidNameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsValidNameFunction, bool>(isValidNameFunction, blockParameter);
        }

        
        public Task<bool> IsValidNameQueryAsync(string name, BlockParameter blockParameter = null)
        {
            var isValidNameFunction = new IsValidNameFunction();
                isValidNameFunction.Name = name;
            
            return ContractHandler.QueryAsync<IsValidNameFunction, bool>(isValidNameFunction, blockParameter);
        }

        public Task<bool> IsValidSymbolQueryAsync(IsValidSymbolFunction isValidSymbolFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsValidSymbolFunction, bool>(isValidSymbolFunction, blockParameter);
        }

        
        public Task<bool> IsValidSymbolQueryAsync(string symbol, BlockParameter blockParameter = null)
        {
            var isValidSymbolFunction = new IsValidSymbolFunction();
                isValidSymbolFunction.Symbol = symbol;
            
            return ContractHandler.QueryAsync<IsValidSymbolFunction, bool>(isValidSymbolFunction, blockParameter);
        }

        public Task<string> NameQueryAsync(NameFunction nameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }

        
        public Task<string> NameQueryAsync(string avatar, BlockParameter blockParameter = null)
        {
            var nameFunction = new NameFunction();
                nameFunction.Avatar = avatar;
            
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }

        public Task<string> RegisterCustomNameRequestAsync(RegisterCustomNameFunction registerCustomNameFunction)
        {
             return ContractHandler.SendRequestAsync(registerCustomNameFunction);
        }

        public Task<TransactionReceipt> RegisterCustomNameRequestAndWaitForReceiptAsync(RegisterCustomNameFunction registerCustomNameFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerCustomNameFunction, cancellationToken);
        }

        public Task<string> RegisterCustomNameRequestAsync(string avatar, string name)
        {
            var registerCustomNameFunction = new RegisterCustomNameFunction();
                registerCustomNameFunction.Avatar = avatar;
                registerCustomNameFunction.Name = name;
            
             return ContractHandler.SendRequestAsync(registerCustomNameFunction);
        }

        public Task<TransactionReceipt> RegisterCustomNameRequestAndWaitForReceiptAsync(string avatar, string name, CancellationTokenSource cancellationToken = null)
        {
            var registerCustomNameFunction = new RegisterCustomNameFunction();
                registerCustomNameFunction.Avatar = avatar;
                registerCustomNameFunction.Name = name;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerCustomNameFunction, cancellationToken);
        }

        public Task<string> RegisterCustomSymbolRequestAsync(RegisterCustomSymbolFunction registerCustomSymbolFunction)
        {
             return ContractHandler.SendRequestAsync(registerCustomSymbolFunction);
        }

        public Task<TransactionReceipt> RegisterCustomSymbolRequestAndWaitForReceiptAsync(RegisterCustomSymbolFunction registerCustomSymbolFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerCustomSymbolFunction, cancellationToken);
        }

        public Task<string> RegisterCustomSymbolRequestAsync(string avatar, string symbol)
        {
            var registerCustomSymbolFunction = new RegisterCustomSymbolFunction();
                registerCustomSymbolFunction.Avatar = avatar;
                registerCustomSymbolFunction.Symbol = symbol;
            
             return ContractHandler.SendRequestAsync(registerCustomSymbolFunction);
        }

        public Task<TransactionReceipt> RegisterCustomSymbolRequestAndWaitForReceiptAsync(string avatar, string symbol, CancellationTokenSource cancellationToken = null)
        {
            var registerCustomSymbolFunction = new RegisterCustomSymbolFunction();
                registerCustomSymbolFunction.Avatar = avatar;
                registerCustomSymbolFunction.Symbol = symbol;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerCustomSymbolFunction, cancellationToken);
        }

        public Task<string> SetMetadataDigestRequestAsync(SetMetadataDigestFunction setMetadataDigestFunction)
        {
             return ContractHandler.SendRequestAsync(setMetadataDigestFunction);
        }

        public Task<TransactionReceipt> SetMetadataDigestRequestAndWaitForReceiptAsync(SetMetadataDigestFunction setMetadataDigestFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setMetadataDigestFunction, cancellationToken);
        }

        public Task<string> SetMetadataDigestRequestAsync(string avatar, byte[] metadataDigest)
        {
            var setMetadataDigestFunction = new SetMetadataDigestFunction();
                setMetadataDigestFunction.Avatar = avatar;
                setMetadataDigestFunction.MetadataDigest = metadataDigest;
            
             return ContractHandler.SendRequestAsync(setMetadataDigestFunction);
        }

        public Task<TransactionReceipt> SetMetadataDigestRequestAndWaitForReceiptAsync(string avatar, byte[] metadataDigest, CancellationTokenSource cancellationToken = null)
        {
            var setMetadataDigestFunction = new SetMetadataDigestFunction();
                setMetadataDigestFunction.Avatar = avatar;
                setMetadataDigestFunction.MetadataDigest = metadataDigest;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setMetadataDigestFunction, cancellationToken);
        }

        public Task<string> SymbolQueryAsync(SymbolFunction symbolFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(symbolFunction, blockParameter);
        }

        
        public Task<string> SymbolQueryAsync(string avatar, BlockParameter blockParameter = null)
        {
            var symbolFunction = new SymbolFunction();
                symbolFunction.Avatar = avatar;
            
            return ContractHandler.QueryAsync<SymbolFunction, string>(symbolFunction, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(GetMetadataDigestFunction),
                typeof(IsValidNameFunction),
                typeof(IsValidSymbolFunction),
                typeof(NameFunction),
                typeof(RegisterCustomNameFunction),
                typeof(RegisterCustomSymbolFunction),
                typeof(SetMetadataDigestFunction),
                typeof(SymbolFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {

            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {

            };
        }
    }
}
