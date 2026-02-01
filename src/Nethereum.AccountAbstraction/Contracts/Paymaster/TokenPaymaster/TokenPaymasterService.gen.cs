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
using Nethereum.AccountAbstraction.Contracts.Paymaster.TokenPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;

namespace Nethereum.AccountAbstraction.Contracts.Paymaster.TokenPaymaster
{
    public partial class TokenPaymasterService: TokenPaymasterServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, TokenPaymasterDeployment tokenPaymasterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<TokenPaymasterDeployment>().SendRequestAndWaitForReceiptAsync(tokenPaymasterDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, TokenPaymasterDeployment tokenPaymasterDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<TokenPaymasterDeployment>().SendRequestAsync(tokenPaymasterDeployment);
        }

        public static async Task<TokenPaymasterService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, TokenPaymasterDeployment tokenPaymasterDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, tokenPaymasterDeployment, cancellationTokenSource);
            return new TokenPaymasterService(web3, receipt.ContractAddress);
        }

        public TokenPaymasterService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

    }


    public partial class TokenPaymasterServiceBase: ContractWeb3ServiceBase
    {

        public TokenPaymasterServiceBase(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<BigInteger> MarkupDenominatorQueryAsync(MarkupDenominatorFunction markupDenominatorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MarkupDenominatorFunction, BigInteger>(markupDenominatorFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> MarkupDenominatorQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MarkupDenominatorFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> PriceDecimalsQueryAsync(PriceDecimalsFunction priceDecimalsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PriceDecimalsFunction, BigInteger>(priceDecimalsFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> PriceDecimalsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PriceDecimalsFunction, BigInteger>(null, blockParameter);
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

        public Task<BigInteger> EstimateTokenCostQueryAsync(EstimateTokenCostFunction estimateTokenCostFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<EstimateTokenCostFunction, BigInteger>(estimateTokenCostFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> EstimateTokenCostQueryAsync(BigInteger ethCost, BlockParameter blockParameter = null)
        {
            var estimateTokenCostFunction = new EstimateTokenCostFunction();
                estimateTokenCostFunction.EthCost = ethCost;
            
            return ContractHandler.QueryAsync<EstimateTokenCostFunction, BigInteger>(estimateTokenCostFunction, blockParameter);
        }

        public Task<BigInteger> GetCurrentTokenPriceQueryAsync(GetCurrentTokenPriceFunction getCurrentTokenPriceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetCurrentTokenPriceFunction, BigInteger>(getCurrentTokenPriceFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetCurrentTokenPriceQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetCurrentTokenPriceFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> GetDepositQueryAsync(GetDepositFunction getDepositFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDepositFunction, BigInteger>(getDepositFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> GetDepositQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDepositFunction, BigInteger>(null, blockParameter);
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

        public Task<BigInteger> PriceMarkupQueryAsync(PriceMarkupFunction priceMarkupFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PriceMarkupFunction, BigInteger>(priceMarkupFunction, blockParameter);
        }

        
        public virtual Task<BigInteger> PriceMarkupQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PriceMarkupFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> PriceOracleQueryAsync(PriceOracleFunction priceOracleFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PriceOracleFunction, string>(priceOracleFunction, blockParameter);
        }

        
        public virtual Task<string> PriceOracleQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PriceOracleFunction, string>(null, blockParameter);
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

        public virtual Task<string> SetPriceMarkupRequestAsync(SetPriceMarkupFunction setPriceMarkupFunction)
        {
             return ContractHandler.SendRequestAsync(setPriceMarkupFunction);
        }

        public virtual Task<TransactionReceipt> SetPriceMarkupRequestAndWaitForReceiptAsync(SetPriceMarkupFunction setPriceMarkupFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setPriceMarkupFunction, cancellationToken);
        }

        public virtual Task<string> SetPriceMarkupRequestAsync(BigInteger markup)
        {
            var setPriceMarkupFunction = new SetPriceMarkupFunction();
                setPriceMarkupFunction.Markup = markup;
            
             return ContractHandler.SendRequestAsync(setPriceMarkupFunction);
        }

        public virtual Task<TransactionReceipt> SetPriceMarkupRequestAndWaitForReceiptAsync(BigInteger markup, CancellationTokenSource cancellationToken = null)
        {
            var setPriceMarkupFunction = new SetPriceMarkupFunction();
                setPriceMarkupFunction.Markup = markup;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setPriceMarkupFunction, cancellationToken);
        }

        public virtual Task<string> SetPriceOracleRequestAsync(SetPriceOracleFunction setPriceOracleFunction)
        {
             return ContractHandler.SendRequestAsync(setPriceOracleFunction);
        }

        public virtual Task<TransactionReceipt> SetPriceOracleRequestAndWaitForReceiptAsync(SetPriceOracleFunction setPriceOracleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setPriceOracleFunction, cancellationToken);
        }

        public virtual Task<string> SetPriceOracleRequestAsync(string oracleAddress)
        {
            var setPriceOracleFunction = new SetPriceOracleFunction();
                setPriceOracleFunction.OracleAddress = oracleAddress;
            
             return ContractHandler.SendRequestAsync(setPriceOracleFunction);
        }

        public virtual Task<TransactionReceipt> SetPriceOracleRequestAndWaitForReceiptAsync(string oracleAddress, CancellationTokenSource cancellationToken = null)
        {
            var setPriceOracleFunction = new SetPriceOracleFunction();
                setPriceOracleFunction.OracleAddress = oracleAddress;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setPriceOracleFunction, cancellationToken);
        }

        public virtual Task<string> SetTokenRequestAsync(SetTokenFunction setTokenFunction)
        {
             return ContractHandler.SendRequestAsync(setTokenFunction);
        }

        public virtual Task<TransactionReceipt> SetTokenRequestAndWaitForReceiptAsync(SetTokenFunction setTokenFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setTokenFunction, cancellationToken);
        }

        public virtual Task<string> SetTokenRequestAsync(string tokenAddress)
        {
            var setTokenFunction = new SetTokenFunction();
                setTokenFunction.TokenAddress = tokenAddress;
            
             return ContractHandler.SendRequestAsync(setTokenFunction);
        }

        public virtual Task<TransactionReceipt> SetTokenRequestAndWaitForReceiptAsync(string tokenAddress, CancellationTokenSource cancellationToken = null)
        {
            var setTokenFunction = new SetTokenFunction();
                setTokenFunction.TokenAddress = tokenAddress;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setTokenFunction, cancellationToken);
        }

        public Task<string> TokenQueryAsync(TokenFunction tokenFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TokenFunction, string>(tokenFunction, blockParameter);
        }

        
        public virtual Task<string> TokenQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TokenFunction, string>(null, blockParameter);
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

        public virtual Task<string> WithdrawTokensRequestAsync(WithdrawTokensFunction withdrawTokensFunction)
        {
             return ContractHandler.SendRequestAsync(withdrawTokensFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawTokensRequestAndWaitForReceiptAsync(WithdrawTokensFunction withdrawTokensFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawTokensFunction, cancellationToken);
        }

        public virtual Task<string> WithdrawTokensRequestAsync(string to, BigInteger amount)
        {
            var withdrawTokensFunction = new WithdrawTokensFunction();
                withdrawTokensFunction.To = to;
                withdrawTokensFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(withdrawTokensFunction);
        }

        public virtual Task<TransactionReceipt> WithdrawTokensRequestAndWaitForReceiptAsync(string to, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var withdrawTokensFunction = new WithdrawTokensFunction();
                withdrawTokensFunction.To = to;
                withdrawTokensFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(withdrawTokensFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(MarkupDenominatorFunction),
                typeof(PriceDecimalsFunction),
                typeof(DepositFunction),
                typeof(EntryPointFunction),
                typeof(EstimateTokenCostFunction),
                typeof(GetCurrentTokenPriceFunction),
                typeof(GetDepositFunction),
                typeof(OwnerFunction),
                typeof(PostOpFunction),
                typeof(PriceMarkupFunction),
                typeof(PriceOracleFunction),
                typeof(RenounceOwnershipFunction),
                typeof(SetPriceMarkupFunction),
                typeof(SetPriceOracleFunction),
                typeof(SetTokenFunction),
                typeof(TokenFunction),
                typeof(TransferOwnershipFunction),
                typeof(ValidatePaymasterUserOpFunction),
                typeof(WithdrawToFunction),
                typeof(WithdrawTokensFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(OracleChangedEventDTO),
                typeof(OwnershipTransferredEventDTO),
                typeof(TokenChangedEventDTO),
                typeof(TokenPaymentEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(InsufficientDepositError),
                typeof(InsufficientTokenAllowanceError),
                typeof(InsufficientTokenBalanceError),
                typeof(InvalidMarkupError),
                typeof(OnlyEntryPointError),
                typeof(OwnableInvalidOwnerError),
                typeof(OwnableUnauthorizedAccountError),
                typeof(SafeERC20FailedOperationError),
                typeof(TokenTransferFailedError)
            };
        }
    }
}
