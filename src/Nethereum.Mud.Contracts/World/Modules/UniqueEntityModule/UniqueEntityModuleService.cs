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
using Nethereum.Mud.Contracts.World.Modules.UniqueEntityModule.ContractDefinition;

namespace Nethereum.Mud.Contracts.World.Modules.UniqueEntityModule
{
    public partial class UniqueEntityModuleService : ContractWeb3ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(IWeb3 web3, UniqueEntityModuleDeployment uniqueEntityModuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<UniqueEntityModuleDeployment>().SendRequestAndWaitForReceiptAsync(uniqueEntityModuleDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(IWeb3 web3, UniqueEntityModuleDeployment uniqueEntityModuleDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<UniqueEntityModuleDeployment>().SendRequestAsync(uniqueEntityModuleDeployment);
        }

        public static async Task<UniqueEntityModuleService> DeployContractAndGetServiceAsync(IWeb3 web3, UniqueEntityModuleDeployment uniqueEntityModuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, uniqueEntityModuleDeployment, cancellationTokenSource);
            return new UniqueEntityModuleService(web3, receipt.ContractAddress);
        }

        public UniqueEntityModuleService(IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> MsgSenderQueryAsync(MsgSenderFunction msgSenderFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgSenderFunction, string>(msgSenderFunction, blockParameter);
        }


        public Task<string> MsgSenderQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgSenderFunction, string>(null, blockParameter);
        }

        public Task<BigInteger> MsgValueQueryAsync(MsgValueFunction msgValueFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgValueFunction, BigInteger>(msgValueFunction, blockParameter);
        }


        public Task<BigInteger> MsgValueQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<MsgValueFunction, BigInteger>(null, blockParameter);
        }

        public Task<string> WorldQueryAsync(WorldFunction worldFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WorldFunction, string>(worldFunction, blockParameter);
        }


        public Task<string> WorldQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WorldFunction, string>(null, blockParameter);
        }

        public Task<string> InstallRequestAsync(InstallFunction installFunction)
        {
            return ContractHandler.SendRequestAsync(installFunction);
        }

        public Task<TransactionReceipt> InstallRequestAndWaitForReceiptAsync(InstallFunction installFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(installFunction, cancellationToken);
        }

        public Task<string> InstallRequestAsync(byte[] encodedArgs)
        {
            var installFunction = new InstallFunction();
            installFunction.EncodedArgs = encodedArgs;

            return ContractHandler.SendRequestAsync(installFunction);
        }

        public Task<TransactionReceipt> InstallRequestAndWaitForReceiptAsync(byte[] encodedArgs, CancellationTokenSource cancellationToken = null)
        {
            var installFunction = new InstallFunction();
            installFunction.EncodedArgs = encodedArgs;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(installFunction, cancellationToken);
        }

        public Task<string> InstallRootRequestAsync(InstallRootFunction installRootFunction)
        {
            return ContractHandler.SendRequestAsync(installRootFunction);
        }

        public Task<TransactionReceipt> InstallRootRequestAndWaitForReceiptAsync(InstallRootFunction installRootFunction, CancellationTokenSource cancellationToken = null)
        {
            return ContractHandler.SendRequestAndWaitForReceiptAsync(installRootFunction, cancellationToken);
        }

        public Task<string> InstallRootRequestAsync(byte[] encodedArgs)
        {
            var installRootFunction = new InstallRootFunction();
            installRootFunction.EncodedArgs = encodedArgs;

            return ContractHandler.SendRequestAsync(installRootFunction);
        }

        public Task<TransactionReceipt> InstallRootRequestAndWaitForReceiptAsync(byte[] encodedArgs, CancellationTokenSource cancellationToken = null)
        {
            var installRootFunction = new InstallRootFunction();
            installRootFunction.EncodedArgs = encodedArgs;

            return ContractHandler.SendRequestAndWaitForReceiptAsync(installRootFunction, cancellationToken);
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

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(MsgSenderFunction),
                typeof(MsgValueFunction),
                typeof(WorldFunction),
                typeof(InstallFunction),
                typeof(InstallRootFunction),
                typeof(SupportsInterfaceFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(StoreSetrecordEventDTO),
                typeof(StoreSplicestaticdataEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(FieldlayoutEmptyError),
                typeof(FieldlayoutInvalidstaticdatalengthError),
                typeof(FieldlayoutStaticlengthdoesnotfitinawordError),
                typeof(FieldlayoutStaticlengthisnotzeroError),
                typeof(FieldlayoutStaticlengthiszeroError),
                typeof(FieldlayoutToomanydynamicfieldsError),
                typeof(FieldlayoutToomanyfieldsError),
                typeof(ModuleAlreadyinstalledError),
                typeof(ModuleMissingdependencyError),
                typeof(ModuleNonrootinstallnotsupportedError),
                typeof(ModuleRootinstallnotsupportedError),
                typeof(SchemaInvalidlengthError),
                typeof(SchemaStatictypeafterdynamictypeError),
                typeof(SliceOutofboundsError),
                typeof(StoreInvalidfieldnameslengthError),
                typeof(StoreInvalidkeynameslengthError),
                typeof(StoreInvalidresourcetypeError),
                typeof(StoreInvalidstaticdatalengthError),
                typeof(StoreInvalidvalueschemadynamiclengthError),
                typeof(StoreInvalidvalueschemalengthError),
                typeof(StoreInvalidvalueschemastaticlengthError),
                typeof(StoreTablealreadyexistsError)
            };
        }
    }
}
