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
using Nethereum.Mud.Contracts.InitModule.ContractDefinition;

namespace Nethereum.Mud.Contracts.InitModule
{
    public partial class InitModuleService: ContractWeb3ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, InitModuleDeployment initModuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<InitModuleDeployment>().SendRequestAndWaitForReceiptAsync(initModuleDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, InitModuleDeployment initModuleDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<InitModuleDeployment>().SendRequestAsync(initModuleDeployment);
        }

        public static async Task<InitModuleService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, InitModuleDeployment initModuleDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, initModuleDeployment, cancellationTokenSource);
            return new InitModuleService(web3, receipt.ContractAddress);
        }

        public InitModuleService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
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

        public Task<string> InstallRootRequestAsync(InstallRootFunction installRootFunction)
        {
             return ContractHandler.SendRequestAsync(installRootFunction);
        }

        public Task<TransactionReceipt> InstallRootRequestAndWaitForReceiptAsync(InstallRootFunction installRootFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(installRootFunction, cancellationToken);
        }

        public Task<string> InstallRootRequestAsync(byte[] returnValue1)
        {
            var installRootFunction = new InstallRootFunction();
                installRootFunction.ReturnValue1 = returnValue1;
            
             return ContractHandler.SendRequestAsync(installRootFunction);
        }

        public Task<TransactionReceipt> InstallRootRequestAndWaitForReceiptAsync(byte[] returnValue1, CancellationTokenSource cancellationToken = null)
        {
            var installRootFunction = new InstallRootFunction();
                installRootFunction.ReturnValue1 = returnValue1;
            
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
