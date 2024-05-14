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
using Nethereum.Mud.Contracts.World.ContractDefinition;

namespace Nethereum.Mud.Contracts.World
{

   


    public partial class WorldService: ContractWeb3ServiceBase
    {
        public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, WorldDeployment worldDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            return web3.Eth.GetContractDeploymentHandler<WorldDeployment>().SendRequestAndWaitForReceiptAsync(worldDeployment, cancellationTokenSource);
        }

        public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, WorldDeployment worldDeployment)
        {
            return web3.Eth.GetContractDeploymentHandler<WorldDeployment>().SendRequestAsync(worldDeployment);
        }

        public static async Task<WorldService> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, WorldDeployment worldDeployment, CancellationTokenSource cancellationTokenSource = null)
        {
            var receipt = await DeployContractAndWaitForReceiptAsync(web3, worldDeployment, cancellationTokenSource);
            return new WorldService(web3, receipt.ContractAddress);
        }

        public WorldService(Nethereum.Web3.IWeb3 web3, string contractAddress) : base(web3, contractAddress)
        {
        }

        public Task<string> CallRequestAsync(CallFunction callFunction)
        {
             return ContractHandler.SendRequestAsync(callFunction);
        }

        public Task<TransactionReceipt> CallRequestAndWaitForReceiptAsync(CallFunction callFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(callFunction, cancellationToken);
        }

        public Task<string> CallRequestAsync(byte[] systemId, byte[] callData)
        {
            var callFunction = new CallFunction();
                callFunction.SystemId = systemId;
                callFunction.CallData = callData;
            
             return ContractHandler.SendRequestAsync(callFunction);
        }

        public Task<TransactionReceipt> CallRequestAndWaitForReceiptAsync(byte[] systemId, byte[] callData, CancellationTokenSource cancellationToken = null)
        {
            var callFunction = new CallFunction();
                callFunction.SystemId = systemId;
                callFunction.CallData = callData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(callFunction, cancellationToken);
        }

        public Task<string> CallFromRequestAsync(CallFromFunction callFromFunction)
        {
             return ContractHandler.SendRequestAsync(callFromFunction);
        }

        public Task<TransactionReceipt> CallFromRequestAndWaitForReceiptAsync(CallFromFunction callFromFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(callFromFunction, cancellationToken);
        }

        public Task<string> CallFromRequestAsync(string delegator, byte[] systemId, byte[] callData)
        {
            var callFromFunction = new CallFromFunction();
                callFromFunction.Delegator = delegator;
                callFromFunction.SystemId = systemId;
                callFromFunction.CallData = callData;
            
             return ContractHandler.SendRequestAsync(callFromFunction);
        }

        public Task<TransactionReceipt> CallFromRequestAndWaitForReceiptAsync(string delegator, byte[] systemId, byte[] callData, CancellationTokenSource cancellationToken = null)
        {
            var callFromFunction = new CallFromFunction();
                callFromFunction.Delegator = delegator;
                callFromFunction.SystemId = systemId;
                callFromFunction.CallData = callData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(callFromFunction, cancellationToken);
        }

        public Task<string> CreatorQueryAsync(CreatorFunction creatorFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CreatorFunction, string>(creatorFunction, blockParameter);
        }

        
        public Task<string> CreatorQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<CreatorFunction, string>(null, blockParameter);
        }

        public Task<string> DeleteRecordRequestAsync(DeleteRecordFunction deleteRecordFunction)
        {
             return ContractHandler.SendRequestAsync(deleteRecordFunction);
        }

        public Task<TransactionReceipt> DeleteRecordRequestAndWaitForReceiptAsync(DeleteRecordFunction deleteRecordFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(deleteRecordFunction, cancellationToken);
        }

        public Task<string> DeleteRecordRequestAsync(byte[] tableId, List<byte[]> keyTuple)
        {
            var deleteRecordFunction = new DeleteRecordFunction();
                deleteRecordFunction.TableId = tableId;
                deleteRecordFunction.KeyTuple = keyTuple;
            
             return ContractHandler.SendRequestAsync(deleteRecordFunction);
        }

        public Task<TransactionReceipt> DeleteRecordRequestAndWaitForReceiptAsync(byte[] tableId, List<byte[]> keyTuple, CancellationTokenSource cancellationToken = null)
        {
            var deleteRecordFunction = new DeleteRecordFunction();
                deleteRecordFunction.TableId = tableId;
                deleteRecordFunction.KeyTuple = keyTuple;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(deleteRecordFunction, cancellationToken);
        }

        public Task<byte[]> GetDynamicFieldQueryAsync(GetDynamicFieldFunction getDynamicFieldFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDynamicFieldFunction, byte[]>(getDynamicFieldFunction, blockParameter);
        }

        
        public Task<byte[]> GetDynamicFieldQueryAsync(byte[] tableId, List<byte[]> keyTuple, byte dynamicFieldIndex, BlockParameter blockParameter = null)
        {
            var getDynamicFieldFunction = new GetDynamicFieldFunction();
                getDynamicFieldFunction.TableId = tableId;
                getDynamicFieldFunction.KeyTuple = keyTuple;
                getDynamicFieldFunction.DynamicFieldIndex = dynamicFieldIndex;
            
            return ContractHandler.QueryAsync<GetDynamicFieldFunction, byte[]>(getDynamicFieldFunction, blockParameter);
        }

        public Task<BigInteger> GetDynamicFieldLengthQueryAsync(GetDynamicFieldLengthFunction getDynamicFieldLengthFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDynamicFieldLengthFunction, BigInteger>(getDynamicFieldLengthFunction, blockParameter);
        }

        
        public Task<BigInteger> GetDynamicFieldLengthQueryAsync(byte[] tableId, List<byte[]> keyTuple, byte dynamicFieldIndex, BlockParameter blockParameter = null)
        {
            var getDynamicFieldLengthFunction = new GetDynamicFieldLengthFunction();
                getDynamicFieldLengthFunction.TableId = tableId;
                getDynamicFieldLengthFunction.KeyTuple = keyTuple;
                getDynamicFieldLengthFunction.DynamicFieldIndex = dynamicFieldIndex;
            
            return ContractHandler.QueryAsync<GetDynamicFieldLengthFunction, BigInteger>(getDynamicFieldLengthFunction, blockParameter);
        }

        public Task<byte[]> GetDynamicFieldSliceQueryAsync(GetDynamicFieldSliceFunction getDynamicFieldSliceFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetDynamicFieldSliceFunction, byte[]>(getDynamicFieldSliceFunction, blockParameter);
        }

        
        public Task<byte[]> GetDynamicFieldSliceQueryAsync(byte[] tableId, List<byte[]> keyTuple, byte dynamicFieldIndex, BigInteger start, BigInteger end, BlockParameter blockParameter = null)
        {
            var getDynamicFieldSliceFunction = new GetDynamicFieldSliceFunction();
                getDynamicFieldSliceFunction.TableId = tableId;
                getDynamicFieldSliceFunction.KeyTuple = keyTuple;
                getDynamicFieldSliceFunction.DynamicFieldIndex = dynamicFieldIndex;
                getDynamicFieldSliceFunction.Start = start;
                getDynamicFieldSliceFunction.End = end;
            
            return ContractHandler.QueryAsync<GetDynamicFieldSliceFunction, byte[]>(getDynamicFieldSliceFunction, blockParameter);
        }

        public Task<byte[]> GetFieldQueryAsync(GetField1Function getField1Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetField1Function, byte[]>(getField1Function, blockParameter);
        }

        
        public Task<byte[]> GetFieldQueryAsync(byte[] tableId, List<byte[]> keyTuple, byte fieldIndex, byte[] fieldLayout, BlockParameter blockParameter = null)
        {
            var getField1Function = new GetField1Function();
                getField1Function.TableId = tableId;
                getField1Function.KeyTuple = keyTuple;
                getField1Function.FieldIndex = fieldIndex;
                getField1Function.FieldLayout = fieldLayout;
            
            return ContractHandler.QueryAsync<GetField1Function, byte[]>(getField1Function, blockParameter);
        }

        public Task<byte[]> GetFieldQueryAsync(GetFieldFunction getFieldFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetFieldFunction, byte[]>(getFieldFunction, blockParameter);
        }

        
        public Task<byte[]> GetFieldQueryAsync(byte[] tableId, List<byte[]> keyTuple, byte fieldIndex, BlockParameter blockParameter = null)
        {
            var getFieldFunction = new GetFieldFunction();
                getFieldFunction.TableId = tableId;
                getFieldFunction.KeyTuple = keyTuple;
                getFieldFunction.FieldIndex = fieldIndex;
            
            return ContractHandler.QueryAsync<GetFieldFunction, byte[]>(getFieldFunction, blockParameter);
        }

        public Task<byte[]> GetFieldLayoutQueryAsync(GetFieldLayoutFunction getFieldLayoutFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetFieldLayoutFunction, byte[]>(getFieldLayoutFunction, blockParameter);
        }

        
        public Task<byte[]> GetFieldLayoutQueryAsync(byte[] tableId, BlockParameter blockParameter = null)
        {
            var getFieldLayoutFunction = new GetFieldLayoutFunction();
                getFieldLayoutFunction.TableId = tableId;
            
            return ContractHandler.QueryAsync<GetFieldLayoutFunction, byte[]>(getFieldLayoutFunction, blockParameter);
        }

        public Task<BigInteger> GetFieldLengthQueryAsync(GetFieldLength1Function getFieldLength1Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetFieldLength1Function, BigInteger>(getFieldLength1Function, blockParameter);
        }

        
        public Task<BigInteger> GetFieldLengthQueryAsync(byte[] tableId, List<byte[]> keyTuple, byte fieldIndex, byte[] fieldLayout, BlockParameter blockParameter = null)
        {
            var getFieldLength1Function = new GetFieldLength1Function();
                getFieldLength1Function.TableId = tableId;
                getFieldLength1Function.KeyTuple = keyTuple;
                getFieldLength1Function.FieldIndex = fieldIndex;
                getFieldLength1Function.FieldLayout = fieldLayout;
            
            return ContractHandler.QueryAsync<GetFieldLength1Function, BigInteger>(getFieldLength1Function, blockParameter);
        }

        public Task<BigInteger> GetFieldLengthQueryAsync(GetFieldLengthFunction getFieldLengthFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetFieldLengthFunction, BigInteger>(getFieldLengthFunction, blockParameter);
        }

        
        public Task<BigInteger> GetFieldLengthQueryAsync(byte[] tableId, List<byte[]> keyTuple, byte fieldIndex, BlockParameter blockParameter = null)
        {
            var getFieldLengthFunction = new GetFieldLengthFunction();
                getFieldLengthFunction.TableId = tableId;
                getFieldLengthFunction.KeyTuple = keyTuple;
                getFieldLengthFunction.FieldIndex = fieldIndex;
            
            return ContractHandler.QueryAsync<GetFieldLengthFunction, BigInteger>(getFieldLengthFunction, blockParameter);
        }

        public Task<byte[]> GetKeySchemaQueryAsync(GetKeySchemaFunction getKeySchemaFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetKeySchemaFunction, byte[]>(getKeySchemaFunction, blockParameter);
        }

        
        public Task<byte[]> GetKeySchemaQueryAsync(byte[] tableId, BlockParameter blockParameter = null)
        {
            var getKeySchemaFunction = new GetKeySchemaFunction();
                getKeySchemaFunction.TableId = tableId;
            
            return ContractHandler.QueryAsync<GetKeySchemaFunction, byte[]>(getKeySchemaFunction, blockParameter);
        }

        public Task<GetRecord1OutputDTO> GetRecordQueryAsync(GetRecord1Function getRecord1Function, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetRecord1Function, GetRecord1OutputDTO>(getRecord1Function, blockParameter);
        }

        public Task<GetRecord1OutputDTO> GetRecordQueryAsync(byte[] tableId, List<byte[]> keyTuple, byte[] fieldLayout, BlockParameter blockParameter = null)
        {
            var getRecord1Function = new GetRecord1Function();
                getRecord1Function.TableId = tableId;
                getRecord1Function.KeyTuple = keyTuple;
                getRecord1Function.FieldLayout = fieldLayout;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetRecord1Function, GetRecord1OutputDTO>(getRecord1Function, blockParameter);
        }

        public Task<GetRecordOutputDTO> GetRecordQueryAsync(GetRecordFunction getRecordFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<GetRecordFunction, GetRecordOutputDTO>(getRecordFunction, blockParameter);
        }

        public Task<GetRecordOutputDTO> GetRecordQueryAsync(byte[] tableId, List<byte[]> keyTuple, BlockParameter blockParameter = null)
        {
            var getRecordFunction = new GetRecordFunction();
                getRecordFunction.TableId = tableId;
                getRecordFunction.KeyTuple = keyTuple;
            
            return ContractHandler.QueryDeserializingToObjectAsync<GetRecordFunction, GetRecordOutputDTO>(getRecordFunction, blockParameter);
        }

        public Task<byte[]> GetStaticFieldQueryAsync(GetStaticFieldFunction getStaticFieldFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetStaticFieldFunction, byte[]>(getStaticFieldFunction, blockParameter);
        }

        
        public Task<byte[]> GetStaticFieldQueryAsync(byte[] tableId, List<byte[]> keyTuple, byte fieldIndex, byte[] fieldLayout, BlockParameter blockParameter = null)
        {
            var getStaticFieldFunction = new GetStaticFieldFunction();
                getStaticFieldFunction.TableId = tableId;
                getStaticFieldFunction.KeyTuple = keyTuple;
                getStaticFieldFunction.FieldIndex = fieldIndex;
                getStaticFieldFunction.FieldLayout = fieldLayout;
            
            return ContractHandler.QueryAsync<GetStaticFieldFunction, byte[]>(getStaticFieldFunction, blockParameter);
        }

        public Task<byte[]> GetValueSchemaQueryAsync(GetValueSchemaFunction getValueSchemaFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<GetValueSchemaFunction, byte[]>(getValueSchemaFunction, blockParameter);
        }

        
        public Task<byte[]> GetValueSchemaQueryAsync(byte[] tableId, BlockParameter blockParameter = null)
        {
            var getValueSchemaFunction = new GetValueSchemaFunction();
                getValueSchemaFunction.TableId = tableId;
            
            return ContractHandler.QueryAsync<GetValueSchemaFunction, byte[]>(getValueSchemaFunction, blockParameter);
        }

        public Task<string> InitializeRequestAsync(InitializeFunction initializeFunction)
        {
             return ContractHandler.SendRequestAsync(initializeFunction);
        }

        public Task<TransactionReceipt> InitializeRequestAndWaitForReceiptAsync(InitializeFunction initializeFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeFunction, cancellationToken);
        }

        public Task<string> InitializeRequestAsync(string initModule)
        {
            var initializeFunction = new InitializeFunction();
                initializeFunction.InitModule = initModule;
            
             return ContractHandler.SendRequestAsync(initializeFunction);
        }

        public Task<TransactionReceipt> InitializeRequestAndWaitForReceiptAsync(string initModule, CancellationTokenSource cancellationToken = null)
        {
            var initializeFunction = new InitializeFunction();
                initializeFunction.InitModule = initModule;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(initializeFunction, cancellationToken);
        }

        public Task<string> InstallRootModuleRequestAsync(InstallRootModuleFunction installRootModuleFunction)
        {
             return ContractHandler.SendRequestAsync(installRootModuleFunction);
        }

        public Task<TransactionReceipt> InstallRootModuleRequestAndWaitForReceiptAsync(InstallRootModuleFunction installRootModuleFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(installRootModuleFunction, cancellationToken);
        }

        public Task<string> InstallRootModuleRequestAsync(string module, byte[] encodedArgs)
        {
            var installRootModuleFunction = new InstallRootModuleFunction();
                installRootModuleFunction.Module = module;
                installRootModuleFunction.EncodedArgs = encodedArgs;
            
             return ContractHandler.SendRequestAsync(installRootModuleFunction);
        }

        public Task<TransactionReceipt> InstallRootModuleRequestAndWaitForReceiptAsync(string module, byte[] encodedArgs, CancellationTokenSource cancellationToken = null)
        {
            var installRootModuleFunction = new InstallRootModuleFunction();
                installRootModuleFunction.Module = module;
                installRootModuleFunction.EncodedArgs = encodedArgs;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(installRootModuleFunction, cancellationToken);
        }

        public Task<string> PopFromDynamicFieldRequestAsync(PopFromDynamicFieldFunction popFromDynamicFieldFunction)
        {
             return ContractHandler.SendRequestAsync(popFromDynamicFieldFunction);
        }

        public Task<TransactionReceipt> PopFromDynamicFieldRequestAndWaitForReceiptAsync(PopFromDynamicFieldFunction popFromDynamicFieldFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(popFromDynamicFieldFunction, cancellationToken);
        }

        public Task<string> PopFromDynamicFieldRequestAsync(byte[] tableId, List<byte[]> keyTuple, byte dynamicFieldIndex, BigInteger byteLengthToPop)
        {
            var popFromDynamicFieldFunction = new PopFromDynamicFieldFunction();
                popFromDynamicFieldFunction.TableId = tableId;
                popFromDynamicFieldFunction.KeyTuple = keyTuple;
                popFromDynamicFieldFunction.DynamicFieldIndex = dynamicFieldIndex;
                popFromDynamicFieldFunction.ByteLengthToPop = byteLengthToPop;
            
             return ContractHandler.SendRequestAsync(popFromDynamicFieldFunction);
        }

        public Task<TransactionReceipt> PopFromDynamicFieldRequestAndWaitForReceiptAsync(byte[] tableId, List<byte[]> keyTuple, byte dynamicFieldIndex, BigInteger byteLengthToPop, CancellationTokenSource cancellationToken = null)
        {
            var popFromDynamicFieldFunction = new PopFromDynamicFieldFunction();
                popFromDynamicFieldFunction.TableId = tableId;
                popFromDynamicFieldFunction.KeyTuple = keyTuple;
                popFromDynamicFieldFunction.DynamicFieldIndex = dynamicFieldIndex;
                popFromDynamicFieldFunction.ByteLengthToPop = byteLengthToPop;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(popFromDynamicFieldFunction, cancellationToken);
        }

        public Task<string> PushToDynamicFieldRequestAsync(PushToDynamicFieldFunction pushToDynamicFieldFunction)
        {
             return ContractHandler.SendRequestAsync(pushToDynamicFieldFunction);
        }

        public Task<TransactionReceipt> PushToDynamicFieldRequestAndWaitForReceiptAsync(PushToDynamicFieldFunction pushToDynamicFieldFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(pushToDynamicFieldFunction, cancellationToken);
        }

        public Task<string> PushToDynamicFieldRequestAsync(byte[] tableId, List<byte[]> keyTuple, byte dynamicFieldIndex, byte[] dataToPush)
        {
            var pushToDynamicFieldFunction = new PushToDynamicFieldFunction();
                pushToDynamicFieldFunction.TableId = tableId;
                pushToDynamicFieldFunction.KeyTuple = keyTuple;
                pushToDynamicFieldFunction.DynamicFieldIndex = dynamicFieldIndex;
                pushToDynamicFieldFunction.DataToPush = dataToPush;
            
             return ContractHandler.SendRequestAsync(pushToDynamicFieldFunction);
        }

        public Task<TransactionReceipt> PushToDynamicFieldRequestAndWaitForReceiptAsync(byte[] tableId, List<byte[]> keyTuple, byte dynamicFieldIndex, byte[] dataToPush, CancellationTokenSource cancellationToken = null)
        {
            var pushToDynamicFieldFunction = new PushToDynamicFieldFunction();
                pushToDynamicFieldFunction.TableId = tableId;
                pushToDynamicFieldFunction.KeyTuple = keyTuple;
                pushToDynamicFieldFunction.DynamicFieldIndex = dynamicFieldIndex;
                pushToDynamicFieldFunction.DataToPush = dataToPush;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(pushToDynamicFieldFunction, cancellationToken);
        }

        public Task<string> SetDynamicFieldRequestAsync(SetDynamicFieldFunction setDynamicFieldFunction)
        {
             return ContractHandler.SendRequestAsync(setDynamicFieldFunction);
        }

        public Task<TransactionReceipt> SetDynamicFieldRequestAndWaitForReceiptAsync(SetDynamicFieldFunction setDynamicFieldFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setDynamicFieldFunction, cancellationToken);
        }

        public Task<string> SetDynamicFieldRequestAsync(byte[] tableId, List<byte[]> keyTuple, byte dynamicFieldIndex, byte[] data)
        {
            var setDynamicFieldFunction = new SetDynamicFieldFunction();
                setDynamicFieldFunction.TableId = tableId;
                setDynamicFieldFunction.KeyTuple = keyTuple;
                setDynamicFieldFunction.DynamicFieldIndex = dynamicFieldIndex;
                setDynamicFieldFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(setDynamicFieldFunction);
        }

        public Task<TransactionReceipt> SetDynamicFieldRequestAndWaitForReceiptAsync(byte[] tableId, List<byte[]> keyTuple, byte dynamicFieldIndex, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var setDynamicFieldFunction = new SetDynamicFieldFunction();
                setDynamicFieldFunction.TableId = tableId;
                setDynamicFieldFunction.KeyTuple = keyTuple;
                setDynamicFieldFunction.DynamicFieldIndex = dynamicFieldIndex;
                setDynamicFieldFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setDynamicFieldFunction, cancellationToken);
        }

        public Task<string> SetFieldRequestAsync(SetFieldFunction setFieldFunction)
        {
             return ContractHandler.SendRequestAsync(setFieldFunction);
        }

        public Task<TransactionReceipt> SetFieldRequestAndWaitForReceiptAsync(SetFieldFunction setFieldFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setFieldFunction, cancellationToken);
        }

        public Task<string> SetFieldRequestAsync(byte[] tableId, List<byte[]> keyTuple, byte fieldIndex, byte[] data)
        {
            var setFieldFunction = new SetFieldFunction();
                setFieldFunction.TableId = tableId;
                setFieldFunction.KeyTuple = keyTuple;
                setFieldFunction.FieldIndex = fieldIndex;
                setFieldFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(setFieldFunction);
        }

        public Task<TransactionReceipt> SetFieldRequestAndWaitForReceiptAsync(byte[] tableId, List<byte[]> keyTuple, byte fieldIndex, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var setFieldFunction = new SetFieldFunction();
                setFieldFunction.TableId = tableId;
                setFieldFunction.KeyTuple = keyTuple;
                setFieldFunction.FieldIndex = fieldIndex;
                setFieldFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setFieldFunction, cancellationToken);
        }

        public Task<string> SetFieldRequestAsync(SetField1Function setField1Function)
        {
             return ContractHandler.SendRequestAsync(setField1Function);
        }

        public Task<TransactionReceipt> SetFieldRequestAndWaitForReceiptAsync(SetField1Function setField1Function, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setField1Function, cancellationToken);
        }

        public Task<string> SetFieldRequestAsync(byte[] tableId, List<byte[]> keyTuple, byte fieldIndex, byte[] data, byte[] fieldLayout)
        {
            var setField1Function = new SetField1Function();
                setField1Function.TableId = tableId;
                setField1Function.KeyTuple = keyTuple;
                setField1Function.FieldIndex = fieldIndex;
                setField1Function.Data = data;
                setField1Function.FieldLayout = fieldLayout;
            
             return ContractHandler.SendRequestAsync(setField1Function);
        }

        public Task<TransactionReceipt> SetFieldRequestAndWaitForReceiptAsync(byte[] tableId, List<byte[]> keyTuple, byte fieldIndex, byte[] data, byte[] fieldLayout, CancellationTokenSource cancellationToken = null)
        {
            var setField1Function = new SetField1Function();
                setField1Function.TableId = tableId;
                setField1Function.KeyTuple = keyTuple;
                setField1Function.FieldIndex = fieldIndex;
                setField1Function.Data = data;
                setField1Function.FieldLayout = fieldLayout;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setField1Function, cancellationToken);
        }

        public Task<string> SetRecordRequestAsync(SetRecordFunction setRecordFunction)
        {
             return ContractHandler.SendRequestAsync(setRecordFunction);
        }

        public Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync(SetRecordFunction setRecordFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setRecordFunction, cancellationToken);
        }

        public Task<string> SetRecordRequestAsync(byte[] tableId, List<byte[]> keyTuple, byte[] staticData, byte[] encodedLengths, byte[] dynamicData)
        {
            var setRecordFunction = new SetRecordFunction();
                setRecordFunction.TableId = tableId;
                setRecordFunction.KeyTuple = keyTuple;
                setRecordFunction.StaticData = staticData;
                setRecordFunction.EncodedLengths = encodedLengths;
                setRecordFunction.DynamicData = dynamicData;
            
             return ContractHandler.SendRequestAsync(setRecordFunction);
        }

        public Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync(byte[] tableId, List<byte[]> keyTuple, byte[] staticData, byte[] encodedLengths, byte[] dynamicData, CancellationTokenSource cancellationToken = null)
        {
            var setRecordFunction = new SetRecordFunction();
                setRecordFunction.TableId = tableId;
                setRecordFunction.KeyTuple = keyTuple;
                setRecordFunction.StaticData = staticData;
                setRecordFunction.EncodedLengths = encodedLengths;
                setRecordFunction.DynamicData = dynamicData;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setRecordFunction, cancellationToken);
        }

        public Task<string> SetStaticFieldRequestAsync(SetStaticFieldFunction setStaticFieldFunction)
        {
             return ContractHandler.SendRequestAsync(setStaticFieldFunction);
        }

        public Task<TransactionReceipt> SetStaticFieldRequestAndWaitForReceiptAsync(SetStaticFieldFunction setStaticFieldFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setStaticFieldFunction, cancellationToken);
        }

        public Task<string> SetStaticFieldRequestAsync(byte[] tableId, List<byte[]> keyTuple, byte fieldIndex, byte[] data, byte[] fieldLayout)
        {
            var setStaticFieldFunction = new SetStaticFieldFunction();
                setStaticFieldFunction.TableId = tableId;
                setStaticFieldFunction.KeyTuple = keyTuple;
                setStaticFieldFunction.FieldIndex = fieldIndex;
                setStaticFieldFunction.Data = data;
                setStaticFieldFunction.FieldLayout = fieldLayout;
            
             return ContractHandler.SendRequestAsync(setStaticFieldFunction);
        }

        public Task<TransactionReceipt> SetStaticFieldRequestAndWaitForReceiptAsync(byte[] tableId, List<byte[]> keyTuple, byte fieldIndex, byte[] data, byte[] fieldLayout, CancellationTokenSource cancellationToken = null)
        {
            var setStaticFieldFunction = new SetStaticFieldFunction();
                setStaticFieldFunction.TableId = tableId;
                setStaticFieldFunction.KeyTuple = keyTuple;
                setStaticFieldFunction.FieldIndex = fieldIndex;
                setStaticFieldFunction.Data = data;
                setStaticFieldFunction.FieldLayout = fieldLayout;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(setStaticFieldFunction, cancellationToken);
        }

        public Task<string> SpliceDynamicDataRequestAsync(SpliceDynamicDataFunction spliceDynamicDataFunction)
        {
             return ContractHandler.SendRequestAsync(spliceDynamicDataFunction);
        }

        public Task<TransactionReceipt> SpliceDynamicDataRequestAndWaitForReceiptAsync(SpliceDynamicDataFunction spliceDynamicDataFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(spliceDynamicDataFunction, cancellationToken);
        }

        public Task<string> SpliceDynamicDataRequestAsync(byte[] tableId, List<byte[]> keyTuple, byte dynamicFieldIndex, ulong startWithinField, ulong deleteCount, byte[] data)
        {
            var spliceDynamicDataFunction = new SpliceDynamicDataFunction();
                spliceDynamicDataFunction.TableId = tableId;
                spliceDynamicDataFunction.KeyTuple = keyTuple;
                spliceDynamicDataFunction.DynamicFieldIndex = dynamicFieldIndex;
                spliceDynamicDataFunction.StartWithinField = startWithinField;
                spliceDynamicDataFunction.DeleteCount = deleteCount;
                spliceDynamicDataFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(spliceDynamicDataFunction);
        }

        public Task<TransactionReceipt> SpliceDynamicDataRequestAndWaitForReceiptAsync(byte[] tableId, List<byte[]> keyTuple, byte dynamicFieldIndex, ulong startWithinField, ulong deleteCount, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var spliceDynamicDataFunction = new SpliceDynamicDataFunction();
                spliceDynamicDataFunction.TableId = tableId;
                spliceDynamicDataFunction.KeyTuple = keyTuple;
                spliceDynamicDataFunction.DynamicFieldIndex = dynamicFieldIndex;
                spliceDynamicDataFunction.StartWithinField = startWithinField;
                spliceDynamicDataFunction.DeleteCount = deleteCount;
                spliceDynamicDataFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(spliceDynamicDataFunction, cancellationToken);
        }

        public Task<string> SpliceStaticDataRequestAsync(SpliceStaticDataFunction spliceStaticDataFunction)
        {
             return ContractHandler.SendRequestAsync(spliceStaticDataFunction);
        }

        public Task<TransactionReceipt> SpliceStaticDataRequestAndWaitForReceiptAsync(SpliceStaticDataFunction spliceStaticDataFunction, CancellationTokenSource cancellationToken = null)
        {
             return ContractHandler.SendRequestAndWaitForReceiptAsync(spliceStaticDataFunction, cancellationToken);
        }

        public Task<string> SpliceStaticDataRequestAsync(byte[] tableId, List<byte[]> keyTuple, ulong start, byte[] data)
        {
            var spliceStaticDataFunction = new SpliceStaticDataFunction();
                spliceStaticDataFunction.TableId = tableId;
                spliceStaticDataFunction.KeyTuple = keyTuple;
                spliceStaticDataFunction.Start = start;
                spliceStaticDataFunction.Data = data;
            
             return ContractHandler.SendRequestAsync(spliceStaticDataFunction);
        }

        public Task<TransactionReceipt> SpliceStaticDataRequestAndWaitForReceiptAsync(byte[] tableId, List<byte[]> keyTuple, ulong start, byte[] data, CancellationTokenSource cancellationToken = null)
        {
            var spliceStaticDataFunction = new SpliceStaticDataFunction();
                spliceStaticDataFunction.TableId = tableId;
                spliceStaticDataFunction.KeyTuple = keyTuple;
                spliceStaticDataFunction.Start = start;
                spliceStaticDataFunction.Data = data;
            
             return ContractHandler.SendRequestAndWaitForReceiptAsync(spliceStaticDataFunction, cancellationToken);
        }

        public Task<byte[]> StoreVersionQueryAsync(StoreVersionFunction storeVersionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<StoreVersionFunction, byte[]>(storeVersionFunction, blockParameter);
        }

        
        public Task<byte[]> StoreVersionQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<StoreVersionFunction, byte[]>(null, blockParameter);
        }

        public Task<byte[]> WorldVersionQueryAsync(WorldVersionFunction worldVersionFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WorldVersionFunction, byte[]>(worldVersionFunction, blockParameter);
        }

        
        public Task<byte[]> WorldVersionQueryAsync(BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryAsync<WorldVersionFunction, byte[]>(null, blockParameter);
        }

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(CallFunction),
                typeof(CallFromFunction),
                typeof(CreatorFunction),
                typeof(DeleteRecordFunction),
                typeof(GetDynamicFieldFunction),
                typeof(GetDynamicFieldLengthFunction),
                typeof(GetDynamicFieldSliceFunction),
                typeof(GetField1Function),
                typeof(GetFieldFunction),
                typeof(GetFieldLayoutFunction),
                typeof(GetFieldLength1Function),
                typeof(GetFieldLengthFunction),
                typeof(GetKeySchemaFunction),
                typeof(GetRecord1Function),
                typeof(GetRecordFunction),
                typeof(GetStaticFieldFunction),
                typeof(GetValueSchemaFunction),
                typeof(InitializeFunction),
                typeof(InstallRootModuleFunction),
                typeof(PopFromDynamicFieldFunction),
                typeof(PushToDynamicFieldFunction),
                typeof(SetDynamicFieldFunction),
                typeof(SetFieldFunction),
                typeof(SetField1Function),
                typeof(SetRecordFunction),
                typeof(SetStaticFieldFunction),
                typeof(SpliceDynamicDataFunction),
                typeof(SpliceStaticDataFunction),
                typeof(StoreVersionFunction),
                typeof(WorldVersionFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>
            {
                typeof(HelloStoreEventDTO),
                typeof(HelloWorldEventDTO),
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
                typeof(ModuleAlreadyinstalledError),
                typeof(ModuleMissingdependencyError),
                typeof(ModuleNonrootinstallnotsupportedError),
                typeof(ModuleRootinstallnotsupportedError),
                typeof(SchemaInvalidlengthError),
                typeof(SchemaStatictypeafterdynamictypeError),
                typeof(SliceOutofboundsError),
                typeof(StoreIndexoutofboundsError),
                typeof(StoreInvalidboundsError),
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
