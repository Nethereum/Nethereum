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
using Nethereum.Mud.Contracts.World.Systems.RegistrationSystem.ContractDefinition;
using System.Threading;

namespace Nethereum.Mud.Contracts.World.Systems.RegistrationSystem
{
    public partial class RegistrationSystemService: ContractWeb3ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, RegistrationSystemDeployment registrationSystemDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<RegistrationSystemDeployment>().SendRequestAndWaitForReceiptAsync(registrationSystemDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, RegistrationSystemDeployment registrationSystemDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<RegistrationSystemDeployment>().SendRequestAsync(registrationSystemDeployment);
        }

        public static async Task<RegistrationSystemService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, RegistrationSystemDeployment registrationSystemDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, registrationSystemDeployment, cancellationTokenSource);
            return new RegistrationSystemService(web3, receipt.ContractAddress);
        }

        public RegistrationSystemService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
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

        public Task<string> InstallModuleRequestAsync(InstallModuleFunction installModuleFunction)
        {
             return ContractHandler.SendRequestAsync(installModuleFunction);
        }

        public Task<TransactionReceipt> InstallModuleRequestAndWaitForReceiptAsync(InstallModuleFunction installModuleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(installModuleFunction, cancellationToken);
        }

        public Task<string> InstallModuleRequestAsync(string module, byte[] encodedArgs)
        {
            var installModuleFunction = new InstallModuleFunction();
                installModuleFunction.Module = module;
                installModuleFunction.EncodedArgs = encodedArgs;
            
             return ContractHandler.SendRequestAsync(installModuleFunction);
        }

        public Task<TransactionReceipt> InstallModuleRequestAndWaitForReceiptAsync(string module, byte[] encodedArgs, CancellationTokenSource cancellationToken = null)
        {
            var installModuleFunction = new InstallModuleFunction();
                installModuleFunction.Module = module;
                installModuleFunction.EncodedArgs = encodedArgs;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(installModuleFunction, cancellationToken);
        }

        public Task<string> RegisterDelegationRequestAsync(RegisterDelegationFunction registerDelegationFunction)
        {
             return ContractHandler.SendRequestAsync(registerDelegationFunction);
        }

        public Task<TransactionReceipt> RegisterDelegationRequestAndWaitForReceiptAsync(RegisterDelegationFunction registerDelegationFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerDelegationFunction, cancellationToken);
        }

        public Task<string> RegisterDelegationRequestAsync(string delegatee, byte[] delegationControlId, byte[] initCallData)
        {
            var registerDelegationFunction = new RegisterDelegationFunction();
                registerDelegationFunction.Delegatee = delegatee;
                registerDelegationFunction.DelegationControlId = delegationControlId;
                registerDelegationFunction.InitCallData = initCallData;
            
             return ContractHandler.SendRequestAsync(registerDelegationFunction);
        }

        public Task<TransactionReceipt> RegisterDelegationRequestAndWaitForReceiptAsync(string delegatee, byte[] delegationControlId, byte[] initCallData, CancellationTokenSource cancellationToken = null)
        {
            var registerDelegationFunction = new RegisterDelegationFunction();
                registerDelegationFunction.Delegatee = delegatee;
                registerDelegationFunction.DelegationControlId = delegationControlId;
                registerDelegationFunction.InitCallData = initCallData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerDelegationFunction, cancellationToken);
        }

        public Task<string> RegisterFunctionSelectorRequestAsync(RegisterFunctionSelectorFunction registerFunctionSelectorFunction)
        {
             return ContractHandler.SendRequestAsync(registerFunctionSelectorFunction);
        }

        public Task<TransactionReceipt> RegisterFunctionSelectorRequestAndWaitForReceiptAsync(RegisterFunctionSelectorFunction registerFunctionSelectorFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunctionSelectorFunction, cancellationToken);
        }

        public Task<string> RegisterFunctionSelectorRequestAsync(byte[] systemId, string systemFunctionSignature)
        {
            var registerFunctionSelectorFunction = new RegisterFunctionSelectorFunction();
                registerFunctionSelectorFunction.SystemId = systemId;
                registerFunctionSelectorFunction.SystemFunctionSignature = systemFunctionSignature;
            
             return ContractHandler.SendRequestAsync(registerFunctionSelectorFunction);
        }

        public Task<TransactionReceipt> RegisterFunctionSelectorRequestAndWaitForReceiptAsync(byte[] systemId, string systemFunctionSignature, CancellationTokenSource cancellationToken = null)
        {
            var registerFunctionSelectorFunction = new RegisterFunctionSelectorFunction();
                registerFunctionSelectorFunction.SystemId = systemId;
                registerFunctionSelectorFunction.SystemFunctionSignature = systemFunctionSignature;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerFunctionSelectorFunction, cancellationToken);
        }

        public Task<string> RegisterNamespaceRequestAsync(RegisterNamespaceFunction registerNamespaceFunction)
        {
             return ContractHandler.SendRequestAsync(registerNamespaceFunction);
        }

        public Task<TransactionReceipt> RegisterNamespaceRequestAndWaitForReceiptAsync(RegisterNamespaceFunction registerNamespaceFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerNamespaceFunction, cancellationToken);
        }

        public Task<string> RegisterNamespaceRequestAsync(byte[] namespaceId)
        {
            var registerNamespaceFunction = new RegisterNamespaceFunction();
                registerNamespaceFunction.NamespaceId = namespaceId;
            
             return ContractHandler.SendRequestAsync(registerNamespaceFunction);
        }

        public Task<TransactionReceipt> RegisterNamespaceRequestAndWaitForReceiptAsync(byte[] namespaceId, CancellationTokenSource cancellationToken = null)
        {
            var registerNamespaceFunction = new RegisterNamespaceFunction();
                registerNamespaceFunction.NamespaceId = namespaceId;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerNamespaceFunction, cancellationToken);
        }

        public Task<string> RegisterNamespaceDelegationRequestAsync(RegisterNamespaceDelegationFunction registerNamespaceDelegationFunction)
        {
             return ContractHandler.SendRequestAsync(registerNamespaceDelegationFunction);
        }

        public Task<TransactionReceipt> RegisterNamespaceDelegationRequestAndWaitForReceiptAsync(RegisterNamespaceDelegationFunction registerNamespaceDelegationFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerNamespaceDelegationFunction, cancellationToken);
        }

        public Task<string> RegisterNamespaceDelegationRequestAsync(byte[] namespaceId, byte[] delegationControlId, byte[] initCallData)
        {
            var registerNamespaceDelegationFunction = new RegisterNamespaceDelegationFunction();
                registerNamespaceDelegationFunction.NamespaceId = namespaceId;
                registerNamespaceDelegationFunction.DelegationControlId = delegationControlId;
                registerNamespaceDelegationFunction.InitCallData = initCallData;
            
             return ContractHandler.SendRequestAsync(registerNamespaceDelegationFunction);
        }

        public Task<TransactionReceipt> RegisterNamespaceDelegationRequestAndWaitForReceiptAsync(byte[] namespaceId, byte[] delegationControlId, byte[] initCallData, CancellationTokenSource cancellationToken = null)
        {
            var registerNamespaceDelegationFunction = new RegisterNamespaceDelegationFunction();
                registerNamespaceDelegationFunction.NamespaceId = namespaceId;
                registerNamespaceDelegationFunction.DelegationControlId = delegationControlId;
                registerNamespaceDelegationFunction.InitCallData = initCallData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerNamespaceDelegationFunction, cancellationToken);
        }

        public Task<string> RegisterRootFunctionSelectorRequestAsync(RegisterRootFunctionSelectorFunction registerRootFunctionSelectorFunction)
        {
             return ContractHandler.SendRequestAsync(registerRootFunctionSelectorFunction);
        }

        public Task<TransactionReceipt> RegisterRootFunctionSelectorRequestAndWaitForReceiptAsync(RegisterRootFunctionSelectorFunction registerRootFunctionSelectorFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerRootFunctionSelectorFunction, cancellationToken);
        }

        public Task<string> RegisterRootFunctionSelectorRequestAsync(byte[] systemId, string worldFunctionSignature, string systemFunctionSignature)
        {
            var registerRootFunctionSelectorFunction = new RegisterRootFunctionSelectorFunction();
                registerRootFunctionSelectorFunction.SystemId = systemId;
                registerRootFunctionSelectorFunction.WorldFunctionSignature = worldFunctionSignature;
                registerRootFunctionSelectorFunction.SystemFunctionSignature = systemFunctionSignature;
            
             return ContractHandler.SendRequestAsync(registerRootFunctionSelectorFunction);
        }

        public Task<TransactionReceipt> RegisterRootFunctionSelectorRequestAndWaitForReceiptAsync(byte[] systemId, string worldFunctionSignature, string systemFunctionSignature, CancellationTokenSource cancellationToken = null)
        {
            var registerRootFunctionSelectorFunction = new RegisterRootFunctionSelectorFunction();
                registerRootFunctionSelectorFunction.SystemId = systemId;
                registerRootFunctionSelectorFunction.WorldFunctionSignature = worldFunctionSignature;
                registerRootFunctionSelectorFunction.SystemFunctionSignature = systemFunctionSignature;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerRootFunctionSelectorFunction, cancellationToken);
        }

        public Task<string> RegisterStoreHookRequestAsync(RegisterStoreHookFunction registerStoreHookFunction)
        {
             return ContractHandler.SendRequestAsync(registerStoreHookFunction);
        }

        public Task<TransactionReceipt> RegisterStoreHookRequestAndWaitForReceiptAsync(RegisterStoreHookFunction registerStoreHookFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerStoreHookFunction, cancellationToken);
        }

        public Task<string> RegisterStoreHookRequestAsync(byte[] tableId, string hookAddress, byte enabledHooksBitmap)
        {
            var registerStoreHookFunction = new RegisterStoreHookFunction();
                registerStoreHookFunction.TableId = tableId;
                registerStoreHookFunction.HookAddress = hookAddress;
                registerStoreHookFunction.EnabledHooksBitmap = enabledHooksBitmap;
            
             return ContractHandler.SendRequestAsync(registerStoreHookFunction);
        }

        public Task<TransactionReceipt> RegisterStoreHookRequestAndWaitForReceiptAsync(byte[] tableId, string hookAddress, byte enabledHooksBitmap, CancellationTokenSource cancellationToken = null)
        {
            var registerStoreHookFunction = new RegisterStoreHookFunction();
                registerStoreHookFunction.TableId = tableId;
                registerStoreHookFunction.HookAddress = hookAddress;
                registerStoreHookFunction.EnabledHooksBitmap = enabledHooksBitmap;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerStoreHookFunction, cancellationToken);
        }

        public Task<string> RegisterSystemRequestAsync(RegisterSystemFunction registerSystemFunction)
        {
             return ContractHandler.SendRequestAsync(registerSystemFunction);
        }

        public Task<TransactionReceipt> RegisterSystemRequestAndWaitForReceiptAsync(RegisterSystemFunction registerSystemFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerSystemFunction, cancellationToken);
        }

        public Task<string> RegisterSystemRequestAsync(byte[] systemId, string system, bool publicAccess)
        {
            var registerSystemFunction = new RegisterSystemFunction();
                registerSystemFunction.SystemId = systemId;
                registerSystemFunction.System = system;
                registerSystemFunction.PublicAccess = publicAccess;
            
             return ContractHandler.SendRequestAsync(registerSystemFunction);
        }

        public Task<TransactionReceipt> RegisterSystemRequestAndWaitForReceiptAsync(byte[] systemId, string system, bool publicAccess, CancellationTokenSource cancellationToken = null)
        {
            var registerSystemFunction = new RegisterSystemFunction();
                registerSystemFunction.SystemId = systemId;
                registerSystemFunction.System = system;
                registerSystemFunction.PublicAccess = publicAccess;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerSystemFunction, cancellationToken);
        }

        public Task<string> RegisterSystemHookRequestAsync(RegisterSystemHookFunction registerSystemHookFunction)
        {
             return ContractHandler.SendRequestAsync(registerSystemHookFunction);
        }

        public Task<TransactionReceipt> RegisterSystemHookRequestAndWaitForReceiptAsync(RegisterSystemHookFunction registerSystemHookFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerSystemHookFunction, cancellationToken);
        }

        public Task<string> RegisterSystemHookRequestAsync(byte[] systemId, string hookAddress, byte enabledHooksBitmap)
        {
            var registerSystemHookFunction = new RegisterSystemHookFunction();
                registerSystemHookFunction.SystemId = systemId;
                registerSystemHookFunction.HookAddress = hookAddress;
                registerSystemHookFunction.EnabledHooksBitmap = enabledHooksBitmap;
            
             return ContractHandler.SendRequestAsync(registerSystemHookFunction);
        }

        public Task<TransactionReceipt> RegisterSystemHookRequestAndWaitForReceiptAsync(byte[] systemId, string hookAddress, byte enabledHooksBitmap, CancellationTokenSource cancellationToken = null)
        {
            var registerSystemHookFunction = new RegisterSystemHookFunction();
                registerSystemHookFunction.SystemId = systemId;
                registerSystemHookFunction.HookAddress = hookAddress;
                registerSystemHookFunction.EnabledHooksBitmap = enabledHooksBitmap;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerSystemHookFunction, cancellationToken);
        }

        public Task<string> RegisterTableRequestAsync(RegisterTableFunction registerTableFunction)
        {
             return ContractHandler.SendRequestAsync(registerTableFunction);
        }

        public Task<TransactionReceipt> RegisterTableRequestAndWaitForReceiptAsync(RegisterTableFunction registerTableFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerTableFunction, cancellationToken);
        }

        public Task<string> RegisterTableRequestAsync(byte[] tableId, byte[] fieldLayout, byte[] keySchema, byte[] valueSchema, List<string> keyNames, List<string> fieldNames)
        {
            var registerTableFunction = new RegisterTableFunction();
                registerTableFunction.TableId = tableId;
                registerTableFunction.FieldLayout = fieldLayout;
                registerTableFunction.KeySchema = keySchema;
                registerTableFunction.ValueSchema = valueSchema;
                registerTableFunction.KeyNames = keyNames;
                registerTableFunction.FieldNames = fieldNames;
            
             return ContractHandler.SendRequestAsync(registerTableFunction);
        }

        public Task<TransactionReceipt> RegisterTableRequestAndWaitForReceiptAsync(byte[] tableId, byte[] fieldLayout, byte[] keySchema, byte[] valueSchema, List<string> keyNames, List<string> fieldNames, CancellationTokenSource cancellationToken = null)
        {
            var registerTableFunction = new RegisterTableFunction();
                registerTableFunction.TableId = tableId;
                registerTableFunction.FieldLayout = fieldLayout;
                registerTableFunction.KeySchema = keySchema;
                registerTableFunction.ValueSchema = valueSchema;
                registerTableFunction.KeyNames = keyNames;
                registerTableFunction.FieldNames = fieldNames;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(registerTableFunction, cancellationToken);
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

        public Task<string> UnregisterDelegationRequestAsync(UnregisterDelegationFunction unregisterDelegationFunction)
        {
             return ContractHandler.SendRequestAsync(unregisterDelegationFunction);
        }

        public Task<TransactionReceipt> UnregisterDelegationRequestAndWaitForReceiptAsync(UnregisterDelegationFunction unregisterDelegationFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unregisterDelegationFunction, cancellationToken);
        }

        public Task<string> UnregisterDelegationRequestAsync(string delegatee)
        {
            var unregisterDelegationFunction = new UnregisterDelegationFunction();
                unregisterDelegationFunction.Delegatee = delegatee;
            
             return ContractHandler.SendRequestAsync(unregisterDelegationFunction);
        }

        public Task<TransactionReceipt> UnregisterDelegationRequestAndWaitForReceiptAsync(string delegatee, CancellationTokenSource cancellationToken = null)
        {
            var unregisterDelegationFunction = new UnregisterDelegationFunction();
                unregisterDelegationFunction.Delegatee = delegatee;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unregisterDelegationFunction, cancellationToken);
        }

        public Task<string> UnregisterNamespaceDelegationRequestAsync(UnregisterNamespaceDelegationFunction unregisterNamespaceDelegationFunction)
        {
             return ContractHandler.SendRequestAsync(unregisterNamespaceDelegationFunction);
        }

        public Task<TransactionReceipt> UnregisterNamespaceDelegationRequestAndWaitForReceiptAsync(UnregisterNamespaceDelegationFunction unregisterNamespaceDelegationFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unregisterNamespaceDelegationFunction, cancellationToken);
        }

        public Task<string> UnregisterNamespaceDelegationRequestAsync(byte[] namespaceId)
        {
            var unregisterNamespaceDelegationFunction = new UnregisterNamespaceDelegationFunction();
                unregisterNamespaceDelegationFunction.NamespaceId = namespaceId;
            
             return ContractHandler.SendRequestAsync(unregisterNamespaceDelegationFunction);
        }

        public Task<TransactionReceipt> UnregisterNamespaceDelegationRequestAndWaitForReceiptAsync(byte[] namespaceId, CancellationTokenSource cancellationToken = null)
        {
            var unregisterNamespaceDelegationFunction = new UnregisterNamespaceDelegationFunction();
                unregisterNamespaceDelegationFunction.NamespaceId = namespaceId;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unregisterNamespaceDelegationFunction, cancellationToken);
        }

        public Task<string> UnregisterStoreHookRequestAsync(UnregisterStoreHookFunction unregisterStoreHookFunction)
        {
             return ContractHandler.SendRequestAsync(unregisterStoreHookFunction);
        }

        public Task<TransactionReceipt> UnregisterStoreHookRequestAndWaitForReceiptAsync(UnregisterStoreHookFunction unregisterStoreHookFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unregisterStoreHookFunction, cancellationToken);
        }

        public Task<string> UnregisterStoreHookRequestAsync(byte[] tableId, string hookAddress)
        {
            var unregisterStoreHookFunction = new UnregisterStoreHookFunction();
                unregisterStoreHookFunction.TableId = tableId;
                unregisterStoreHookFunction.HookAddress = hookAddress;
            
             return ContractHandler.SendRequestAsync(unregisterStoreHookFunction);
        }

        public Task<TransactionReceipt> UnregisterStoreHookRequestAndWaitForReceiptAsync(byte[] tableId, string hookAddress, CancellationTokenSource cancellationToken = null)
        {
            var unregisterStoreHookFunction = new UnregisterStoreHookFunction();
                unregisterStoreHookFunction.TableId = tableId;
                unregisterStoreHookFunction.HookAddress = hookAddress;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unregisterStoreHookFunction, cancellationToken);
        }

        public Task<string> UnregisterSystemHookRequestAsync(UnregisterSystemHookFunction unregisterSystemHookFunction)
        {
             return ContractHandler.SendRequestAsync(unregisterSystemHookFunction);
        }

        public Task<TransactionReceipt> UnregisterSystemHookRequestAndWaitForReceiptAsync(UnregisterSystemHookFunction unregisterSystemHookFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unregisterSystemHookFunction, cancellationToken);
        }

        public Task<string> UnregisterSystemHookRequestAsync(byte[] systemId, string hookAddress)
        {
            var unregisterSystemHookFunction = new UnregisterSystemHookFunction();
                unregisterSystemHookFunction.SystemId = systemId;
                unregisterSystemHookFunction.HookAddress = hookAddress;
            
             return ContractHandler.SendRequestAsync(unregisterSystemHookFunction);
        }

        public Task<TransactionReceipt> UnregisterSystemHookRequestAndWaitForReceiptAsync(byte[] systemId, string hookAddress, CancellationTokenSource cancellationToken = null)
        {
            var unregisterSystemHookFunction = new UnregisterSystemHookFunction();
                unregisterSystemHookFunction.SystemId = systemId;
                unregisterSystemHookFunction.HookAddress = hookAddress;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(unregisterSystemHookFunction, cancellationToken);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(MsgSenderFunction),
                typeof(MsgValueFunction),
                typeof(WorldFunction),
                typeof(InstallModuleFunction),
                typeof(RegisterDelegationFunction),
                typeof(RegisterFunctionSelectorFunction),
                typeof(RegisterNamespaceFunction),
                typeof(RegisterNamespaceDelegationFunction),
                typeof(RegisterRootFunctionSelectorFunction),
                typeof(RegisterStoreHookFunction),
                typeof(RegisterSystemFunction),
                typeof(RegisterSystemHookFunction),
                typeof(RegisterTableFunction),
                typeof(SupportsInterfaceFunction),
                typeof(UnregisterDelegationFunction),
                typeof(UnregisterNamespaceDelegationFunction),
                typeof(UnregisterStoreHookFunction),
                typeof(UnregisterSystemHookFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(StoreDeleterecordEventDTO),
                typeof(StoreSetrecordEventDTO),
                typeof(StoreSplicedynamicdataEventDTO),
                typeof(StoreSplicestaticdataEventDTO)
            };
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(EncodedlengthsInvalidlengthError),
                typeof(FieldlayoutEmptyError),
                typeof(FieldlayoutInvalidstaticdatalengthError),
                typeof(FieldlayoutStaticlengthdoesnotfitinawordError),
                typeof(FieldlayoutStaticlengthisnotzeroError),
                typeof(FieldlayoutStaticlengthiszeroError),
                typeof(FieldlayoutToomanydynamicfieldsError),
                typeof(FieldlayoutToomanyfieldsError),
                typeof(SchemaInvalidlengthError),
                typeof(SchemaStatictypeafterdynamictypeError),
                typeof(SliceOutofboundsError),
                typeof(StoreIndexoutofboundsError),
                typeof(StoreInvalidfieldnameslengthError),
                typeof(StoreInvalidkeynameslengthError),
                typeof(StoreInvalidresourcetypeError),
                typeof(StoreInvalidspliceError),
                typeof(StoreInvalidstaticdatalengthError),
                typeof(StoreInvalidvalueschemadynamiclengthError),
                typeof(StoreInvalidvalueschemalengthError),
                typeof(StoreInvalidvalueschemastaticlengthError),
                typeof(StoreTablealreadyexistsError),
                typeof(StoreTablenotfoundError),
                typeof(UnauthorizedCallContextError),
                typeof(WorldAccessdeniedError),
                typeof(WorldAlreadyinitializedError),
                typeof(WorldCallbacknotallowedError),
                typeof(WorldDelegationnotfoundError),
                typeof(WorldFunctionselectoralreadyexistsError),
                typeof(WorldFunctionselectornotfoundError),
                typeof(WorldInsufficientbalanceError),
                typeof(WorldInterfacenotsupportedError),
                typeof(WorldInvalidnamespaceError),
                typeof(WorldInvalidresourceidError),
                typeof(WorldInvalidresourcetypeError),
                typeof(WorldResourcealreadyexistsError),
                typeof(WorldResourcenotfoundError),
                typeof(WorldSystemalreadyexistsError),
                typeof(WorldUnlimiteddelegationnotallowedError)
            };
        }
    }
}
