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
using Nethereum.Circles.Contracts.NameRegistry.ContractDefinition;

namespace Nethereum.Circles.Contracts.NameRegistry
{
    public partial class NameRegistryService: ContractWeb3ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, NameRegistryDeployment nameRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<NameRegistryDeployment>().SendRequestAndWaitForReceiptAsync(nameRegistryDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, NameRegistryDeployment nameRegistryDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<NameRegistryDeployment>().SendRequestAsync(nameRegistryDeployment);
        }

        public static async Task<NameRegistryService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, NameRegistryDeployment nameRegistryDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, nameRegistryDeployment, cancellationTokenSource);
            return new NameRegistryService(web3, receipt.ContractAddress);
        }

        public NameRegistryService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> DefaultCirclesNamePrefixQueryAsync(DefaultCirclesNamePrefixFunction defaultCirclesNamePrefixFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DefaultCirclesNamePrefixFunction, string>(defaultCirclesNamePrefixFunction, blockParameter);
        }

        
        public Task<string> DefaultCirclesNamePrefixQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DefaultCirclesNamePrefixFunction, string>(null, blockParameter);
        }

        public Task<string> DefaultCirclesSymbolQueryAsync(DefaultCirclesSymbolFunction defaultCirclesSymbolFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DefaultCirclesSymbolFunction, string>(defaultCirclesSymbolFunction, blockParameter);
        }

        
        public Task<string> DefaultCirclesSymbolQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DefaultCirclesSymbolFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> MaxShortNameQueryAsync(MaxShortNameFunction maxShortNameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxShortNameFunction, BigInteger>(maxShortNameFunction, blockParameter);
        }

        
        public Task<BigInteger> MaxShortNameQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxShortNameFunction, BigInteger>(null, blockParameter);
        }

        public Task<byte[]> AvatarToMetaDataDigestQueryAsync(AvatarToMetaDataDigestFunction avatarToMetaDataDigestFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AvatarToMetaDataDigestFunction, byte[]>(avatarToMetaDataDigestFunction, blockParameter);
        }

        
        public Task<byte[]> AvatarToMetaDataDigestQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var avatarToMetaDataDigestFunction = new AvatarToMetaDataDigestFunction();
                avatarToMetaDataDigestFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<AvatarToMetaDataDigestFunction, byte[]>(avatarToMetaDataDigestFunction, blockParameter);
        }

        public Task<BigInteger> CalculateShortNameWithNonceQueryAsync(CalculateShortNameWithNonceFunction calculateShortNameWithNonceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CalculateShortNameWithNonceFunction, BigInteger>(calculateShortNameWithNonceFunction, blockParameter);
        }

        
        public Task<BigInteger> CalculateShortNameWithNonceQueryAsync(string avatar, BigInteger nonce, BlockParameter blockParameter = null)
        {
            var calculateShortNameWithNonceFunction = new CalculateShortNameWithNonceFunction();
                calculateShortNameWithNonceFunction.Avatar = avatar;
                calculateShortNameWithNonceFunction.Nonce = nonce;
            
            return ContractHandler.QueryAsync<CalculateShortNameWithNonceFunction, BigInteger>(calculateShortNameWithNonceFunction, blockParameter);
        }

        public Task<string> CustomNamesQueryAsync(CustomNamesFunction customNamesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CustomNamesFunction, string>(customNamesFunction, blockParameter);
        }

        
        public Task<string> CustomNamesQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var customNamesFunction = new CustomNamesFunction();
                customNamesFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<CustomNamesFunction, string>(customNamesFunction, blockParameter);
        }

        public Task<string> CustomSymbolsQueryAsync(CustomSymbolsFunction customSymbolsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CustomSymbolsFunction, string>(customSymbolsFunction, blockParameter);
        }

        
        public Task<string> CustomSymbolsQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var customSymbolsFunction = new CustomSymbolsFunction();
                customSymbolsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<CustomSymbolsFunction, string>(customSymbolsFunction, blockParameter);
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

        public Task<string> HubQueryAsync(HubFunction hubFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HubFunction, string>(hubFunction, blockParameter);
        }

        
        public Task<string> HubQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HubFunction, string>(null, blockParameter);
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

        public Task<string> RegisterShortNameRequestAsync(RegisterShortNameFunction registerShortNameFunction)
        {
             return ContractHandler.SendRequestAsync(registerShortNameFunction);
        }

        public Task<string> RegisterShortNameRequestAsync()
        {
             return ContractHandler.SendRequestAsync<RegisterShortNameFunction>();
        }

        public Task<TransactionReceipt> RegisterShortNameRequestAndWaitForReceiptAsync(RegisterShortNameFunction registerShortNameFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerShortNameFunction, cancellationToken);
        }

        public Task<TransactionReceipt> RegisterShortNameRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<RegisterShortNameFunction>(null, cancellationToken);
        }

        public Task<string> RegisterShortNameWithNonceRequestAsync(RegisterShortNameWithNonceFunction registerShortNameWithNonceFunction)
        {
             return ContractHandler.SendRequestAsync(registerShortNameWithNonceFunction);
        }

        public Task<TransactionReceipt> RegisterShortNameWithNonceRequestAndWaitForReceiptAsync(RegisterShortNameWithNonceFunction registerShortNameWithNonceFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerShortNameWithNonceFunction, cancellationToken);
        }

        public Task<string> RegisterShortNameWithNonceRequestAsync(BigInteger nonce)
        {
            var registerShortNameWithNonceFunction = new RegisterShortNameWithNonceFunction();
                registerShortNameWithNonceFunction.Nonce = nonce;
            
             return ContractHandler.SendRequestAsync(registerShortNameWithNonceFunction);
        }

        public Task<TransactionReceipt> RegisterShortNameWithNonceRequestAndWaitForReceiptAsync(BigInteger nonce, CancellationTokenSource cancellationToken = null)
        {
            var registerShortNameWithNonceFunction = new RegisterShortNameWithNonceFunction();
                registerShortNameWithNonceFunction.Nonce = nonce;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerShortNameWithNonceFunction, cancellationToken);
        }

        public Task<SearchShortNameOutputDTO> SearchShortNameQueryAsync(SearchShortNameFunction searchShortNameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<SearchShortNameFunction, SearchShortNameOutputDTO>(searchShortNameFunction, blockParameter);
        }

        public Task<SearchShortNameOutputDTO> SearchShortNameQueryAsync(string avatar, BlockParameter blockParameter = null)
        {
            var searchShortNameFunction = new SearchShortNameFunction();
                searchShortNameFunction.Avatar = avatar;
            
            return ContractHandler.QueryDeserializingToObjectAsync<SearchShortNameFunction, SearchShortNameOutputDTO>(searchShortNameFunction, blockParameter);
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

        public Task<string> ShortNameToAvatarQueryAsync(ShortNameToAvatarFunction shortNameToAvatarFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ShortNameToAvatarFunction, string>(shortNameToAvatarFunction, blockParameter);
        }

        
        public Task<string> ShortNameToAvatarQueryAsync(BigInteger returnValue1, BlockParameter blockParameter = null)
        {
            var shortNameToAvatarFunction = new ShortNameToAvatarFunction();
                shortNameToAvatarFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<ShortNameToAvatarFunction, string>(shortNameToAvatarFunction, blockParameter);
        }

        public Task<BigInteger> ShortNamesQueryAsync(ShortNamesFunction shortNamesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ShortNamesFunction, BigInteger>(shortNamesFunction, blockParameter);
        }

        
        public Task<BigInteger> ShortNamesQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var shortNamesFunction = new ShortNamesFunction();
                shortNamesFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<ShortNamesFunction, BigInteger>(shortNamesFunction, blockParameter);
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

        public Task<string> UpdateMetadataDigestRequestAsync(UpdateMetadataDigestFunction updateMetadataDigestFunction)
        {
             return ContractHandler.SendRequestAsync(updateMetadataDigestFunction);
        }

        public Task<TransactionReceipt> UpdateMetadataDigestRequestAndWaitForReceiptAsync(UpdateMetadataDigestFunction updateMetadataDigestFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(updateMetadataDigestFunction, cancellationToken);
        }

        public Task<string> UpdateMetadataDigestRequestAsync(byte[] metadataDigest)
        {
            var updateMetadataDigestFunction = new UpdateMetadataDigestFunction();
                updateMetadataDigestFunction.MetadataDigest = metadataDigest;
            
             return ContractHandler.SendRequestAsync(updateMetadataDigestFunction);
        }

        public Task<TransactionReceipt> UpdateMetadataDigestRequestAndWaitForReceiptAsync(byte[] metadataDigest, CancellationTokenSource cancellationToken = null)
        {
            var updateMetadataDigestFunction = new UpdateMetadataDigestFunction();
                updateMetadataDigestFunction.MetadataDigest = metadataDigest;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(updateMetadataDigestFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(DefaultCirclesNamePrefixFunction),
                typeof(DefaultCirclesSymbolFunction),
                typeof(MaxShortNameFunction),
                typeof(AvatarToMetaDataDigestFunction),
                typeof(CalculateShortNameWithNonceFunction),
                typeof(CustomNamesFunction),
                typeof(CustomSymbolsFunction),
                typeof(GetMetadataDigestFunction),
                typeof(HubFunction),
                typeof(IsValidNameFunction),
                typeof(IsValidSymbolFunction),
                typeof(NameFunction),
                typeof(RegisterCustomNameFunction),
                typeof(RegisterCustomSymbolFunction),
                typeof(RegisterShortNameFunction),
                typeof(RegisterShortNameWithNonceFunction),
                typeof(SearchShortNameFunction),
                typeof(SetMetadataDigestFunction),
                typeof(ShortNameToAvatarFunction),
                typeof(ShortNamesFunction),
                typeof(SymbolFunction),
                typeof(UpdateMetadataDigestFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(RegisterShortNameEventDTO),
                typeof(UpdateMetadataDigestEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(CirclesAmountOverflowError),
                typeof(CirclesErrorAddressUintArgsError),
                typeof(CirclesErrorNoArgsError),
                typeof(CirclesErrorOneAddressArgError),
                typeof(CirclesIdMustBeDerivedFromAddressError),
                typeof(CirclesInvalidCirclesIdError),
                typeof(CirclesInvalidParameterError),
                typeof(CirclesNamesAvatarAlreadyHasCustomNameOrSymbolError),
                typeof(CirclesNamesInvalidNameError),
                typeof(CirclesNamesOrganizationHasNoSymbolError),
                typeof(CirclesNamesShortNameAlreadyAssignedError),
                typeof(CirclesNamesShortNameWithNonceTakenError),
                typeof(CirclesNamesShortNameZeroError),
                typeof(CirclesProxyAlreadyInitializedError),
                typeof(CirclesReentrancyGuardError)
            };
        }
    }
}
