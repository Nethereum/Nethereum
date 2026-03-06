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
using Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.AppChain.Contracts.Paymaster.SponsoredPaymaster
{
    public partial class SponsoredPaymasterService: SponsoredPaymasterServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, SponsoredPaymasterDeployment sponsoredPaymasterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<SponsoredPaymasterDeployment>().SendRequestAndWaitForReceiptAsync(sponsoredPaymasterDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, SponsoredPaymasterDeployment sponsoredPaymasterDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<SponsoredPaymasterDeployment>().SendRequestAsync(sponsoredPaymasterDeployment);
        }

        public static async Task<SponsoredPaymasterService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, SponsoredPaymasterDeployment sponsoredPaymasterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, sponsoredPaymasterDeployment, cancellationTokenSource);
            return new SponsoredPaymasterService(web3, receipt.ContractAddress);
        }

        public SponsoredPaymasterService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class SponsoredPaymasterServiceBase: ContractWeb3ServiceBase
    {

        public SponsoredPaymasterServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
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

        public Task<byte[]> SponsorRoleQueryAsync(SponsorRoleFunction sponsorRoleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SponsorRoleFunction, byte[]>(sponsorRoleFunction, blockParameter);
        }

        
        public virtual Task<byte[]> SponsorRoleQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SponsorRoleFunction, byte[]>(null, blockParameter);
        }

        public Task<string> AccountRegistryQueryAsync(AccountRegistryFunction accountRegistryFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AccountRegistryFunction, string>(accountRegistryFunction, blockParameter);
        }

        
        public virtual Task<string> AccountRegistryQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AccountRegistryFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> DailySponsoredQueryAsync(DailySponsoredFunction dailySponsoredFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DailySponsoredFunction, BigInteger>(dailySponsoredFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> DailySponsoredQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var dailySponsoredFunction = new DailySponsoredFunction();
                dailySponsoredFunction.Account = account;
            
            return ContractHandler.QueryAsync<DailySponsoredFunction, BigInteger>(dailySponsoredFunction, blockParameter);
        }

        public virtual Task<string> DepositRequestAsync(DepositFunction depositFunction)
        {
             return ContractHandler.SendRequestAsync(depositFunction);
        }

        public virtual Task<string> DepositRequestAsync()
        {
             return ContractHandler.SendRequestAsync<DepositFunction>();
        }

        public virtual Task<TransactionReceipt> DepositRequestAndWaitForReceiptAsync(DepositFunction depositFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(depositFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> DepositRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<DepositFunction>(null, cancellationToken);
        }

        public Task<string> EntryPointQueryAsync(EntryPointFunction entryPointFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EntryPointFunction, string>(entryPointFunction, blockParameter);
        }

        
        public virtual Task<string> EntryPointQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EntryPointFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> GetDepositQueryAsync(GetDepositFunction getDepositFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDepositFunction, BigInteger>(getDepositFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetDepositQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDepositFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> GetRemainingDailySponsorshipQueryAsync(GetRemainingDailySponsorshipFunction getRemainingDailySponsorshipFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetRemainingDailySponsorshipFunction, BigInteger>(getRemainingDailySponsorshipFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetRemainingDailySponsorshipQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var getRemainingDailySponsorshipFunction = new GetRemainingDailySponsorshipFunction();
                getRemainingDailySponsorshipFunction.Account = account;
            
            return ContractHandler.QueryAsync<GetRemainingDailySponsorshipFunction, BigInteger>(getRemainingDailySponsorshipFunction, blockParameter);
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

        public virtual Task<GetSponsorshipStatsOutputDTO> GetSponsorshipStatsQueryAsync(GetSponsorshipStatsFunction getSponsorshipStatsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetSponsorshipStatsFunction, GetSponsorshipStatsOutputDTO>(getSponsorshipStatsFunction, blockParameter);
        }

        public virtual Task<GetSponsorshipStatsOutputDTO> GetSponsorshipStatsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetSponsorshipStatsFunction, GetSponsorshipStatsOutputDTO>(null, blockParameter);
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

        public Task<ulong> LastSponsorDayQueryAsync(LastSponsorDayFunction lastSponsorDayFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<LastSponsorDayFunction, ulong>(lastSponsorDayFunction, blockParameter);
        }

        
        public virtual Task<ulong> LastSponsorDayQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var lastSponsorDayFunction = new LastSponsorDayFunction();
                lastSponsorDayFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<LastSponsorDayFunction, ulong>(lastSponsorDayFunction, blockParameter);
        }

        public Task<BigInteger> MaxDailySponsorPerUserQueryAsync(MaxDailySponsorPerUserFunction maxDailySponsorPerUserFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxDailySponsorPerUserFunction, BigInteger>(maxDailySponsorPerUserFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> MaxDailySponsorPerUserQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxDailySponsorPerUserFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> MaxTotalDailySponsorshipQueryAsync(MaxTotalDailySponsorshipFunction maxTotalDailySponsorshipFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxTotalDailySponsorshipFunction, BigInteger>(maxTotalDailySponsorshipFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> MaxTotalDailySponsorshipQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxTotalDailySponsorshipFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public virtual Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public virtual Task<string> PostOpRequestAsync(PostOpFunction postOpFunction)
        {
             return ContractHandler.SendRequestAsync(postOpFunction);
        }

        public virtual Task<TransactionReceipt> PostOpRequestAndWaitForReceiptAsync(PostOpFunction postOpFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postOpFunction, cancellationToken);
        }

        public virtual Task<string> PostOpRequestAsync(byte mode, byte[] context, BigInteger actualGasCost, BigInteger returnValue4)
        {
            var postOpFunction = new PostOpFunction();
                postOpFunction.Mode = mode;
                postOpFunction.Context = context;
                postOpFunction.ActualGasCost = actualGasCost;
                postOpFunction.ReturnValue4 = returnValue4;
            
             return ContractHandler.SendRequestAsync(postOpFunction);
        }

        public virtual Task<TransactionReceipt> PostOpRequestAndWaitForReceiptAsync(byte mode, byte[] context, BigInteger actualGasCost, BigInteger returnValue4, CancellationTokenSource cancellationToken = null)
        {
            var postOpFunction = new PostOpFunction();
                postOpFunction.Mode = mode;
                postOpFunction.Context = context;
                postOpFunction.ActualGasCost = actualGasCost;
                postOpFunction.ReturnValue4 = returnValue4;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(postOpFunction, cancellationToken);
        }

        public virtual Task<string> RenounceOwnershipRequestAsync(RenounceOwnershipFunction renounceOwnershipFunction)
        {
             return ContractHandler.SendRequestAsync(renounceOwnershipFunction);
        }

        public virtual Task<string> RenounceOwnershipRequestAsync()
        {
             return ContractHandler.SendRequestAsync<RenounceOwnershipFunction>();
        }

        public virtual Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(RenounceOwnershipFunction renounceOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(renounceOwnershipFunction, cancellationToken);
        }

        public virtual Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<RenounceOwnershipFunction>(null, cancellationToken);
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

        public virtual Task<string> SetAccountRegistryRequestAsync(SetAccountRegistryFunction setAccountRegistryFunction)
        {
             return ContractHandler.SendRequestAsync(setAccountRegistryFunction);
        }

        public virtual Task<TransactionReceipt> SetAccountRegistryRequestAndWaitForReceiptAsync(SetAccountRegistryFunction setAccountRegistryFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setAccountRegistryFunction, cancellationToken);
        }

        public virtual Task<string> SetAccountRegistryRequestAsync(string registry)
        {
            var setAccountRegistryFunction = new SetAccountRegistryFunction();
                setAccountRegistryFunction.Registry = registry;
            
             return ContractHandler.SendRequestAsync(setAccountRegistryFunction);
        }

        public virtual Task<TransactionReceipt> SetAccountRegistryRequestAndWaitForReceiptAsync(string registry, CancellationTokenSource cancellationToken = null)
        {
            var setAccountRegistryFunction = new SetAccountRegistryFunction();
                setAccountRegistryFunction.Registry = registry;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setAccountRegistryFunction, cancellationToken);
        }

        public virtual Task<string> SetMaxDailySponsorPerUserRequestAsync(SetMaxDailySponsorPerUserFunction setMaxDailySponsorPerUserFunction)
        {
             return ContractHandler.SendRequestAsync(setMaxDailySponsorPerUserFunction);
        }

        public virtual Task<TransactionReceipt> SetMaxDailySponsorPerUserRequestAndWaitForReceiptAsync(SetMaxDailySponsorPerUserFunction setMaxDailySponsorPerUserFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setMaxDailySponsorPerUserFunction, cancellationToken);
        }

        public virtual Task<string> SetMaxDailySponsorPerUserRequestAsync(BigInteger amount)
        {
            var setMaxDailySponsorPerUserFunction = new SetMaxDailySponsorPerUserFunction();
                setMaxDailySponsorPerUserFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(setMaxDailySponsorPerUserFunction);
        }

        public virtual Task<TransactionReceipt> SetMaxDailySponsorPerUserRequestAndWaitForReceiptAsync(BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var setMaxDailySponsorPerUserFunction = new SetMaxDailySponsorPerUserFunction();
                setMaxDailySponsorPerUserFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setMaxDailySponsorPerUserFunction, cancellationToken);
        }

        public virtual Task<string> SetMaxTotalDailySponsorshipRequestAsync(SetMaxTotalDailySponsorshipFunction setMaxTotalDailySponsorshipFunction)
        {
             return ContractHandler.SendRequestAsync(setMaxTotalDailySponsorshipFunction);
        }

        public virtual Task<TransactionReceipt> SetMaxTotalDailySponsorshipRequestAndWaitForReceiptAsync(SetMaxTotalDailySponsorshipFunction setMaxTotalDailySponsorshipFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setMaxTotalDailySponsorshipFunction, cancellationToken);
        }

        public virtual Task<string> SetMaxTotalDailySponsorshipRequestAsync(BigInteger amount)
        {
            var setMaxTotalDailySponsorshipFunction = new SetMaxTotalDailySponsorshipFunction();
                setMaxTotalDailySponsorshipFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(setMaxTotalDailySponsorshipFunction);
        }

        public virtual Task<TransactionReceipt> SetMaxTotalDailySponsorshipRequestAndWaitForReceiptAsync(BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var setMaxTotalDailySponsorshipFunction = new SetMaxTotalDailySponsorshipFunction();
                setMaxTotalDailySponsorshipFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setMaxTotalDailySponsorshipFunction, cancellationToken);
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

        public Task<BigInteger> TotalSponsoredQueryAsync(TotalSponsoredFunction totalSponsoredFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSponsoredFunction, BigInteger>(totalSponsoredFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> TotalSponsoredQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TotalSponsoredFunction, BigInteger>(null, blockParameter);
        }

        public virtual Task<string> TransferOwnershipRequestAsync(TransferOwnershipFunction transferOwnershipFunction)
        {
             return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public virtual Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(TransferOwnershipFunction transferOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public virtual Task<string> TransferOwnershipRequestAsync(string newOwner)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
                transferOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public virtual Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
                transferOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public virtual Task<string> ValidatePaymasterUserOpRequestAsync(ValidatePaymasterUserOpFunction validatePaymasterUserOpFunction)
        {
             return ContractHandler.SendRequestAsync(validatePaymasterUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidatePaymasterUserOpRequestAndWaitForReceiptAsync(ValidatePaymasterUserOpFunction validatePaymasterUserOpFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validatePaymasterUserOpFunction, cancellationToken);
        }

        public virtual Task<string> ValidatePaymasterUserOpRequestAsync(PackedUserOperation userOp, byte[] returnValue2, BigInteger maxCost)
        {
            var validatePaymasterUserOpFunction = new ValidatePaymasterUserOpFunction();
                validatePaymasterUserOpFunction.UserOp = userOp;
                validatePaymasterUserOpFunction.ReturnValue2 = returnValue2;
                validatePaymasterUserOpFunction.MaxCost = maxCost;
            
             return ContractHandler.SendRequestAsync(validatePaymasterUserOpFunction);
        }

        public virtual Task<TransactionReceipt> ValidatePaymasterUserOpRequestAndWaitForReceiptAsync(PackedUserOperation userOp, byte[] returnValue2, BigInteger maxCost, CancellationTokenSource cancellationToken = null)
        {
            var validatePaymasterUserOpFunction = new ValidatePaymasterUserOpFunction();
                validatePaymasterUserOpFunction.UserOp = userOp;
                validatePaymasterUserOpFunction.ReturnValue2 = returnValue2;
                validatePaymasterUserOpFunction.MaxCost = maxCost;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(validatePaymasterUserOpFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawToRequestAsync(WithdrawToFunction withdrawToFunction)
        {
             return ContractHandler.SendRequestAsync(withdrawToFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawToRequestAndWaitForReceiptAsync(WithdrawToFunction withdrawToFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawToFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawToRequestAsync(string to, BigInteger amount)
        {
            var withdrawToFunction = new WithdrawToFunction();
                withdrawToFunction.To = to;
                withdrawToFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(withdrawToFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawToRequestAndWaitForReceiptAsync(string to, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var withdrawToFunction = new WithdrawToFunction();
                withdrawToFunction.To = to;
                withdrawToFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawToFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(DefaultAdminRoleFunction),
                typeof(SponsorRoleFunction),
                typeof(AccountRegistryFunction),
                typeof(DailySponsoredFunction),
                typeof(DepositFunction),
                typeof(EntryPointFunction),
                typeof(GetDepositFunction),
                typeof(GetRemainingDailySponsorshipFunction),
                typeof(GetRoleAdminFunction),
                typeof(GetSponsorshipStatsFunction),
                typeof(GrantRoleFunction),
                typeof(HasRoleFunction),
                typeof(LastSponsorDayFunction),
                typeof(MaxDailySponsorPerUserFunction),
                typeof(MaxTotalDailySponsorshipFunction),
                typeof(OwnerFunction),
                typeof(PostOpFunction),
                typeof(RenounceOwnershipFunction),
                typeof(RenounceRoleFunction),
                typeof(RevokeRoleFunction),
                typeof(SetAccountRegistryFunction),
                typeof(SetMaxDailySponsorPerUserFunction),
                typeof(SetMaxTotalDailySponsorshipFunction),
                typeof(SupportsInterfaceFunction),
                typeof(TotalSponsoredFunction),
                typeof(TransferOwnershipFunction),
                typeof(ValidatePaymasterUserOpFunction),
                typeof(WithdrawToFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(AccountRegistryChangedEventDTO),
                typeof(GasSponsoredEventDTO),
                typeof(MaxTotalSponsorshipChangedEventDTO),
                typeof(OwnershipTransferredEventDTO),
                typeof(RoleAdminChangedEventDTO),
                typeof(RoleGrantedEventDTO),
                typeof(RoleRevokedEventDTO),
                typeof(SponsorLimitSetEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(AccessControlBadConfirmationError),
                typeof(AccessControlUnauthorizedAccountError),
                typeof(AccountNotActiveError),
                typeof(DailySponsorLimitExceededError),
                typeof(InsufficientDepositError),
                typeof(OnlyEntryPointError),
                typeof(OwnableInvalidOwnerError),
                typeof(OwnableUnauthorizedAccountError),
                typeof(TotalDailySponsorLimitExceededError)
            };
        }
    }
}
