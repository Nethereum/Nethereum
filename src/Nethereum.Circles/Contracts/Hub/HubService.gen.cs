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
using Nethereum.Circles.Contracts.Hub.ContractDefinition;

namespace Nethereum.Circles.Contracts.Hub
{
    public partial class HubService: ContractWeb3ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, HubDeployment hubDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<HubDeployment>().SendRequestAndWaitForReceiptAsync(hubDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, HubDeployment hubDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<HubDeployment>().SendRequestAsync(hubDeployment);
        }

        public static async Task<HubService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, HubDeployment hubDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, hubDeployment, cancellationTokenSource);
            return new HubService(web3, receipt.ContractAddress);
        }

        public HubService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<byte[]> AdvancedUsageFlagsQueryAsync(AdvancedUsageFlagsFunction advancedUsageFlagsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AdvancedUsageFlagsFunction, byte[]>(advancedUsageFlagsFunction, blockParameter);
        }

        
        public Task<byte[]> AdvancedUsageFlagsQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var advancedUsageFlagsFunction = new AdvancedUsageFlagsFunction();
                advancedUsageFlagsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<AdvancedUsageFlagsFunction, byte[]>(advancedUsageFlagsFunction, blockParameter);
        }

        public Task<string> AvatarsQueryAsync(AvatarsFunction avatarsFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<AvatarsFunction, string>(avatarsFunction, blockParameter);
        }

        
        public Task<string> AvatarsQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var avatarsFunction = new AvatarsFunction();
                avatarsFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<AvatarsFunction, string>(avatarsFunction, blockParameter);
        }

        public Task<BigInteger> BalanceOfQueryAsync(BalanceOfFunction balanceOfFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        
        public Task<BigInteger> BalanceOfQueryAsync(string account, BigInteger id, BlockParameter blockParameter = null)
        {
            var balanceOfFunction = new BalanceOfFunction();
                balanceOfFunction.Account = account;
                balanceOfFunction.Id = id;
            
            return ContractHandler.QueryAsync<BalanceOfFunction, BigInteger>(balanceOfFunction, blockParameter);
        }

        public Task<List<BigInteger>> BalanceOfBatchQueryAsync(BalanceOfBatchFunction balanceOfBatchFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<BalanceOfBatchFunction, List<BigInteger>>(balanceOfBatchFunction, blockParameter);
        }

        
        public Task<List<BigInteger>> BalanceOfBatchQueryAsync(List<string> accounts, List<BigInteger> ids, BlockParameter blockParameter = null)
        {
            var balanceOfBatchFunction = new BalanceOfBatchFunction();
                balanceOfBatchFunction.Accounts = accounts;
                balanceOfBatchFunction.Ids = ids;
            
            return ContractHandler.QueryAsync<BalanceOfBatchFunction, List<BigInteger>>(balanceOfBatchFunction, blockParameter);
        }

        public Task<BalanceOfOnDayOutputDTO> BalanceOfOnDayQueryAsync(BalanceOfOnDayFunction balanceOfOnDayFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<BalanceOfOnDayFunction, BalanceOfOnDayOutputDTO>(balanceOfOnDayFunction, blockParameter);
        }

        public Task<BalanceOfOnDayOutputDTO> BalanceOfOnDayQueryAsync(string account, BigInteger id, ulong day, BlockParameter blockParameter = null)
        {
            var balanceOfOnDayFunction = new BalanceOfOnDayFunction();
                balanceOfOnDayFunction.Account = account;
                balanceOfOnDayFunction.Id = id;
                balanceOfOnDayFunction.Day = day;
            
            return ContractHandler.QueryDeserializingToObjectAsync<BalanceOfOnDayFunction, BalanceOfOnDayOutputDTO>(balanceOfOnDayFunction, blockParameter);
        }

        public Task<string> BurnRequestAsync(BurnFunction burnFunction)
        {
             return ContractHandler.SendRequestAsync(burnFunction);
        }

        public Task<TransactionReceipt> BurnRequestAndWaitForReceiptAsync(BurnFunction burnFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(burnFunction, cancellationToken);
        }

        public Task<string> BurnRequestAsync(BigInteger id, BigInteger amount, byte[] data)
        {
            var burnFunction = new BurnFunction();
                burnFunction.Id = id;
                burnFunction.Amount = amount;
                burnFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(burnFunction);
        }

        public Task<TransactionReceipt> BurnRequestAndWaitForReceiptAsync(BigInteger id, BigInteger amount, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var burnFunction = new BurnFunction();
                burnFunction.Id = id;
                burnFunction.Amount = amount;
                burnFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(burnFunction, cancellationToken);
        }

        public Task<CalculateIssuanceOutputDTO> CalculateIssuanceQueryAsync(CalculateIssuanceFunction calculateIssuanceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<CalculateIssuanceFunction, CalculateIssuanceOutputDTO>(calculateIssuanceFunction, blockParameter);
        }

        public Task<CalculateIssuanceOutputDTO> CalculateIssuanceQueryAsync(string human, BlockParameter blockParameter = null)
        {
            var calculateIssuanceFunction = new CalculateIssuanceFunction();
                calculateIssuanceFunction.Human = human;
            
            return ContractHandler.QueryDeserializingToObjectAsync<CalculateIssuanceFunction, CalculateIssuanceOutputDTO>(calculateIssuanceFunction, blockParameter);
        }

        public Task<string> CalculateIssuanceWithCheckRequestAsync(CalculateIssuanceWithCheckFunction calculateIssuanceWithCheckFunction)
        {
             return ContractHandler.SendRequestAsync(calculateIssuanceWithCheckFunction);
        }

        public Task<TransactionReceipt> CalculateIssuanceWithCheckRequestAndWaitForReceiptAsync(CalculateIssuanceWithCheckFunction calculateIssuanceWithCheckFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(calculateIssuanceWithCheckFunction, cancellationToken);
        }

        public Task<string> CalculateIssuanceWithCheckRequestAsync(string human)
        {
            var calculateIssuanceWithCheckFunction = new CalculateIssuanceWithCheckFunction();
                calculateIssuanceWithCheckFunction.Human = human;
            
             return ContractHandler.SendRequestAsync(calculateIssuanceWithCheckFunction);
        }

        public Task<TransactionReceipt> CalculateIssuanceWithCheckRequestAndWaitForReceiptAsync(string human, CancellationTokenSource cancellationToken = null)
        {
            var calculateIssuanceWithCheckFunction = new CalculateIssuanceWithCheckFunction();
                calculateIssuanceWithCheckFunction.Human = human;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(calculateIssuanceWithCheckFunction, cancellationToken);
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

        public Task<string> GroupMintRequestAsync(GroupMintFunction groupMintFunction)
        {
             return ContractHandler.SendRequestAsync(groupMintFunction);
        }

        public Task<TransactionReceipt> GroupMintRequestAndWaitForReceiptAsync(GroupMintFunction groupMintFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(groupMintFunction, cancellationToken);
        }

        public Task<string> GroupMintRequestAsync(string group, List<string> collateralAvatars, List<BigInteger> amounts, byte[] data)
        {
            var groupMintFunction = new GroupMintFunction();
                groupMintFunction.Group = group;
                groupMintFunction.CollateralAvatars = collateralAvatars;
                groupMintFunction.Amounts = amounts;
                groupMintFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(groupMintFunction);
        }

        public Task<TransactionReceipt> GroupMintRequestAndWaitForReceiptAsync(string group, List<string> collateralAvatars, List<BigInteger> amounts, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var groupMintFunction = new GroupMintFunction();
                groupMintFunction.Group = group;
                groupMintFunction.CollateralAvatars = collateralAvatars;
                groupMintFunction.Amounts = amounts;
                groupMintFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(groupMintFunction, cancellationToken);
        }

        public Task<BigInteger> InflationDayZeroQueryAsync(InflationDayZeroFunction inflationDayZeroFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<InflationDayZeroFunction, BigInteger>(inflationDayZeroFunction, blockParameter);
        }

        
        public Task<BigInteger> InflationDayZeroQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<InflationDayZeroFunction, BigInteger>(null, blockParameter);
        }

        public Task<BigInteger> InvitationOnlyTimeQueryAsync(InvitationOnlyTimeFunction invitationOnlyTimeFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<InvitationOnlyTimeFunction, BigInteger>(invitationOnlyTimeFunction, blockParameter);
        }

        
        public Task<BigInteger> InvitationOnlyTimeQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<InvitationOnlyTimeFunction, BigInteger>(null, blockParameter);
        }

        public Task<bool> IsApprovedForAllQueryAsync(IsApprovedForAllFunction isApprovedForAllFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsApprovedForAllFunction, bool>(isApprovedForAllFunction, blockParameter);
        }

        
        public Task<bool> IsApprovedForAllQueryAsync(string account, string @operator, BlockParameter blockParameter = null)
        {
            var isApprovedForAllFunction = new IsApprovedForAllFunction();
                isApprovedForAllFunction.Account = account;
                isApprovedForAllFunction.Operator = @operator;
            
            return ContractHandler.QueryAsync<IsApprovedForAllFunction, bool>(isApprovedForAllFunction, blockParameter);
        }

        public Task<bool> IsGroupQueryAsync(IsGroupFunction isGroupFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsGroupFunction, bool>(isGroupFunction, blockParameter);
        }

        
        public Task<bool> IsGroupQueryAsync(string group, BlockParameter blockParameter = null)
        {
            var isGroupFunction = new IsGroupFunction();
                isGroupFunction.Group = group;
            
            return ContractHandler.QueryAsync<IsGroupFunction, bool>(isGroupFunction, blockParameter);
        }

        public Task<bool> IsHumanQueryAsync(IsHumanFunction isHumanFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsHumanFunction, bool>(isHumanFunction, blockParameter);
        }

        
        public Task<bool> IsHumanQueryAsync(string human, BlockParameter blockParameter = null)
        {
            var isHumanFunction = new IsHumanFunction();
                isHumanFunction.Human = human;
            
            return ContractHandler.QueryAsync<IsHumanFunction, bool>(isHumanFunction, blockParameter);
        }

        public Task<bool> IsOrganizationQueryAsync(IsOrganizationFunction isOrganizationFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsOrganizationFunction, bool>(isOrganizationFunction, blockParameter);
        }

        
        public Task<bool> IsOrganizationQueryAsync(string organization, BlockParameter blockParameter = null)
        {
            var isOrganizationFunction = new IsOrganizationFunction();
                isOrganizationFunction.Organization = organization;
            
            return ContractHandler.QueryAsync<IsOrganizationFunction, bool>(isOrganizationFunction, blockParameter);
        }

        public Task<bool> IsPermittedFlowQueryAsync(IsPermittedFlowFunction isPermittedFlowFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsPermittedFlowFunction, bool>(isPermittedFlowFunction, blockParameter);
        }

        
        public Task<bool> IsPermittedFlowQueryAsync(string from, string to, string circlesAvatar, BlockParameter blockParameter = null)
        {
            var isPermittedFlowFunction = new IsPermittedFlowFunction();
                isPermittedFlowFunction.From = from;
                isPermittedFlowFunction.To = to;
                isPermittedFlowFunction.CirclesAvatar = circlesAvatar;
            
            return ContractHandler.QueryAsync<IsPermittedFlowFunction, bool>(isPermittedFlowFunction, blockParameter);
        }

        public Task<bool> IsTrustedQueryAsync(IsTrustedFunction isTrustedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<IsTrustedFunction, bool>(isTrustedFunction, blockParameter);
        }

        
        public Task<bool> IsTrustedQueryAsync(string truster, string trustee, BlockParameter blockParameter = null)
        {
            var isTrustedFunction = new IsTrustedFunction();
                isTrustedFunction.Truster = truster;
                isTrustedFunction.Trustee = trustee;
            
            return ContractHandler.QueryAsync<IsTrustedFunction, bool>(isTrustedFunction, blockParameter);
        }

        public Task<string> MigrateRequestAsync(MigrateFunction migrateFunction)
        {
             return ContractHandler.SendRequestAsync(migrateFunction);
        }

        public Task<TransactionReceipt> MigrateRequestAndWaitForReceiptAsync(MigrateFunction migrateFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(migrateFunction, cancellationToken);
        }

        public Task<string> MigrateRequestAsync(string owner, List<string> avatars, List<BigInteger> amounts)
        {
            var migrateFunction = new MigrateFunction();
                migrateFunction.Owner = owner;
                migrateFunction.Avatars = avatars;
                migrateFunction.Amounts = amounts;
            
             return ContractHandler.SendRequestAsync(migrateFunction);
        }

        public Task<TransactionReceipt> MigrateRequestAndWaitForReceiptAsync(string owner, List<string> avatars, List<BigInteger> amounts, CancellationTokenSource cancellationToken = null)
        {
            var migrateFunction = new MigrateFunction();
                migrateFunction.Owner = owner;
                migrateFunction.Avatars = avatars;
                migrateFunction.Amounts = amounts;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(migrateFunction, cancellationToken);
        }

        public Task<string> MintPoliciesQueryAsync(MintPoliciesFunction mintPoliciesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MintPoliciesFunction, string>(mintPoliciesFunction, blockParameter);
        }

        
        public Task<string> MintPoliciesQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var mintPoliciesFunction = new MintPoliciesFunction();
                mintPoliciesFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<MintPoliciesFunction, string>(mintPoliciesFunction, blockParameter);
        }

        public Task<string> OperateFlowMatrixRequestAsync(OperateFlowMatrixFunction operateFlowMatrixFunction)
        {
             return ContractHandler.SendRequestAsync(operateFlowMatrixFunction);
        }

        public Task<TransactionReceipt> OperateFlowMatrixRequestAndWaitForReceiptAsync(OperateFlowMatrixFunction operateFlowMatrixFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(operateFlowMatrixFunction, cancellationToken);
        }

        public Task<string> OperateFlowMatrixRequestAsync(List<string> flowVertices, List<FlowEdge> flow, List<ContractDefinition.Stream> streams, byte[] packedCoordinates)
        {
            var operateFlowMatrixFunction = new OperateFlowMatrixFunction();
                operateFlowMatrixFunction.FlowVertices = flowVertices;
                operateFlowMatrixFunction.Flow = flow;
                operateFlowMatrixFunction.Streams = streams;
                operateFlowMatrixFunction.PackedCoordinates = packedCoordinates;
            
             return ContractHandler.SendRequestAsync(operateFlowMatrixFunction);
        }

        public Task<TransactionReceipt> OperateFlowMatrixRequestAndWaitForReceiptAsync(List<string> flowVertices, List<FlowEdge> flow, List<ContractDefinition.Stream> streams, byte[] packedCoordinates, CancellationTokenSource cancellationToken = null)
        {
            var operateFlowMatrixFunction = new OperateFlowMatrixFunction();
                operateFlowMatrixFunction.FlowVertices = flowVertices;
                operateFlowMatrixFunction.Flow = flow;
                operateFlowMatrixFunction.Streams = streams;
                operateFlowMatrixFunction.PackedCoordinates = packedCoordinates;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(operateFlowMatrixFunction, cancellationToken);
        }

        public Task<string> PersonalMintRequestAsync(PersonalMintFunction personalMintFunction)
        {
             return ContractHandler.SendRequestAsync(personalMintFunction);
        }

        public Task<string> PersonalMintRequestAsync()
        {
             return ContractHandler.SendRequestAsync<PersonalMintFunction>();
        }

        public Task<TransactionReceipt> PersonalMintRequestAndWaitForReceiptAsync(PersonalMintFunction personalMintFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(personalMintFunction, cancellationToken);
        }

        public Task<TransactionReceipt> PersonalMintRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<PersonalMintFunction>(null, cancellationToken);
        }

        public Task<string> RegisterCustomGroupRequestAsync(RegisterCustomGroupFunction registerCustomGroupFunction)
        {
             return ContractHandler.SendRequestAsync(registerCustomGroupFunction);
        }

        public Task<TransactionReceipt> RegisterCustomGroupRequestAndWaitForReceiptAsync(RegisterCustomGroupFunction registerCustomGroupFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerCustomGroupFunction, cancellationToken);
        }

        public Task<string> RegisterCustomGroupRequestAsync(string mint, string treasury, string name, string symbol, byte[] metadataDigest)
        {
            var registerCustomGroupFunction = new RegisterCustomGroupFunction();
                registerCustomGroupFunction.Mint = mint;
                registerCustomGroupFunction.Treasury = treasury;
                registerCustomGroupFunction.Name = name;
                registerCustomGroupFunction.Symbol = symbol;
                registerCustomGroupFunction.MetadataDigest = metadataDigest;
            
             return ContractHandler.SendRequestAsync(registerCustomGroupFunction);
        }

        public Task<TransactionReceipt> RegisterCustomGroupRequestAndWaitForReceiptAsync(string mint, string treasury, string name, string symbol, byte[] metadataDigest, CancellationTokenSource cancellationToken = null)
        {
            var registerCustomGroupFunction = new RegisterCustomGroupFunction();
                registerCustomGroupFunction.Mint = mint;
                registerCustomGroupFunction.Treasury = treasury;
                registerCustomGroupFunction.Name = name;
                registerCustomGroupFunction.Symbol = symbol;
                registerCustomGroupFunction.MetadataDigest = metadataDigest;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerCustomGroupFunction, cancellationToken);
        }

        public Task<string> RegisterGroupRequestAsync(RegisterGroupFunction registerGroupFunction)
        {
             return ContractHandler.SendRequestAsync(registerGroupFunction);
        }

        public Task<TransactionReceipt> RegisterGroupRequestAndWaitForReceiptAsync(RegisterGroupFunction registerGroupFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerGroupFunction, cancellationToken);
        }

        public Task<string> RegisterGroupRequestAsync(string mint, string name, string symbol, byte[] metadataDigest)
        {
            var registerGroupFunction = new RegisterGroupFunction();
                registerGroupFunction.Mint = mint;
                registerGroupFunction.Name = name;
                registerGroupFunction.Symbol = symbol;
                registerGroupFunction.MetadataDigest = metadataDigest;
            
             return ContractHandler.SendRequestAsync(registerGroupFunction);
        }

        public Task<TransactionReceipt> RegisterGroupRequestAndWaitForReceiptAsync(string mint, string name, string symbol, byte[] metadataDigest, CancellationTokenSource cancellationToken = null)
        {
            var registerGroupFunction = new RegisterGroupFunction();
                registerGroupFunction.Mint = mint;
                registerGroupFunction.Name = name;
                registerGroupFunction.Symbol = symbol;
                registerGroupFunction.MetadataDigest = metadataDigest;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerGroupFunction, cancellationToken);
        }

        public Task<string> RegisterHumanRequestAsync(RegisterHumanFunction registerHumanFunction)
        {
             return ContractHandler.SendRequestAsync(registerHumanFunction);
        }

        public Task<TransactionReceipt> RegisterHumanRequestAndWaitForReceiptAsync(RegisterHumanFunction registerHumanFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerHumanFunction, cancellationToken);
        }

        public Task<string> RegisterHumanRequestAsync(string inviter, byte[] metadataDigest)
        {
            var registerHumanFunction = new RegisterHumanFunction();
                registerHumanFunction.Inviter = inviter;
                registerHumanFunction.MetadataDigest = metadataDigest;
            
             return ContractHandler.SendRequestAsync(registerHumanFunction);
        }

        public Task<TransactionReceipt> RegisterHumanRequestAndWaitForReceiptAsync(string inviter, byte[] metadataDigest, CancellationTokenSource cancellationToken = null)
        {
            var registerHumanFunction = new RegisterHumanFunction();
                registerHumanFunction.Inviter = inviter;
                registerHumanFunction.MetadataDigest = metadataDigest;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerHumanFunction, cancellationToken);
        }

        public Task<string> RegisterOrganizationRequestAsync(RegisterOrganizationFunction registerOrganizationFunction)
        {
             return ContractHandler.SendRequestAsync(registerOrganizationFunction);
        }

        public Task<TransactionReceipt> RegisterOrganizationRequestAndWaitForReceiptAsync(RegisterOrganizationFunction registerOrganizationFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerOrganizationFunction, cancellationToken);
        }

        public Task<string> RegisterOrganizationRequestAsync(string name, byte[] metadataDigest)
        {
            var registerOrganizationFunction = new RegisterOrganizationFunction();
                registerOrganizationFunction.Name = name;
                registerOrganizationFunction.MetadataDigest = metadataDigest;
            
             return ContractHandler.SendRequestAsync(registerOrganizationFunction);
        }

        public Task<TransactionReceipt> RegisterOrganizationRequestAndWaitForReceiptAsync(string name, byte[] metadataDigest, CancellationTokenSource cancellationToken = null)
        {
            var registerOrganizationFunction = new RegisterOrganizationFunction();
                registerOrganizationFunction.Name = name;
                registerOrganizationFunction.MetadataDigest = metadataDigest;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerOrganizationFunction, cancellationToken);
        }

        public Task<string> SafeBatchTransferFromRequestAsync(SafeBatchTransferFromFunction safeBatchTransferFromFunction)
        {
             return ContractHandler.SendRequestAsync(safeBatchTransferFromFunction);
        }

        public Task<TransactionReceipt> SafeBatchTransferFromRequestAndWaitForReceiptAsync(SafeBatchTransferFromFunction safeBatchTransferFromFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(safeBatchTransferFromFunction, cancellationToken);
        }

        public Task<string> SafeBatchTransferFromRequestAsync(string from, string to, List<BigInteger> ids, List<BigInteger> values, byte[] data)
        {
            var safeBatchTransferFromFunction = new SafeBatchTransferFromFunction();
                safeBatchTransferFromFunction.From = from;
                safeBatchTransferFromFunction.To = to;
                safeBatchTransferFromFunction.Ids = ids;
                safeBatchTransferFromFunction.Values = values;
                safeBatchTransferFromFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(safeBatchTransferFromFunction);
        }

        public Task<TransactionReceipt> SafeBatchTransferFromRequestAndWaitForReceiptAsync(string from, string to, List<BigInteger> ids, List<BigInteger> values, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var safeBatchTransferFromFunction = new SafeBatchTransferFromFunction();
                safeBatchTransferFromFunction.From = from;
                safeBatchTransferFromFunction.To = to;
                safeBatchTransferFromFunction.Ids = ids;
                safeBatchTransferFromFunction.Values = values;
                safeBatchTransferFromFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(safeBatchTransferFromFunction, cancellationToken);
        }

        public Task<string> SafeTransferFromRequestAsync(SafeTransferFromFunction safeTransferFromFunction)
        {
             return ContractHandler.SendRequestAsync(safeTransferFromFunction);
        }

        public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(SafeTransferFromFunction safeTransferFromFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(safeTransferFromFunction, cancellationToken);
        }

        public Task<string> SafeTransferFromRequestAsync(string from, string to, BigInteger id, BigInteger value, byte[] data)
        {
            var safeTransferFromFunction = new SafeTransferFromFunction();
                safeTransferFromFunction.From = from;
                safeTransferFromFunction.To = to;
                safeTransferFromFunction.Id = id;
                safeTransferFromFunction.Value = value;
                safeTransferFromFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(safeTransferFromFunction);
        }

        public Task<TransactionReceipt> SafeTransferFromRequestAndWaitForReceiptAsync(string from, string to, BigInteger id, BigInteger value, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var safeTransferFromFunction = new SafeTransferFromFunction();
                safeTransferFromFunction.From = from;
                safeTransferFromFunction.To = to;
                safeTransferFromFunction.Id = id;
                safeTransferFromFunction.Value = value;
                safeTransferFromFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(safeTransferFromFunction, cancellationToken);
        }

        public Task<string> SetAdvancedUsageFlagRequestAsync(SetAdvancedUsageFlagFunction setAdvancedUsageFlagFunction)
        {
             return ContractHandler.SendRequestAsync(setAdvancedUsageFlagFunction);
        }

        public Task<TransactionReceipt> SetAdvancedUsageFlagRequestAndWaitForReceiptAsync(SetAdvancedUsageFlagFunction setAdvancedUsageFlagFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setAdvancedUsageFlagFunction, cancellationToken);
        }

        public Task<string> SetAdvancedUsageFlagRequestAsync(byte[] flag)
        {
            var setAdvancedUsageFlagFunction = new SetAdvancedUsageFlagFunction();
                setAdvancedUsageFlagFunction.Flag = flag;
            
             return ContractHandler.SendRequestAsync(setAdvancedUsageFlagFunction);
        }

        public Task<TransactionReceipt> SetAdvancedUsageFlagRequestAndWaitForReceiptAsync(byte[] flag, CancellationTokenSource cancellationToken = null)
        {
            var setAdvancedUsageFlagFunction = new SetAdvancedUsageFlagFunction();
                setAdvancedUsageFlagFunction.Flag = flag;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setAdvancedUsageFlagFunction, cancellationToken);
        }

        public Task<string> SetApprovalForAllRequestAsync(SetApprovalForAllFunction setApprovalForAllFunction)
        {
             return ContractHandler.SendRequestAsync(setApprovalForAllFunction);
        }

        public Task<TransactionReceipt> SetApprovalForAllRequestAndWaitForReceiptAsync(SetApprovalForAllFunction setApprovalForAllFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setApprovalForAllFunction, cancellationToken);
        }

        public Task<string> SetApprovalForAllRequestAsync(string @operator, bool approved)
        {
            var setApprovalForAllFunction = new SetApprovalForAllFunction();
                setApprovalForAllFunction.Operator = @operator;
                setApprovalForAllFunction.Approved = approved;
            
             return ContractHandler.SendRequestAsync(setApprovalForAllFunction);
        }

        public Task<TransactionReceipt> SetApprovalForAllRequestAndWaitForReceiptAsync(string @operator, bool approved, CancellationTokenSource cancellationToken = null)
        {
            var setApprovalForAllFunction = new SetApprovalForAllFunction();
                setApprovalForAllFunction.Operator = @operator;
                setApprovalForAllFunction.Approved = approved;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setApprovalForAllFunction, cancellationToken);
        }

        public Task<string> StopRequestAsync(StopFunction stopFunction)
        {
             return ContractHandler.SendRequestAsync(stopFunction);
        }

        public Task<string> StopRequestAsync()
        {
             return ContractHandler.SendRequestAsync<StopFunction>();
        }

        public Task<TransactionReceipt> StopRequestAndWaitForReceiptAsync(StopFunction stopFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(stopFunction, cancellationToken);
        }

        public Task<TransactionReceipt> StopRequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync<StopFunction>(null, cancellationToken);
        }

        public Task<bool> StoppedQueryAsync(StoppedFunction stoppedFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<StoppedFunction, bool>(stoppedFunction, blockParameter);
        }

        
        public Task<bool> StoppedQueryAsync(string human, BlockParameter blockParameter = null)
        {
            var stoppedFunction = new StoppedFunction();
                stoppedFunction.Human = human;
            
            return ContractHandler.QueryAsync<StoppedFunction, bool>(stoppedFunction, blockParameter);
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

        
        public Task<BigInteger> TotalSupplyQueryAsync(BigInteger id, BlockParameter blockParameter = null)
        {
            var totalSupplyFunction = new TotalSupplyFunction();
                totalSupplyFunction.Id = id;
            
            return ContractHandler.QueryAsync<TotalSupplyFunction, BigInteger>(totalSupplyFunction, blockParameter);
        }

        public Task<string> TreasuriesQueryAsync(TreasuriesFunction treasuriesFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<TreasuriesFunction, string>(treasuriesFunction, blockParameter);
        }

        
        public Task<string> TreasuriesQueryAsync(string returnValue1, BlockParameter blockParameter = null)
        {
            var treasuriesFunction = new TreasuriesFunction();
                treasuriesFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<TreasuriesFunction, string>(treasuriesFunction, blockParameter);
        }

        public Task<string> TrustRequestAsync(TrustFunction trustFunction)
        {
             return ContractHandler.SendRequestAsync(trustFunction);
        }

        public Task<TransactionReceipt> TrustRequestAndWaitForReceiptAsync(TrustFunction trustFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(trustFunction, cancellationToken);
        }

        public Task<string> TrustRequestAsync(string trustReceiver, BigInteger expiry)
        {
            var trustFunction = new TrustFunction();
                trustFunction.TrustReceiver = trustReceiver;
                trustFunction.Expiry = expiry;
            
             return ContractHandler.SendRequestAsync(trustFunction);
        }

        public Task<TransactionReceipt> TrustRequestAndWaitForReceiptAsync(string trustReceiver, BigInteger expiry, CancellationTokenSource cancellationToken = null)
        {
            var trustFunction = new TrustFunction();
                trustFunction.TrustReceiver = trustReceiver;
                trustFunction.Expiry = expiry;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(trustFunction, cancellationToken);
        }

        public Task<TrustMarkersOutputDTO> TrustMarkersQueryAsync(TrustMarkersFunction trustMarkersFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<TrustMarkersFunction, TrustMarkersOutputDTO>(trustMarkersFunction, blockParameter);
        }

        public Task<TrustMarkersOutputDTO> TrustMarkersQueryAsync(string returnValue1, string returnValue2, BlockParameter blockParameter = null)
        {
            var trustMarkersFunction = new TrustMarkersFunction();
                trustMarkersFunction.ReturnValue1 = returnValue1;
                trustMarkersFunction.ReturnValue2 = returnValue2;
            
            return ContractHandler.QueryDeserializingToObjectAsync<TrustMarkersFunction, TrustMarkersOutputDTO>(trustMarkersFunction, blockParameter);
        }

        public Task<string> UriQueryAsync(UriFunction uriFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<UriFunction, string>(uriFunction, blockParameter);
        }

        
        public Task<string> UriQueryAsync(BigInteger returnValue1, BlockParameter blockParameter = null)
        {
            var uriFunction = new UriFunction();
                uriFunction.ReturnValue1 = returnValue1;
            
            return ContractHandler.QueryAsync<UriFunction, string>(uriFunction, blockParameter);
        }

        public Task<string> WrapRequestAsync(WrapFunction wrapFunction)
        {
             return ContractHandler.SendRequestAsync(wrapFunction);
        }

        public Task<TransactionReceipt> WrapRequestAndWaitForReceiptAsync(WrapFunction wrapFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(wrapFunction, cancellationToken);
        }

        public Task<string> WrapRequestAsync(string avatar, BigInteger amount, byte type)
        {
            var wrapFunction = new WrapFunction();
                wrapFunction.Avatar = avatar;
                wrapFunction.Amount = amount;
                wrapFunction.Type = type;
            
             return ContractHandler.SendRequestAsync(wrapFunction);
        }

        public Task<TransactionReceipt> WrapRequestAndWaitForReceiptAsync(string avatar, BigInteger amount, byte type, CancellationTokenSource cancellationToken = null)
        {
            var wrapFunction = new WrapFunction();
                wrapFunction.Avatar = avatar;
                wrapFunction.Amount = amount;
                wrapFunction.Type = type;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(wrapFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(AdvancedUsageFlagsFunction),
                typeof(AvatarsFunction),
                typeof(BalanceOfFunction),
                typeof(BalanceOfBatchFunction),
                typeof(BalanceOfOnDayFunction),
                typeof(BurnFunction),
                typeof(CalculateIssuanceFunction),
                typeof(CalculateIssuanceWithCheckFunction),
                typeof(ConvertDemurrageToInflationaryValueFunction),
                typeof(ConvertInflationaryToDemurrageValueFunction),
                typeof(DayFunction),
                typeof(GroupMintFunction),
                typeof(InflationDayZeroFunction),
                typeof(InvitationOnlyTimeFunction),
                typeof(IsApprovedForAllFunction),
                typeof(IsGroupFunction),
                typeof(IsHumanFunction),
                typeof(IsOrganizationFunction),
                typeof(IsPermittedFlowFunction),
                typeof(IsTrustedFunction),
                typeof(MigrateFunction),
                typeof(MintPoliciesFunction),
                typeof(OperateFlowMatrixFunction),
                typeof(PersonalMintFunction),
                typeof(RegisterCustomGroupFunction),
                typeof(RegisterGroupFunction),
                typeof(RegisterHumanFunction),
                typeof(RegisterOrganizationFunction),
                typeof(SafeBatchTransferFromFunction),
                typeof(SafeTransferFromFunction),
                typeof(SetAdvancedUsageFlagFunction),
                typeof(SetApprovalForAllFunction),
                typeof(StopFunction),
                typeof(StoppedFunction),
                typeof(SupportsInterfaceFunction),
                typeof(ToTokenIdFunction),
                typeof(TotalSupplyFunction),
                typeof(TreasuriesFunction),
                typeof(TrustFunction),
                typeof(TrustMarkersFunction),
                typeof(UriFunction),
                typeof(WrapFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(ApprovalForAllEventDTO),
                typeof(DiscountCostEventDTO),
                typeof(FlowEdgesScopeLastEndedEventDTO),
                typeof(FlowEdgesScopeSingleStartedEventDTO),
                typeof(GroupMintEventDTO),
                typeof(PersonalMintEventDTO),
                typeof(RegisterGroupEventDTO),
                typeof(RegisterHumanEventDTO),
                typeof(RegisterOrganizationEventDTO),
                typeof(SetAdvancedUsageFlagEventDTO),
                typeof(StoppedEventDTO),
                typeof(StreamCompletedEventDTO),
                typeof(TransferBatchEventDTO),
                typeof(TransferSingleEventDTO),
                typeof(TrustEventDTO),
                typeof(UriEventDTO)
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
                typeof(CirclesHubFlowEdgeStreamMismatchError),
                typeof(CirclesHubNettedFlowMismatchError),
                typeof(CirclesHubStreamMismatchError),
                typeof(CirclesIdMustBeDerivedFromAddressError),
                typeof(CirclesInvalidCirclesIdError),
                typeof(CirclesInvalidParameterError),
                typeof(CirclesProxyAlreadyInitializedError),
                typeof(CirclesReentrancyGuardError),
                typeof(ERC1155InsufficientBalanceError),
                typeof(ERC1155InvalidApproverError),
                typeof(ERC1155InvalidArrayLengthError),
                typeof(ERC1155InvalidOperatorError),
                typeof(ERC1155InvalidReceiverError),
                typeof(ERC1155InvalidSenderError),
                typeof(ERC1155MissingApprovalForAllError)
            };
        }
    }
}
