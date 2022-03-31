using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ENS.ETHRegistrarController.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.Standards.ENS
{
    public partial class ETHRegistrarControllerService
    {
        public string ContractAddress { get; }

        public ContractHandler ContractHandler { get; }

        public ETHRegistrarControllerService(IEthApiContractService ethApiContractService, string contractAddress)
        {
            ContractAddress = contractAddress;
#if !DOTNET35
            ContractHandler = ethApiContractService.GetContractHandler(contractAddress);
#endif
        }
#if !DOTNET35
        public Task<BigInteger> MinRegistrationDurationQueryAsync(MinRegistrationDurationFunction minRegistrationDurationFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MinRegistrationDurationFunction, BigInteger>(minRegistrationDurationFunction, blockParameter);
        }

        
        public Task<BigInteger> MinRegistrationDurationQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MinRegistrationDurationFunction, BigInteger>(null, blockParameter);
        }

        public Task<bool> AvailableQueryAsync(AvailableFunction availableFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AvailableFunction, bool>(availableFunction, blockParameter);
        }

        
        public Task<bool> AvailableQueryAsync(string name, BlockParameter blockParameter = null)
        {
            var availableFunction = new AvailableFunction();
                availableFunction.Name = name;
            
            return ContractHandler.QueryAsync<AvailableFunction, bool>(availableFunction, blockParameter);
        }

        public Task<string> CommitRequestAsync(CommitFunction commitFunction)
        {
             return ContractHandler.SendRequestAsync(commitFunction);
        }

        public Task<TransactionReceipt> CommitRequestAndWaitForReceiptAsync(CommitFunction commitFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(commitFunction, cancellationToken);
        }

        public Task<string> CommitRequestAsync(byte[] commitment)
        {
            var commitFunction = new CommitFunction();
                commitFunction.Commitment = commitment;
            
             return ContractHandler.SendRequestAsync(commitFunction);
        }

        public Task<TransactionReceipt> CommitRequestAndWaitForReceiptAsync(byte[] commitment, CancellationTokenSource cancellationToken = null)
        {
            var commitFunction = new CommitFunction();
                commitFunction.Commitment = commitment;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(commitFunction, cancellationToken);
        }

        public Task<BigInteger> CommitmentsQueryAsync(CommitmentsFunction commitmentsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CommitmentsFunction, BigInteger>(commitmentsFunction, blockParameter);
        }

        
        public Task<BigInteger> CommitmentsQueryAsync(byte[] returnValue1, BlockParameter blockParameter = null)
        {
            var commitmentsFunction = new CommitmentsFunction();
                commitmentsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<CommitmentsFunction, BigInteger>(commitmentsFunction, blockParameter);
        }

        public Task<bool> IsOwnerQueryAsync(IsOwnerFunction isOwnerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsOwnerFunction, bool>(isOwnerFunction, blockParameter);
        }

        
        public Task<bool> IsOwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsOwnerFunction, bool>(null, blockParameter);
        }

        public Task<byte[]> MakeCommitmentQueryAsync(MakeCommitmentFunction makeCommitmentFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MakeCommitmentFunction, byte[]>(makeCommitmentFunction, blockParameter);
        }

        
        public Task<byte[]> MakeCommitmentQueryAsync(string name, string owner, byte[] secret, BlockParameter blockParameter = null)
        {
            var makeCommitmentFunction = new MakeCommitmentFunction();
                makeCommitmentFunction.Name = name;
                makeCommitmentFunction.Owner = owner;
                makeCommitmentFunction.Secret = secret;
            
            return ContractHandler.QueryAsync<MakeCommitmentFunction, byte[]>(makeCommitmentFunction, blockParameter);
        }

        public Task<byte[]> MakeCommitmentWithConfigQueryAsync(MakeCommitmentWithConfigFunction makeCommitmentWithConfigFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MakeCommitmentWithConfigFunction, byte[]>(makeCommitmentWithConfigFunction, blockParameter);
        }

        
        public Task<byte[]> MakeCommitmentWithConfigQueryAsync(string name, string owner, byte[] secret, string resolver, string addr, BlockParameter blockParameter = null)
        {
            var makeCommitmentWithConfigFunction = new MakeCommitmentWithConfigFunction();
                makeCommitmentWithConfigFunction.Name = name;
                makeCommitmentWithConfigFunction.Owner = owner;
                makeCommitmentWithConfigFunction.Secret = secret;
                makeCommitmentWithConfigFunction.Resolver = resolver;
                makeCommitmentWithConfigFunction.Addr = addr;
            
            return ContractHandler.QueryAsync<MakeCommitmentWithConfigFunction, byte[]>(makeCommitmentWithConfigFunction, blockParameter);
        }

        public Task<BigInteger> MaxCommitmentAgeQueryAsync(MaxCommitmentAgeFunction maxCommitmentAgeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxCommitmentAgeFunction, BigInteger>(maxCommitmentAgeFunction, blockParameter);
        }

        
        public Task<BigInteger> MaxCommitmentAgeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MaxCommitmentAgeFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> MinCommitmentAgeQueryAsync(MinCommitmentAgeFunction minCommitmentAgeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MinCommitmentAgeFunction, BigInteger>(minCommitmentAgeFunction, blockParameter);
        }

        
        public Task<BigInteger> MinCommitmentAgeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MinCommitmentAgeFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> OwnerQueryAsync(OwnerFunction ownerFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(ownerFunction, blockParameter);
        }

        
        public Task<string> OwnerQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OwnerFunction, string>(null, blockParameter);
        }

        public Task<string> RegisterRequestAsync(RegisterFunction registerFunction)
        {
             return ContractHandler.SendRequestAsync(registerFunction);
        }

        public Task<TransactionReceipt> RegisterRequestAndWaitForReceiptAsync(RegisterFunction registerFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunction, cancellationToken);
        }

        public Task<string> RegisterRequestAsync(string name, string owner, BigInteger duration, byte[] secret)
        {
            var registerFunction = new RegisterFunction();
                registerFunction.Name = name;
                registerFunction.Owner = owner;
                registerFunction.Duration = duration;
                registerFunction.Secret = secret;
            
             return ContractHandler.SendRequestAsync(registerFunction);
        }

        public Task<TransactionReceipt> RegisterRequestAndWaitForReceiptAsync(string name, string owner, BigInteger duration, byte[] secret, CancellationTokenSource cancellationToken = null)
        {
            var registerFunction = new RegisterFunction();
                registerFunction.Name = name;
                registerFunction.Owner = owner;
                registerFunction.Duration = duration;
                registerFunction.Secret = secret;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunction, cancellationToken);
        }

        public Task<string> RegisterWithConfigRequestAsync(RegisterWithConfigFunction registerWithConfigFunction)
        {
             return ContractHandler.SendRequestAsync(registerWithConfigFunction);
        }

        public Task<TransactionReceipt> RegisterWithConfigRequestAndWaitForReceiptAsync(RegisterWithConfigFunction registerWithConfigFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerWithConfigFunction, cancellationToken);
        }

        public Task<string> RegisterWithConfigRequestAsync(string name, string owner, BigInteger duration, byte[] secret, string resolver, string addr)
        {
            var registerWithConfigFunction = new RegisterWithConfigFunction();
                registerWithConfigFunction.Name = name;
                registerWithConfigFunction.Owner = owner;
                registerWithConfigFunction.Duration = duration;
                registerWithConfigFunction.Secret = secret;
                registerWithConfigFunction.Resolver = resolver;
                registerWithConfigFunction.Addr = addr;
            
             return ContractHandler.SendRequestAsync(registerWithConfigFunction);
        }

        public Task<TransactionReceipt> RegisterWithConfigRequestAndWaitForReceiptAsync(string name, string owner, BigInteger duration, byte[] secret, string resolver, string addr, CancellationTokenSource cancellationToken = null)
        {
            var registerWithConfigFunction = new RegisterWithConfigFunction();
                registerWithConfigFunction.Name = name;
                registerWithConfigFunction.Owner = owner;
                registerWithConfigFunction.Duration = duration;
                registerWithConfigFunction.Secret = secret;
                registerWithConfigFunction.Resolver = resolver;
                registerWithConfigFunction.Addr = addr;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerWithConfigFunction, cancellationToken);
        }

        public Task<string> RenewRequestAsync(RenewFunction renewFunction)
        {
             return ContractHandler.SendRequestAsync(renewFunction);
        }

        public Task<TransactionReceipt> RenewRequestAndWaitForReceiptAsync(RenewFunction renewFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(renewFunction, cancellationToken);
        }

        public Task<string> RenewRequestAsync(string name, BigInteger duration)
        {
            var renewFunction = new RenewFunction();
                renewFunction.Name = name;
                renewFunction.Duration = duration;
            
             return ContractHandler.SendRequestAsync(renewFunction);
        }

        public Task<TransactionReceipt> RenewRequestAndWaitForReceiptAsync(string name, BigInteger duration, CancellationTokenSource cancellationToken = null)
        {
            var renewFunction = new RenewFunction();
                renewFunction.Name = name;
                renewFunction.Duration = duration;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(renewFunction, cancellationToken);
        }

        public Task<string> RenounceOwnershipRequestAsync(RenounceOwnershipFunction renounceOwnershipFunction)
        {
             return ContractHandler.SendRequestAsync(renounceOwnershipFunction);
        }

        public Task<string> RenounceOwnershipRequestAsync()
        {
             return ContractHandler.SendRequestAsync<RenounceOwnershipFunction>();
        }

        public Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(RenounceOwnershipFunction renounceOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(renounceOwnershipFunction, cancellationToken);
        }

        public Task<TransactionReceipt> RenounceOwnershipRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<RenounceOwnershipFunction>(null, cancellationToken);
        }

        public Task<BigInteger> RentPriceQueryAsync(RentPriceFunction rentPriceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<RentPriceFunction, BigInteger>(rentPriceFunction, blockParameter);
        }

        
        public Task<BigInteger> RentPriceQueryAsync(string name, BigInteger duration, BlockParameter blockParameter = null)
        {
            var rentPriceFunction = new RentPriceFunction();
                rentPriceFunction.Name = name;
                rentPriceFunction.Duration = duration;
            
            return ContractHandler.QueryAsync<RentPriceFunction, BigInteger>(rentPriceFunction, blockParameter);
        }

        public Task<string> SetCommitmentAgesRequestAsync(SetCommitmentAgesFunction setCommitmentAgesFunction)
        {
             return ContractHandler.SendRequestAsync(setCommitmentAgesFunction);
        }

        public Task<TransactionReceipt> SetCommitmentAgesRequestAndWaitForReceiptAsync(SetCommitmentAgesFunction setCommitmentAgesFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setCommitmentAgesFunction, cancellationToken);
        }

        public Task<string> SetCommitmentAgesRequestAsync(BigInteger minCommitmentAge, BigInteger maxCommitmentAge)
        {
            var setCommitmentAgesFunction = new SetCommitmentAgesFunction();
                setCommitmentAgesFunction.MinCommitmentAge = minCommitmentAge;
                setCommitmentAgesFunction.MaxCommitmentAge = maxCommitmentAge;
            
             return ContractHandler.SendRequestAsync(setCommitmentAgesFunction);
        }

        public Task<TransactionReceipt> SetCommitmentAgesRequestAndWaitForReceiptAsync(BigInteger minCommitmentAge, BigInteger maxCommitmentAge, CancellationTokenSource cancellationToken = null)
        {
            var setCommitmentAgesFunction = new SetCommitmentAgesFunction();
                setCommitmentAgesFunction.MinCommitmentAge = minCommitmentAge;
                setCommitmentAgesFunction.MaxCommitmentAge = maxCommitmentAge;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setCommitmentAgesFunction, cancellationToken);
        }

        public Task<string> SetPriceOracleRequestAsync(SetPriceOracleFunction setPriceOracleFunction)
        {
             return ContractHandler.SendRequestAsync(setPriceOracleFunction);
        }

        public Task<TransactionReceipt> SetPriceOracleRequestAndWaitForReceiptAsync(SetPriceOracleFunction setPriceOracleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setPriceOracleFunction, cancellationToken);
        }

        public Task<string> SetPriceOracleRequestAsync(string prices)
        {
            var setPriceOracleFunction = new SetPriceOracleFunction();
                setPriceOracleFunction.Prices = prices;
            
             return ContractHandler.SendRequestAsync(setPriceOracleFunction);
        }

        public Task<TransactionReceipt> SetPriceOracleRequestAndWaitForReceiptAsync(string prices, CancellationTokenSource cancellationToken = null)
        {
            var setPriceOracleFunction = new SetPriceOracleFunction();
                setPriceOracleFunction.Prices = prices;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setPriceOracleFunction, cancellationToken);
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

        public Task<string> TransferOwnershipRequestAsync(TransferOwnershipFunction transferOwnershipFunction)
        {
             return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(TransferOwnershipFunction transferOwnershipFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public Task<string> TransferOwnershipRequestAsync(string newOwner)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
                transferOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAsync(transferOwnershipFunction);
        }

        public Task<TransactionReceipt> TransferOwnershipRequestAndWaitForReceiptAsync(string newOwner, CancellationTokenSource cancellationToken = null)
        {
            var transferOwnershipFunction = new TransferOwnershipFunction();
                transferOwnershipFunction.NewOwner = newOwner;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferOwnershipFunction, cancellationToken);
        }

        public Task<bool> ValidQueryAsync(ValidFunction validFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ValidFunction, bool>(validFunction, blockParameter);
        }

        
        public Task<bool> ValidQueryAsync(string name, BlockParameter blockParameter = null)
        {
            var validFunction = new ValidFunction();
                validFunction.Name = name;
            
            return ContractHandler.QueryAsync<ValidFunction, bool>(validFunction, blockParameter);
        }

        public Task<string> WithdrawRequestAsync(WithdrawFunction withdrawFunction)
        {
             return ContractHandler.SendRequestAsync(withdrawFunction);
        }

        public Task<string> WithdrawRequestAsync()
        {
             return ContractHandler.SendRequestAsync<WithdrawFunction>();
        }

        public Task<TransactionReceipt> WithdrawRequestAndWaitForReceiptAsync(WithdrawFunction withdrawFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawFunction, cancellationToken);
        }

        public Task<TransactionReceipt> WithdrawRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<WithdrawFunction>(null, cancellationToken);
        }
#endif
    }
}
