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
using Nethereum.Circles.Contracts.InflationaryCircles.ContractDefinition;

namespace Nethereum.Circles.Contracts.InflationaryCircles
{
    public partial class InflationaryCirclesService: ContractWeb3ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, InflationaryCirclesDeployment inflationaryCirclesDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<InflationaryCirclesDeployment>().SendRequestAndWaitForReceiptAsync(inflationaryCirclesDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, InflationaryCirclesDeployment inflationaryCirclesDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<InflationaryCirclesDeployment>().SendRequestAsync(inflationaryCirclesDeployment);
        }

        public static async Task<InflationaryCirclesService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, InflationaryCirclesDeployment inflationaryCirclesDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, inflationaryCirclesDeployment, cancellationTokenSource);
            return new InflationaryCirclesService(web3, receipt.ContractAddress);
        }

        public InflationaryCirclesService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<byte[]> DomainSeparatorQueryAsync(DomainSeparatorFunction domainSeparatorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DomainSeparatorFunction, byte[]>(domainSeparatorFunction, blockParameter);
        }

        
        public Task<byte[]> DomainSeparatorQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DomainSeparatorFunction, byte[]>(null, blockParameter);
        }

        public Task<BigInteger> AllowanceQueryAsync(AllowanceFunction allowanceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AllowanceFunction, BigInteger>(allowanceFunction, blockParameter);
        }

        
        public Task<BigInteger> AllowanceQueryAsync(string owner, string spender, BlockParameter blockParameter = null)
        {
            var allowanceFunction = new AllowanceFunction();
                allowanceFunction.Owner = owner;
                allowanceFunction.Spender = spender;
            
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

        public Task<string> ApproveRequestAsync(string spender, BigInteger amount)
        {
            var approveFunction = new ApproveFunction();
                approveFunction.Spender = spender;
                approveFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(approveFunction);
        }

        public Task<TransactionReceipt> ApproveRequestAndWaitForReceiptAsync(string spender, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var approveFunction = new ApproveFunction();
                approveFunction.Spender = spender;
                approveFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(approveFunction, cancellationToken);
        }

        public Task<string> AvatarQueryAsync(AvatarFunction avatarFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AvatarFunction, string>(avatarFunction, blockParameter);
        }

        
        public Task<string> AvatarQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AvatarFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        
        public Task<BigInteger> BalanceOfQueryAsync(string account, BlockParameter blockParameter = null)
        {
            var balanceOfFunction = new BalanceOfFunction();
                balanceOfFunction.Account = account;
            
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        public Task<BigInteger> CirclesIdentifierQueryAsync(CirclesIdentifierFunction circlesIdentifierFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CirclesIdentifierFunction, BigInteger>(circlesIdentifierFunction, blockParameter);
        }

        
        public Task<BigInteger> CirclesIdentifierQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CirclesIdentifierFunction, BigInteger>(null, blockParameter);
        }

        public Task<List<BigInteger>> ConvertBatchDemurrageToInflationaryValuesQueryAsync(ConvertBatchDemurrageToInflationaryValuesFunction convertBatchDemurrageToInflationaryValuesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ConvertBatchDemurrageToInflationaryValuesFunction, List<BigInteger>>(convertBatchDemurrageToInflationaryValuesFunction, blockParameter);
        }

        
        public Task<List<BigInteger>> ConvertBatchDemurrageToInflationaryValuesQueryAsync(List<BigInteger> demurrageValues, ulong dayUpdated, BlockParameter blockParameter = null)
        {
            var convertBatchDemurrageToInflationaryValuesFunction = new ConvertBatchDemurrageToInflationaryValuesFunction();
                convertBatchDemurrageToInflationaryValuesFunction.DemurrageValues = demurrageValues;
                convertBatchDemurrageToInflationaryValuesFunction.DayUpdated = dayUpdated;
            
            return ContractHandler.QueryAsync<ConvertBatchDemurrageToInflationaryValuesFunction, List<BigInteger>>(convertBatchDemurrageToInflationaryValuesFunction, blockParameter);
        }

        public Task<List<BigInteger>> ConvertBatchInflationaryToDemurrageValuesQueryAsync(ConvertBatchInflationaryToDemurrageValuesFunction convertBatchInflationaryToDemurrageValuesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ConvertBatchInflationaryToDemurrageValuesFunction, List<BigInteger>>(convertBatchInflationaryToDemurrageValuesFunction, blockParameter);
        }

        
        public Task<List<BigInteger>> ConvertBatchInflationaryToDemurrageValuesQueryAsync(List<BigInteger> inflationaryValues, ulong day, BlockParameter blockParameter = null)
        {
            var convertBatchInflationaryToDemurrageValuesFunction = new ConvertBatchInflationaryToDemurrageValuesFunction();
                convertBatchInflationaryToDemurrageValuesFunction.InflationaryValues = inflationaryValues;
                convertBatchInflationaryToDemurrageValuesFunction.Day = day;
            
            return ContractHandler.QueryAsync<ConvertBatchInflationaryToDemurrageValuesFunction, List<BigInteger>>(convertBatchInflationaryToDemurrageValuesFunction, blockParameter);
        }

        public Task<BigInteger> ConvertDemurrageToInflationaryValueQueryAsync(ConvertDemurrageToInflationaryValueFunction convertDemurrageToInflationaryValueFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ConvertDemurrageToInflationaryValueFunction, BigInteger>(convertDemurrageToInflationaryValueFunction, blockParameter);
        }

        
        public Task<BigInteger> ConvertDemurrageToInflationaryValueQueryAsync(BigInteger demurrageValue, ulong dayUpdated, BlockParameter blockParameter = null)
        {
            var convertDemurrageToInflationaryValueFunction = new ConvertDemurrageToInflationaryValueFunction();
                convertDemurrageToInflationaryValueFunction.DemurrageValue = demurrageValue;
                convertDemurrageToInflationaryValueFunction.DayUpdated = dayUpdated;
            
            return ContractHandler.QueryAsync<ConvertDemurrageToInflationaryValueFunction, BigInteger>(convertDemurrageToInflationaryValueFunction, blockParameter);
        }

        public Task<BigInteger> ConvertInflationaryToDemurrageValueQueryAsync(ConvertInflationaryToDemurrageValueFunction convertInflationaryToDemurrageValueFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ConvertInflationaryToDemurrageValueFunction, BigInteger>(convertInflationaryToDemurrageValueFunction, blockParameter);
        }

        
        public Task<BigInteger> ConvertInflationaryToDemurrageValueQueryAsync(BigInteger inflationaryValue, ulong day, BlockParameter blockParameter = null)
        {
            var convertInflationaryToDemurrageValueFunction = new ConvertInflationaryToDemurrageValueFunction();
                convertInflationaryToDemurrageValueFunction.InflationaryValue = inflationaryValue;
                convertInflationaryToDemurrageValueFunction.Day = day;
            
            return ContractHandler.QueryAsync<ConvertInflationaryToDemurrageValueFunction, BigInteger>(convertInflationaryToDemurrageValueFunction, blockParameter);
        }

        public Task<ulong> DayQueryAsync(DayFunction dayFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DayFunction, ulong>(dayFunction, blockParameter);
        }

        
        public Task<ulong> DayQueryAsync(BigInteger timestamp, BlockParameter blockParameter = null)
        {
            var dayFunction = new DayFunction();
                dayFunction.Timestamp = timestamp;
            
            return ContractHandler.QueryAsync<DayFunction, ulong>(dayFunction, blockParameter);
        }

        public Task<byte> DecimalsQueryAsync(DecimalsFunction decimalsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DecimalsFunction, byte>(decimalsFunction, blockParameter);
        }

        
        public Task<byte> DecimalsQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<DecimalsFunction, byte>(null, blockParameter);
        }

        public Task<string> DecreaseAllowanceRequestAsync(DecreaseAllowanceFunction decreaseAllowanceFunction)
        {
             return ContractHandler.SendRequestAsync(decreaseAllowanceFunction);
        }

        public Task<TransactionReceipt> DecreaseAllowanceRequestAndWaitForReceiptAsync(DecreaseAllowanceFunction decreaseAllowanceFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(decreaseAllowanceFunction, cancellationToken);
        }

        public Task<string> DecreaseAllowanceRequestAsync(string spender, BigInteger subtractedValue)
        {
            var decreaseAllowanceFunction = new DecreaseAllowanceFunction();
                decreaseAllowanceFunction.Spender = spender;
                decreaseAllowanceFunction.SubtractedValue = subtractedValue;
            
             return ContractHandler.SendRequestAsync(decreaseAllowanceFunction);
        }

        public Task<TransactionReceipt> DecreaseAllowanceRequestAndWaitForReceiptAsync(string spender, BigInteger subtractedValue, CancellationTokenSource cancellationToken = null)
        {
            var decreaseAllowanceFunction = new DecreaseAllowanceFunction();
                decreaseAllowanceFunction.Spender = spender;
                decreaseAllowanceFunction.SubtractedValue = subtractedValue;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(decreaseAllowanceFunction, cancellationToken);
        }

        public Task<Eip712DomainOutputDTO> Eip712DomainQueryAsync(Eip712DomainFunction eip712DomainFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<Eip712DomainFunction, Eip712DomainOutputDTO>(eip712DomainFunction, blockParameter);
        }

        public Task<Eip712DomainOutputDTO> Eip712DomainQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<Eip712DomainFunction, Eip712DomainOutputDTO>(null, blockParameter);
        }

        public Task<string> HubQueryAsync(HubFunction hubFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HubFunction, string>(hubFunction, blockParameter);
        }

        
        public Task<string> HubQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HubFunction, string>(null, blockParameter);
        }

        public Task<string> IncreaseAllowanceRequestAsync(IncreaseAllowanceFunction increaseAllowanceFunction)
        {
             return ContractHandler.SendRequestAsync(increaseAllowanceFunction);
        }

        public Task<TransactionReceipt> IncreaseAllowanceRequestAndWaitForReceiptAsync(IncreaseAllowanceFunction increaseAllowanceFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(increaseAllowanceFunction, cancellationToken);
        }

        public Task<string> IncreaseAllowanceRequestAsync(string spender, BigInteger addedValue)
        {
            var increaseAllowanceFunction = new IncreaseAllowanceFunction();
                increaseAllowanceFunction.Spender = spender;
                increaseAllowanceFunction.AddedValue = addedValue;
            
             return ContractHandler.SendRequestAsync(increaseAllowanceFunction);
        }

        public Task<TransactionReceipt> IncreaseAllowanceRequestAndWaitForReceiptAsync(string spender, BigInteger addedValue, CancellationTokenSource cancellationToken = null)
        {
            var increaseAllowanceFunction = new IncreaseAllowanceFunction();
                increaseAllowanceFunction.Spender = spender;
                increaseAllowanceFunction.AddedValue = addedValue;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(increaseAllowanceFunction, cancellationToken);
        }

        public Task<BigInteger> InflationDayZeroQueryAsync(InflationDayZeroFunction inflationDayZeroFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<InflationDayZeroFunction, BigInteger>(inflationDayZeroFunction, blockParameter);
        }

        
        public Task<BigInteger> InflationDayZeroQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<InflationDayZeroFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> NameQueryAsync(NameFunction nameFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(nameFunction, blockParameter);
        }

        
        public Task<string> NameQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameFunction, string>(null, blockParameter);
        }

        public Task<string> NameRegistryQueryAsync(NameRegistryFunction nameRegistryFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameRegistryFunction, string>(nameRegistryFunction, blockParameter);
        }

        
        public Task<string> NameRegistryQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NameRegistryFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> NoncesQueryAsync(NoncesFunction noncesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<NoncesFunction, BigInteger>(noncesFunction, blockParameter);
        }

        
        public Task<BigInteger> NoncesQueryAsync(string owner, BlockParameter blockParameter = null)
        {
            var noncesFunction = new NoncesFunction();
                noncesFunction.Owner = owner;
            
            return ContractHandler.QueryAsync<NoncesFunction, BigInteger>(noncesFunction, blockParameter);
        }

        public Task<byte[]> OnERC1155BatchReceivedQueryAsync(OnERC1155BatchReceivedFunction onERC1155BatchReceivedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<OnERC1155BatchReceivedFunction, byte[]>(onERC1155BatchReceivedFunction, blockParameter);
        }

        
        public Task<byte[]> OnERC1155BatchReceivedQueryAsync(string returnValue1, string returnValue2, List<BigInteger> returnValue3, List<BigInteger> returnValue4, byte[] returnValue5, BlockParameter blockParameter = null)
        {
            var onERC1155BatchReceivedFunction = new OnERC1155BatchReceivedFunction();
                onERC1155BatchReceivedFunction.ReturnValue1 = returnValue1;
                onERC1155BatchReceivedFunction.ReturnValue2 = returnValue2;
                onERC1155BatchReceivedFunction.ReturnValue3 = returnValue3;
                onERC1155BatchReceivedFunction.ReturnValue4 = returnValue4;
                onERC1155BatchReceivedFunction.ReturnValue5 = returnValue5;
            
            return ContractHandler.QueryAsync<OnERC1155BatchReceivedFunction, byte[]>(onERC1155BatchReceivedFunction, blockParameter);
        }

        public Task<string> OnERC1155ReceivedRequestAsync(OnERC1155ReceivedFunction onERC1155ReceivedFunction)
        {
             return ContractHandler.SendRequestAsync(onERC1155ReceivedFunction);
        }

        public Task<TransactionReceipt> OnERC1155ReceivedRequestAndWaitForReceiptAsync(OnERC1155ReceivedFunction onERC1155ReceivedFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(onERC1155ReceivedFunction, cancellationToken);
        }

        public Task<string> OnERC1155ReceivedRequestAsync(string returnValue1, string from, BigInteger id, BigInteger amount, byte[] returnValue5)
        {
            var onERC1155ReceivedFunction = new OnERC1155ReceivedFunction();
                onERC1155ReceivedFunction.ReturnValue1 = returnValue1;
                onERC1155ReceivedFunction.From = from;
                onERC1155ReceivedFunction.Id = id;
                onERC1155ReceivedFunction.Amount = amount;
                onERC1155ReceivedFunction.ReturnValue5 = returnValue5;
            
             return ContractHandler.SendRequestAsync(onERC1155ReceivedFunction);
        }

        public Task<TransactionReceipt> OnERC1155ReceivedRequestAndWaitForReceiptAsync(string returnValue1, string from, BigInteger id, BigInteger amount, byte[] returnValue5, CancellationTokenSource cancellationToken = null)
        {
            var onERC1155ReceivedFunction = new OnERC1155ReceivedFunction();
                onERC1155ReceivedFunction.ReturnValue1 = returnValue1;
                onERC1155ReceivedFunction.From = from;
                onERC1155ReceivedFunction.Id = id;
                onERC1155ReceivedFunction.Amount = amount;
                onERC1155ReceivedFunction.ReturnValue5 = returnValue5;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(onERC1155ReceivedFunction, cancellationToken);
        }

        public Task<string> PermitRequestAsync(PermitFunction permitFunction)
        {
             return ContractHandler.SendRequestAsync(permitFunction);
        }

        public Task<TransactionReceipt> PermitRequestAndWaitForReceiptAsync(PermitFunction permitFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitFunction, cancellationToken);
        }

        public Task<string> PermitRequestAsync(string owner, string spender, BigInteger value, BigInteger deadline, byte v, byte[] r, byte[] s)
        {
            var permitFunction = new PermitFunction();
                permitFunction.Owner = owner;
                permitFunction.Spender = spender;
                permitFunction.Value = value;
                permitFunction.Deadline = deadline;
                permitFunction.V = v;
                permitFunction.R = r;
                permitFunction.S = s;
            
             return ContractHandler.SendRequestAsync(permitFunction);
        }

        public Task<TransactionReceipt> PermitRequestAndWaitForReceiptAsync(string owner, string spender, BigInteger value, BigInteger deadline, byte v, byte[] r, byte[] s, CancellationTokenSource cancellationToken = null)
        {
            var permitFunction = new PermitFunction();
                permitFunction.Owner = owner;
                permitFunction.Spender = spender;
                permitFunction.Value = value;
                permitFunction.Deadline = deadline;
                permitFunction.V = v;
                permitFunction.R = r;
                permitFunction.S = s;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(permitFunction, cancellationToken);
        }

        public Task<string> SetupRequestAsync(SetupFunction setupFunction)
        {
             return ContractHandler.SendRequestAsync(setupFunction);
        }

        public Task<TransactionReceipt> SetupRequestAndWaitForReceiptAsync(SetupFunction setupFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setupFunction, cancellationToken);
        }

        public Task<string> SetupRequestAsync(string hub, string nameRegistry, string avatar)
        {
            var setupFunction = new SetupFunction();
                setupFunction.Hub = hub;
                setupFunction.NameRegistry = nameRegistry;
                setupFunction.Avatar = avatar;
            
             return ContractHandler.SendRequestAsync(setupFunction);
        }

        public Task<TransactionReceipt> SetupRequestAndWaitForReceiptAsync(string hub, string nameRegistry, string avatar, CancellationTokenSource cancellationToken = null)
        {
            var setupFunction = new SetupFunction();
                setupFunction.Hub = hub;
                setupFunction.NameRegistry = nameRegistry;
                setupFunction.Avatar = avatar;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setupFunction, cancellationToken);
        }

        public Task<bool> SupportsInterfaceQueryAsync(SupportsInterfaceFunction supportsInterfaceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        
        public Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceId, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
                supportsInterfaceFunction.InterfaceId = interfaceId;
            
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }

        public Task<string> SymbolQueryAsync(SymbolFunction symbolFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(symbolFunction, blockParameter);
        }

        
        public Task<string> SymbolQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<SymbolFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> ToTokenIdQueryAsync(ToTokenIdFunction toTokenIdFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ToTokenIdFunction, BigInteger>(toTokenIdFunction, blockParameter);
        }

        
        public Task<BigInteger> ToTokenIdQueryAsync(string avatar, BlockParameter blockParameter = null)
        {
            var toTokenIdFunction = new ToTokenIdFunction();
                toTokenIdFunction.Avatar = avatar;
            
            return ContractHandler.QueryAsync<ToTokenIdFunction, BigInteger>(toTokenIdFunction, blockParameter);
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

        public Task<string> TransferRequestAsync(string to, BigInteger amount)
        {
            var transferFunction = new TransferFunction();
                transferFunction.To = to;
                transferFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(transferFunction);
        }

        public Task<TransactionReceipt> TransferRequestAndWaitForReceiptAsync(string to, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var transferFunction = new TransferFunction();
                transferFunction.To = to;
                transferFunction.Amount = amount;
            
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

        public Task<string> TransferFromRequestAsync(string from, string to, BigInteger amount)
        {
            var transferFromFunction = new TransferFromFunction();
                transferFromFunction.From = from;
                transferFromFunction.To = to;
                transferFromFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(transferFromFunction);
        }

        public Task<TransactionReceipt> TransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var transferFromFunction = new TransferFromFunction();
                transferFromFunction.From = from;
                transferFromFunction.To = to;
                transferFromFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(transferFromFunction, cancellationToken);
        }

        public Task<string> UnwrapRequestAsync(UnwrapFunction unwrapFunction)
        {
             return ContractHandler.SendRequestAsync(unwrapFunction);
        }

        public Task<TransactionReceipt> UnwrapRequestAndWaitForReceiptAsync(UnwrapFunction unwrapFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unwrapFunction, cancellationToken);
        }

        public Task<string> UnwrapRequestAsync(BigInteger amount)
        {
            var unwrapFunction = new UnwrapFunction();
                unwrapFunction.Amount = amount;
            
             return ContractHandler.SendRequestAsync(unwrapFunction);
        }

        public Task<TransactionReceipt> UnwrapRequestAndWaitForReceiptAsync(BigInteger amount, CancellationTokenSource cancellationToken = null)
        {
            var unwrapFunction = new UnwrapFunction();
                unwrapFunction.Amount = amount;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unwrapFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(DomainSeparatorFunction),
                typeof(AllowanceFunction),
                typeof(ApproveFunction),
                typeof(AvatarFunction),
                typeof(BalanceOfFunction),
                typeof(CirclesIdentifierFunction),
                typeof(ConvertBatchDemurrageToInflationaryValuesFunction),
                typeof(ConvertBatchInflationaryToDemurrageValuesFunction),
                typeof(ConvertDemurrageToInflationaryValueFunction),
                typeof(ConvertInflationaryToDemurrageValueFunction),
                typeof(DayFunction),
                typeof(DecimalsFunction),
                typeof(DecreaseAllowanceFunction),
                typeof(Eip712DomainFunction),
                typeof(HubFunction),
                typeof(IncreaseAllowanceFunction),
                typeof(InflationDayZeroFunction),
                typeof(NameFunction),
                typeof(NameRegistryFunction),
                typeof(NoncesFunction),
                typeof(OnERC1155BatchReceivedFunction),
                typeof(OnERC1155ReceivedFunction),
                typeof(PermitFunction),
                typeof(SetupFunction),
                typeof(SupportsInterfaceFunction),
                typeof(SymbolFunction),
                typeof(ToTokenIdFunction),
                typeof(TotalSupplyFunction),
                typeof(TransferFunction),
                typeof(TransferFromFunction),
                typeof(UnwrapFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(ApprovalEventDTO),
                typeof(DepositInflationaryEventDTO),
                typeof(EIP712DomainChangedEventDTO),
                typeof(TransferEventDTO),
                typeof(WithdrawInflationaryEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(CirclesAmountOverflowError),
                typeof(CirclesERC1155CannotReceiveBatchError),
                typeof(CirclesErrorAddressUintArgsError),
                typeof(CirclesErrorNoArgsError),
                typeof(CirclesErrorOneAddressArgError),
                typeof(CirclesIdMustBeDerivedFromAddressError),
                typeof(CirclesInvalidCirclesIdError),
                typeof(CirclesInvalidParameterError),
                typeof(CirclesProxyAlreadyInitializedError),
                typeof(CirclesReentrancyGuardError),
                typeof(ECDSAInvalidSignatureError),
                typeof(ECDSAInvalidSignatureLengthError),
                typeof(ECDSAInvalidSignatureSError),
                typeof(ERC20InsufficientAllowanceError),
                typeof(ERC20InsufficientBalanceError),
                typeof(ERC20InvalidApproverError),
                typeof(ERC20InvalidReceiverError),
                typeof(ERC20InvalidSenderError),
                typeof(ERC20InvalidSpenderError),
                typeof(ERC2612ExpiredSignatureError),
                typeof(ERC2612InvalidSignerError),
                typeof(InvalidAccountNonceError),
                typeof(InvalidShortStringError),
                typeof(StringTooLongError)
            };
        }
    }
}
