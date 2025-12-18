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
using Nethereum.Circles.Contracts.Migration.ContractDefinition;

namespace Nethereum.Circles.Contracts.Migration
{
    public partial class MigrationService: ContractWeb3ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, MigrationDeployment migrationDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<MigrationDeployment>().SendRequestAndWaitForReceiptAsync(migrationDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, MigrationDeployment migrationDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<MigrationDeployment>().SendRequestAsync(migrationDeployment);
        }

        public static async Task<MigrationService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, MigrationDeployment migrationDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, migrationDeployment, cancellationTokenSource);
            return new MigrationService(web3, receipt.ContractAddress);
        }

        public MigrationService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<BigInteger> ConvertFromV1ToDemurrageQueryAsync(ConvertFromV1ToDemurrageFunction convertFromV1ToDemurrageFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<ConvertFromV1ToDemurrageFunction, BigInteger>(convertFromV1ToDemurrageFunction, blockParameter);
        }

        
        public Task<BigInteger> ConvertFromV1ToDemurrageQueryAsync(BigInteger amount, BlockParameter blockParameter = null)
        {
            var convertFromV1ToDemurrageFunction = new ConvertFromV1ToDemurrageFunction();
                convertFromV1ToDemurrageFunction.Amount = amount;
            
            return ContractHandler.QueryAsync<ConvertFromV1ToDemurrageFunction, BigInteger>(convertFromV1ToDemurrageFunction, blockParameter);
        }

        public Task<string> HubV1QueryAsync(HubV1Function hubV1Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HubV1Function, string>(hubV1Function, blockParameter);
        }

        
        public Task<string> HubV1QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HubV1Function, string>(null, blockParameter);
        }

        public Task<string> HubV2QueryAsync(HubV2Function hubV2Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HubV2Function, string>(hubV2Function, blockParameter);
        }

        
        public Task<string> HubV2QueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<HubV2Function, string>(null, blockParameter);
        }

        public Task<BigInteger> InflationDayZeroQueryAsync(InflationDayZeroFunction inflationDayZeroFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<InflationDayZeroFunction, BigInteger>(inflationDayZeroFunction, blockParameter);
        }

        
        public Task<BigInteger> InflationDayZeroQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<InflationDayZeroFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> MigrateRequestAsync(MigrateFunction migrateFunction)
        {
             return ContractHandler.SendRequestAsync(migrateFunction);
        }

        public Task<TransactionReceipt> MigrateRequestAndWaitForReceiptAsync(MigrateFunction migrateFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(migrateFunction, cancellationToken);
        }

        public Task<string> MigrateRequestAsync(List<string> avatars, List<BigInteger> amounts)
        {
            var migrateFunction = new MigrateFunction();
                migrateFunction.Avatars = avatars;
                migrateFunction.Amounts = amounts;
            
             return ContractHandler.SendRequestAsync(migrateFunction);
        }

        public Task<TransactionReceipt> MigrateRequestAndWaitForReceiptAsync(List<string> avatars, List<BigInteger> amounts, CancellationTokenSource cancellationToken = null)
        {
            var migrateFunction = new MigrateFunction();
                migrateFunction.Avatars = avatars;
                migrateFunction.Amounts = amounts;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(migrateFunction, cancellationToken);
        }

        public Task<BigInteger> PeriodQueryAsync(PeriodFunction periodFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PeriodFunction, BigInteger>(periodFunction, blockParameter);
        }

        
        public Task<BigInteger> PeriodQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<PeriodFunction, BigInteger>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(ConvertFromV1ToDemurrageFunction),
                typeof(HubV1Function),
                typeof(HubV2Function),
                typeof(InflationDayZeroFunction),
                typeof(MigrateFunction),
                typeof(PeriodFunction)
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
                typeof(CirclesAmountOverflowError),
                typeof(CirclesErrorAddressUintArgsError),
                typeof(CirclesErrorNoArgsError),
                typeof(CirclesErrorOneAddressArgError),
                typeof(CirclesIdMustBeDerivedFromAddressError),
                typeof(CirclesInvalidCirclesIdError),
                typeof(CirclesInvalidParameterError),
                typeof(CirclesMigrationAmountMustBeGreaterThanZeroError),
                typeof(CirclesProxyAlreadyInitializedError),
                typeof(CirclesReentrancyGuardError)
            };
        }
    }
}
