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
using Nethereum.PrivacyPools.Entrypoint.ContractDefinition;
using Withdrawal = Nethereum.PrivacyPools.Entrypoint.ContractDefinition.Withdrawal;

namespace Nethereum.PrivacyPools.Entrypoint
{
    public partial class EntrypointService: EntrypointServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, EntrypointDeployment entrypointDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<EntrypointDeployment>().SendRequestAndWaitForReceiptAsync(entrypointDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, EntrypointDeployment entrypointDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<EntrypointDeployment>().SendRequestAsync(entrypointDeployment);
        }

        public static async Task<EntrypointService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, EntrypointDeployment entrypointDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, entrypointDeployment, cancellationTokenSource);
            return new EntrypointService(web3, receipt.ContractAddress);
        }

        public EntrypointService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class EntrypointServiceBase: ContractWeb3ServiceBase
    {

        public EntrypointServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<byte[]> DefaultAdminRoleQueryAsync(DefaultAdminRoleFunction defaultAdminRoleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DefaultAdminRoleFunction, byte[]>(defaultAdminRoleFunction, blockParameter);
        }

        
        public virtual Task<byte[]> DefaultAdminRoleQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DefaultAdminRoleFunction, byte[]>(null, blockParameter);
        }

        public Task<string> UpgradeInterfaceVersionQueryAsync(UpgradeInterfaceVersionFunction upgradeInterfaceVersionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<UpgradeInterfaceVersionFunction, string>(upgradeInterfaceVersionFunction, blockParameter);
        }

        
        public virtual Task<string> UpgradeInterfaceVersionQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<UpgradeInterfaceVersionFunction, string>(null, blockParameter);
        }

        public virtual Task<AssetConfigOutputDTO> AssetConfigQueryAsync(AssetConfigFunction assetConfigFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<AssetConfigFunction, AssetConfigOutputDTO>(assetConfigFunction, blockParameter);
        }

        public virtual Task<AssetConfigOutputDTO> AssetConfigQueryAsync(string asset, BlockParameter blockParameter = null)
        {
            var assetConfigFunction = new AssetConfigFunction();
                assetConfigFunction.Asset = asset;
            
            return ContractHandler.QueryDeserializingToObjectAsync<AssetConfigFunction, AssetConfigOutputDTO>(assetConfigFunction, blockParameter);
        }

        public virtual Task<AssociationSetsOutputDTO> AssociationSetsQueryAsync(AssociationSetsFunction associationSetsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<AssociationSetsFunction, AssociationSetsOutputDTO>(associationSetsFunction, blockParameter);
        }

        public virtual Task<AssociationSetsOutputDTO> AssociationSetsQueryAsync(BigInteger returnValue1, BlockParameter blockParameter = null)
        {
            var associationSetsFunction = new AssociationSetsFunction();
                associationSetsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryDeserializingToObjectAsync<AssociationSetsFunction, AssociationSetsOutputDTO>(associationSetsFunction, blockParameter);
        }

        public virtual Task<string> DepositRequestAsync(Deposit1Function deposit1Function)
        {
             return ContractHandler.SendRequestAsync(deposit1Function);
        }

        public virtual Task<TransactionReceipt> DepositRequestAndWaitForReceiptAsync(Deposit1Function deposit1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(deposit1Function, cancellationToken);
        }

        public virtual Task<string> DepositRequestAsync(string asset, BigInteger value, BigInteger precommitment)
        {
            var deposit1Function = new Deposit1Function();
                deposit1Function.Asset = asset;
                deposit1Function.Value = value;
                deposit1Function.Precommitment = precommitment;
            
             return ContractHandler.SendRequestAsync(deposit1Function);
        }

        public virtual Task<TransactionReceipt> DepositRequestAndWaitForReceiptAsync(string asset, BigInteger value, BigInteger precommitment, CancellationTokenSource cancellationToken = null)
        {
            var deposit1Function = new Deposit1Function();
                deposit1Function.Asset = asset;
                deposit1Function.Value = value;
                deposit1Function.Precommitment = precommitment;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(deposit1Function, cancellationToken);
        }

        public virtual Task<string> DepositRequestAsync(DepositFunction depositFunction)
        {
             return ContractHandler.SendRequestAsync(depositFunction);
        }

        public virtual Task<TransactionReceipt> DepositRequestAndWaitForReceiptAsync(DepositFunction depositFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(depositFunction, cancellationToken);
        }

        public virtual Task<string> DepositRequestAsync(BigInteger precommitment)
        {
            var depositFunction = new DepositFunction();
                depositFunction.Precommitment = precommitment;
            
             return ContractHandler.SendRequestAsync(depositFunction);
        }

        public virtual Task<TransactionReceipt> DepositRequestAndWaitForReceiptAsync(BigInteger precommitment, CancellationTokenSource cancellationToken = null)
        {
            var depositFunction = new DepositFunction();
                depositFunction.Precommitment = precommitment;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(depositFunction, cancellationToken);
        }

        public Task<byte[]> GetRoleAdminQueryAsync(GetRoleAdminFunction getRoleAdminFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetRoleAdminFunction, byte[]>(getRoleAdminFunction, blockParameter);
        }

        
        public virtual Task<byte[]> GetRoleAdminQueryAsync(byte[] role, BlockParameter blockParameter = null)
        {
            var getRoleAdminFunction = new GetRoleAdminFunction();
                getRoleAdminFunction.Role = role;
            
            return ContractHandler.QueryAsync<GetRoleAdminFunction, byte[]>(getRoleAdminFunction, blockParameter);
        }

        public virtual Task<string> GrantRoleRequestAsync(GrantRoleFunction grantRoleFunction)
        {
             return ContractHandler.SendRequestAsync(grantRoleFunction);
        }

        public virtual Task<TransactionReceipt> GrantRoleRequestAndWaitForReceiptAsync(GrantRoleFunction grantRoleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(grantRoleFunction, cancellationToken);
        }

        public virtual Task<string> GrantRoleRequestAsync(byte[] role, string account)
        {
            var grantRoleFunction = new GrantRoleFunction();
                grantRoleFunction.Role = role;
                grantRoleFunction.Account = account;
            
             return ContractHandler.SendRequestAsync(grantRoleFunction);
        }

        public virtual Task<TransactionReceipt> GrantRoleRequestAndWaitForReceiptAsync(byte[] role, string account, CancellationTokenSource cancellationToken = null)
        {
            var grantRoleFunction = new GrantRoleFunction();
                grantRoleFunction.Role = role;
                grantRoleFunction.Account = account;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(grantRoleFunction, cancellationToken);
        }

        public Task<bool> HasRoleQueryAsync(HasRoleFunction hasRoleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HasRoleFunction, bool>(hasRoleFunction, blockParameter);
        }

        
        public virtual Task<bool> HasRoleQueryAsync(byte[] role, string account, BlockParameter blockParameter = null)
        {
            var hasRoleFunction = new HasRoleFunction();
                hasRoleFunction.Role = role;
                hasRoleFunction.Account = account;
            
            return ContractHandler.QueryAsync<HasRoleFunction, bool>(hasRoleFunction, blockParameter);
        }

        public virtual Task<string> InitializeRequestAsync(InitializeFunction initializeFunction)
        {
             return ContractHandler.SendRequestAsync(initializeFunction);
        }

        public virtual Task<TransactionReceipt> InitializeRequestAndWaitForReceiptAsync(InitializeFunction initializeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeFunction, cancellationToken);
        }

        public virtual Task<string> InitializeRequestAsync(string owner, string postman)
        {
            var initializeFunction = new InitializeFunction();
                initializeFunction.Owner = owner;
                initializeFunction.Postman = postman;
            
             return ContractHandler.SendRequestAsync(initializeFunction);
        }

        public virtual Task<TransactionReceipt> InitializeRequestAndWaitForReceiptAsync(string owner, string postman, CancellationTokenSource cancellationToken = null)
        {
            var initializeFunction = new InitializeFunction();
                initializeFunction.Owner = owner;
                initializeFunction.Postman = postman;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeFunction, cancellationToken);
        }

        public Task<BigInteger> LatestRootQueryAsync(LatestRootFunction latestRootFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<LatestRootFunction, BigInteger>(latestRootFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> LatestRootQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<LatestRootFunction, BigInteger>(null, blockParameter);
        }

        public Task<byte[]> ProxiableUUIDQueryAsync(ProxiableUUIDFunction proxiableUUIDFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ProxiableUUIDFunction, byte[]>(proxiableUUIDFunction, blockParameter);
        }

        
        public virtual Task<byte[]> ProxiableUUIDQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ProxiableUUIDFunction, byte[]>(null, blockParameter);
        }

        public virtual Task<string> RegisterPoolRequestAsync(RegisterPoolFunction registerPoolFunction)
        {
             return ContractHandler.SendRequestAsync(registerPoolFunction);
        }

        public virtual Task<TransactionReceipt> RegisterPoolRequestAndWaitForReceiptAsync(RegisterPoolFunction registerPoolFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerPoolFunction, cancellationToken);
        }

        public virtual Task<string> RegisterPoolRequestAsync(string asset, string pool, BigInteger minimumDepositAmount, BigInteger vettingFeeBPS, BigInteger maxRelayFeeBPS)
        {
            var registerPoolFunction = new RegisterPoolFunction();
                registerPoolFunction.Asset = asset;
                registerPoolFunction.Pool = pool;
                registerPoolFunction.MinimumDepositAmount = minimumDepositAmount;
                registerPoolFunction.VettingFeeBPS = vettingFeeBPS;
                registerPoolFunction.MaxRelayFeeBPS = maxRelayFeeBPS;
            
             return ContractHandler.SendRequestAsync(registerPoolFunction);
        }

        public virtual Task<TransactionReceipt> RegisterPoolRequestAndWaitForReceiptAsync(string asset, string pool, BigInteger minimumDepositAmount, BigInteger vettingFeeBPS, BigInteger maxRelayFeeBPS, CancellationTokenSource cancellationToken = null)
        {
            var registerPoolFunction = new RegisterPoolFunction();
                registerPoolFunction.Asset = asset;
                registerPoolFunction.Pool = pool;
                registerPoolFunction.MinimumDepositAmount = minimumDepositAmount;
                registerPoolFunction.VettingFeeBPS = vettingFeeBPS;
                registerPoolFunction.MaxRelayFeeBPS = maxRelayFeeBPS;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerPoolFunction, cancellationToken);
        }

        public virtual Task<string> RelayRequestAsync(RelayFunction relayFunction)
        {
             return ContractHandler.SendRequestAsync(relayFunction);
        }

        public virtual Task<TransactionReceipt> RelayRequestAndWaitForReceiptAsync(RelayFunction relayFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(relayFunction, cancellationToken);
        }

        public virtual Task<string> RelayRequestAsync(Withdrawal withdrawal, WithdrawProof proof, BigInteger scope)
        {
            var relayFunction = new RelayFunction();
                relayFunction.Withdrawal = withdrawal;
                relayFunction.Proof = proof;
                relayFunction.Scope = scope;
            
             return ContractHandler.SendRequestAsync(relayFunction);
        }

        public virtual Task<TransactionReceipt> RelayRequestAndWaitForReceiptAsync(Withdrawal withdrawal, WithdrawProof proof, BigInteger scope, CancellationTokenSource cancellationToken = null)
        {
            var relayFunction = new RelayFunction();
                relayFunction.Withdrawal = withdrawal;
                relayFunction.Proof = proof;
                relayFunction.Scope = scope;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(relayFunction, cancellationToken);
        }

        public virtual Task<string> RemovePoolRequestAsync(RemovePoolFunction removePoolFunction)
        {
             return ContractHandler.SendRequestAsync(removePoolFunction);
        }

        public virtual Task<TransactionReceipt> RemovePoolRequestAndWaitForReceiptAsync(RemovePoolFunction removePoolFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removePoolFunction, cancellationToken);
        }

        public virtual Task<string> RemovePoolRequestAsync(string asset)
        {
            var removePoolFunction = new RemovePoolFunction();
                removePoolFunction.Asset = asset;
            
             return ContractHandler.SendRequestAsync(removePoolFunction);
        }

        public virtual Task<TransactionReceipt> RemovePoolRequestAndWaitForReceiptAsync(string asset, CancellationTokenSource cancellationToken = null)
        {
            var removePoolFunction = new RemovePoolFunction();
                removePoolFunction.Asset = asset;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(removePoolFunction, cancellationToken);
        }

        public virtual Task<string> RenounceRoleRequestAsync(RenounceRoleFunction renounceRoleFunction)
        {
             return ContractHandler.SendRequestAsync(renounceRoleFunction);
        }

        public virtual Task<TransactionReceipt> RenounceRoleRequestAndWaitForReceiptAsync(RenounceRoleFunction renounceRoleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(renounceRoleFunction, cancellationToken);
        }

        public virtual Task<string> RenounceRoleRequestAsync(byte[] role, string callerConfirmation)
        {
            var renounceRoleFunction = new RenounceRoleFunction();
                renounceRoleFunction.Role = role;
                renounceRoleFunction.CallerConfirmation = callerConfirmation;
            
             return ContractHandler.SendRequestAsync(renounceRoleFunction);
        }

        public virtual Task<TransactionReceipt> RenounceRoleRequestAndWaitForReceiptAsync(byte[] role, string callerConfirmation, CancellationTokenSource cancellationToken = null)
        {
            var renounceRoleFunction = new RenounceRoleFunction();
                renounceRoleFunction.Role = role;
                renounceRoleFunction.CallerConfirmation = callerConfirmation;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(renounceRoleFunction, cancellationToken);
        }

        public virtual Task<string> RevokeRoleRequestAsync(RevokeRoleFunction revokeRoleFunction)
        {
             return ContractHandler.SendRequestAsync(revokeRoleFunction);
        }

        public virtual Task<TransactionReceipt> RevokeRoleRequestAndWaitForReceiptAsync(RevokeRoleFunction revokeRoleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeRoleFunction, cancellationToken);
        }

        public virtual Task<string> RevokeRoleRequestAsync(byte[] role, string account)
        {
            var revokeRoleFunction = new RevokeRoleFunction();
                revokeRoleFunction.Role = role;
                revokeRoleFunction.Account = account;
            
             return ContractHandler.SendRequestAsync(revokeRoleFunction);
        }

        public virtual Task<TransactionReceipt> RevokeRoleRequestAndWaitForReceiptAsync(byte[] role, string account, CancellationTokenSource cancellationToken = null)
        {
            var revokeRoleFunction = new RevokeRoleFunction();
                revokeRoleFunction.Role = role;
                revokeRoleFunction.Account = account;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(revokeRoleFunction, cancellationToken);
        }

        public Task<BigInteger> RootByIndexQueryAsync(RootByIndexFunction rootByIndexFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RootByIndexFunction, BigInteger>(rootByIndexFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> RootByIndexQueryAsync(BigInteger index, BlockParameter blockParameter = null)
        {
            var rootByIndexFunction = new RootByIndexFunction();
                rootByIndexFunction.Index = index;
            
            return ContractHandler.QueryAsync<RootByIndexFunction, BigInteger>(rootByIndexFunction, blockParameter);
        }

        public Task<string> ScopeToPoolQueryAsync(ScopeToPoolFunction scopeToPoolFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ScopeToPoolFunction, string>(scopeToPoolFunction, blockParameter);
        }

        
        public virtual Task<string> ScopeToPoolQueryAsync(BigInteger scope, BlockParameter blockParameter = null)
        {
            var scopeToPoolFunction = new ScopeToPoolFunction();
                scopeToPoolFunction.Scope = scope;
            
            return ContractHandler.QueryAsync<ScopeToPoolFunction, string>(scopeToPoolFunction, blockParameter);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        
        public virtual Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceId, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
                supportsInterfaceFunction.InterfaceId = interfaceId;
            
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        public virtual Task<string> UpdatePoolConfigurationRequestAsync(UpdatePoolConfigurationFunction updatePoolConfigurationFunction)
        {
             return ContractHandler.SendRequestAsync(updatePoolConfigurationFunction);
        }

        public virtual Task<TransactionReceipt> UpdatePoolConfigurationRequestAndWaitForReceiptAsync(UpdatePoolConfigurationFunction updatePoolConfigurationFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(updatePoolConfigurationFunction, cancellationToken);
        }

        public virtual Task<string> UpdatePoolConfigurationRequestAsync(string asset, BigInteger minimumDepositAmount, BigInteger vettingFeeBPS, BigInteger maxRelayFeeBPS)
        {
            var updatePoolConfigurationFunction = new UpdatePoolConfigurationFunction();
                updatePoolConfigurationFunction.Asset = asset;
                updatePoolConfigurationFunction.MinimumDepositAmount = minimumDepositAmount;
                updatePoolConfigurationFunction.VettingFeeBPS = vettingFeeBPS;
                updatePoolConfigurationFunction.MaxRelayFeeBPS = maxRelayFeeBPS;
            
             return ContractHandler.SendRequestAsync(updatePoolConfigurationFunction);
        }

        public virtual Task<TransactionReceipt> UpdatePoolConfigurationRequestAndWaitForReceiptAsync(string asset, BigInteger minimumDepositAmount, BigInteger vettingFeeBPS, BigInteger maxRelayFeeBPS, CancellationTokenSource cancellationToken = null)
        {
            var updatePoolConfigurationFunction = new UpdatePoolConfigurationFunction();
                updatePoolConfigurationFunction.Asset = asset;
                updatePoolConfigurationFunction.MinimumDepositAmount = minimumDepositAmount;
                updatePoolConfigurationFunction.VettingFeeBPS = vettingFeeBPS;
                updatePoolConfigurationFunction.MaxRelayFeeBPS = maxRelayFeeBPS;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(updatePoolConfigurationFunction, cancellationToken);
        }

        public virtual Task<string> UpdateRootRequestAsync(UpdateRootFunction updateRootFunction)
        {
             return ContractHandler.SendRequestAsync(updateRootFunction);
        }

        public virtual Task<TransactionReceipt> UpdateRootRequestAndWaitForReceiptAsync(UpdateRootFunction updateRootFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(updateRootFunction, cancellationToken);
        }

        public virtual Task<string> UpdateRootRequestAsync(BigInteger root, string ipfsCID)
        {
            var updateRootFunction = new UpdateRootFunction();
                updateRootFunction.Root = root;
                updateRootFunction.IpfsCID = ipfsCID;
            
             return ContractHandler.SendRequestAsync(updateRootFunction);
        }

        public virtual Task<TransactionReceipt> UpdateRootRequestAndWaitForReceiptAsync(BigInteger root, string ipfsCID, CancellationTokenSource cancellationToken = null)
        {
            var updateRootFunction = new UpdateRootFunction();
                updateRootFunction.Root = root;
                updateRootFunction.IpfsCID = ipfsCID;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(updateRootFunction, cancellationToken);
        }

        public virtual Task<string> UpgradeToAndCallRequestAsync(UpgradeToAndCallFunction upgradeToAndCallFunction)
        {
             return ContractHandler.SendRequestAsync(upgradeToAndCallFunction);
        }

        public virtual Task<TransactionReceipt> UpgradeToAndCallRequestAndWaitForReceiptAsync(UpgradeToAndCallFunction upgradeToAndCallFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(upgradeToAndCallFunction, cancellationToken);
        }

        public virtual Task<string> UpgradeToAndCallRequestAsync(string newImplementation, byte[] data)
        {
            var upgradeToAndCallFunction = new UpgradeToAndCallFunction();
                upgradeToAndCallFunction.NewImplementation = newImplementation;
                upgradeToAndCallFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(upgradeToAndCallFunction);
        }

        public virtual Task<TransactionReceipt> UpgradeToAndCallRequestAndWaitForReceiptAsync(string newImplementation, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var upgradeToAndCallFunction = new UpgradeToAndCallFunction();
                upgradeToAndCallFunction.NewImplementation = newImplementation;
                upgradeToAndCallFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(upgradeToAndCallFunction, cancellationToken);
        }

        public Task<bool> UsedPrecommitmentsQueryAsync(UsedPrecommitmentsFunction usedPrecommitmentsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<UsedPrecommitmentsFunction, bool>(usedPrecommitmentsFunction, blockParameter);
        }

        
        public virtual Task<bool> UsedPrecommitmentsQueryAsync(BigInteger precommitment, BlockParameter blockParameter = null)
        {
            var usedPrecommitmentsFunction = new UsedPrecommitmentsFunction();
                usedPrecommitmentsFunction.Precommitment = precommitment;
            
            return ContractHandler.QueryAsync<UsedPrecommitmentsFunction, bool>(usedPrecommitmentsFunction, blockParameter);
        }

        public virtual Task<string> WindDownPoolRequestAsync(WindDownPoolFunction windDownPoolFunction)
        {
             return ContractHandler.SendRequestAsync(windDownPoolFunction);
        }

        public virtual Task<TransactionReceipt> WindDownPoolRequestAndWaitForReceiptAsync(WindDownPoolFunction windDownPoolFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(windDownPoolFunction, cancellationToken);
        }

        public virtual Task<string> WindDownPoolRequestAsync(string pool)
        {
            var windDownPoolFunction = new WindDownPoolFunction();
                windDownPoolFunction.Pool = pool;
            
             return ContractHandler.SendRequestAsync(windDownPoolFunction);
        }

        public virtual Task<TransactionReceipt> WindDownPoolRequestAndWaitForReceiptAsync(string pool, CancellationTokenSource cancellationToken = null)
        {
            var windDownPoolFunction = new WindDownPoolFunction();
                windDownPoolFunction.Pool = pool;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(windDownPoolFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawFeesRequestAsync(WithdrawFeesFunction withdrawFeesFunction)
        {
             return ContractHandler.SendRequestAsync(withdrawFeesFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawFeesRequestAndWaitForReceiptAsync(WithdrawFeesFunction withdrawFeesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawFeesFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawFeesRequestAsync(string asset, string recipient)
        {
            var withdrawFeesFunction = new WithdrawFeesFunction();
                withdrawFeesFunction.Asset = asset;
                withdrawFeesFunction.Recipient = recipient;
            
             return ContractHandler.SendRequestAsync(withdrawFeesFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawFeesRequestAndWaitForReceiptAsync(string asset, string recipient, CancellationTokenSource cancellationToken = null)
        {
            var withdrawFeesFunction = new WithdrawFeesFunction();
                withdrawFeesFunction.Asset = asset;
                withdrawFeesFunction.Recipient = recipient;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawFeesFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(DefaultAdminRoleFunction),
                typeof(UpgradeInterfaceVersionFunction),
                typeof(AssetConfigFunction),
                typeof(AssociationSetsFunction),
                typeof(Deposit1Function),
                typeof(DepositFunction),
                typeof(GetRoleAdminFunction),
                typeof(GrantRoleFunction),
                typeof(HasRoleFunction),
                typeof(InitializeFunction),
                typeof(LatestRootFunction),
                typeof(ProxiableUUIDFunction),
                typeof(RegisterPoolFunction),
                typeof(RelayFunction),
                typeof(RemovePoolFunction),
                typeof(RenounceRoleFunction),
                typeof(RevokeRoleFunction),
                typeof(RootByIndexFunction),
                typeof(ScopeToPoolFunction),
                typeof(SupportsInterfaceFunction),
                typeof(UpdatePoolConfigurationFunction),
                typeof(UpdateRootFunction),
                typeof(UpgradeToAndCallFunction),
                typeof(UsedPrecommitmentsFunction),
                typeof(WindDownPoolFunction),
                typeof(WithdrawFeesFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(DepositedEventDTO),
                typeof(FeesWithdrawnEventDTO),
                typeof(InitializedEventDTO),
                typeof(PoolConfigurationUpdatedEventDTO),
                typeof(PoolRegisteredEventDTO),
                typeof(PoolRemovedEventDTO),
                typeof(PoolWindDownEventDTO),
                typeof(RoleAdminChangedEventDTO),
                typeof(RoleGrantedEventDTO),
                typeof(RoleRevokedEventDTO),
                typeof(RootUpdatedEventDTO),
                typeof(UpgradedEventDTO),
                typeof(WithdrawalRelayedEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(AccessControlBadConfirmationError),
                typeof(AccessControlUnauthorizedAccountError),
                typeof(AddressEmptyCodeError),
                typeof(AssetMismatchError),
                typeof(AssetPoolAlreadyRegisteredError),
                typeof(ERC1967InvalidImplementationError),
                typeof(ERC1967NonPayableError),
                typeof(EmptyRootError),
                typeof(FailedCallError),
                typeof(InvalidEntrypointForPoolError),
                typeof(InvalidFeeBPSError),
                typeof(InvalidIPFSCIDLengthError),
                typeof(InvalidIndexError),
                typeof(InvalidInitializationError),
                typeof(InvalidPoolStateError),
                typeof(InvalidProcessooorError),
                typeof(InvalidWithdrawalAmountError),
                typeof(MinimumDepositAmountError),
                typeof(NativeAssetNotAcceptedError),
                typeof(NativeAssetTransferFailedError),
                typeof(NoRootsAvailableError),
                typeof(NotInitializingError),
                typeof(PoolIsDeadError),
                typeof(PoolNotFoundError),
                typeof(PrecommitmentAlreadyUsedError),
                typeof(ReentrancyGuardReentrantCallError),
                typeof(RelayFeeGreaterThanMaxError),
                typeof(SafeERC20FailedOperationError),
                typeof(ScopePoolAlreadyRegisteredError),
                typeof(UUPSUnauthorizedCallContextError),
                typeof(UUPSUnsupportedProxiableUUIDError),
                typeof(ZeroAddressError)
            };
        }
    }
}
