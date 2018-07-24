using System;
using System.Linq;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Service
{
    public class FunctionServiceMethodCSharpTemplate
    {
        private readonly ServiceModel _model;
        private CommonGenerators _commonGenerators;
        private ITypeConvertor _typeConvertor;

        public FunctionServiceMethodCSharpTemplate(ServiceModel model)
        {
            _model = model;
            _typeConvertor = new ABITypeToCSharpType();
            _commonGenerators = new CommonGenerators();
        }

        public string GenerateMethods()
        {
            var functions = _model.ContractABI.Functions;
            return string.Join(Environment.NewLine, functions.Select(GenerateMethod));
        }

        public string GenerateMethod(FunctionABI functionABI)
        { 

            var functionCQSMessageModel = new FunctionCQSMessageModel(functionABI, _model.CQSNamespace);
            var functionOutputDTOModel = new FunctionOutputDTOModel(functionABI, _model.FunctionOutputNamespace);
            var functionABIModel = new FunctionABIModel(functionABI, _typeConvertor);

            var messageType = functionCQSMessageModel.GetTypeName();
            var messageVariableName = functionCQSMessageModel.GetVariableName();
            var functionNameUpper = _commonGenerators.GenerateClassName(functionABI.Name);

            if (functionABIModel.IsMultipleOutput() && !functionABIModel.IsTransaction())
            {

                var functionOutputDTOType = functionOutputDTOModel.GetTypeName();
                var returnWithInputParam =
$@"{SpaceUtils.TwoTabs}public Task<{functionOutputDTOType}> {functionNameUpper}QueryAsync({messageType} {messageVariableName}, BlockParameter blockParameter = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}return ContractHandler.QueryDeserializingToObjectAsync<{messageType}, {functionOutputDTOType}>({messageVariableName}, blockParameter);
{SpaceUtils.TwoTabs}}}";

                var returnWithoutInputParam =
                    $@"{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}public Task<{functionOutputDTOType}> {functionNameUpper}QueryAsync(BlockParameter blockParameter = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}return ContractHandler.QueryDeserializingToObjectAsync<{messageType}, {functionOutputDTOType}>(null, blockParameter);
{SpaceUtils.TwoTabs}}}";

                if (functionABIModel.HasNoInputParameters())
                {
                    return returnWithInputParam + returnWithoutInputParam;
                }

                return returnWithInputParam;
            }

            if(functionABIModel.IsSingleOutput() && !functionABIModel.IsTransaction())
            if (functionABI.OutputParameters != null && functionABI.OutputParameters.Length == 1 &&
                functionABI.Constant)
            {
                var type = functionABIModel.GetSingleOutputReturnType();

                var returnWithInputParam = 
                    $@"{SpaceUtils.TwoTabs}public Task<{type}> {functionNameUpper}QueryAsync({messageType} {messageVariableName}, BlockParameter blockParameter = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}return ContractHandler.QueryAsync<{messageType}, {type}>({messageVariableName}, blockParameter);
{SpaceUtils.TwoTabs}}}";


                var returnWithoutInputParam =
                    $@"{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}public Task<{type}> {functionNameUpper}QueryAsync(BlockParameter blockParameter = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}return ContractHandler.QueryAsync<{messageType}, {type}>(null, blockParameter);
{SpaceUtils.TwoTabs}}}";

                if (functionABIModel.HasNoInputParameters())
                {
                    return returnWithInputParam + returnWithoutInputParam;
                }

                return returnWithInputParam;
            }

            if(functionABIModel.IsTransaction())
            { 
                var transactionRequestWithInput = 
                    $@"{SpaceUtils.TwoTabs}public Task<string> {functionNameUpper}RequestAsync({messageType} {messageVariableName})
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs} return ContractHandler.SendRequestAsync({messageVariableName});
{SpaceUtils.TwoTabs}}}";

                var transactionRequestWithoutInput =
                    $@"{SpaceUtils.TwoTabs}public Task<string> {functionNameUpper}RequestAsync()
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs} return ContractHandler.SendRequestAsync<{messageType}>();
{SpaceUtils.TwoTabs}}}";

                var transactionRequestAndReceiptWithInput =
                    $@"{SpaceUtils.TwoTabs}public Task<TransactionReceipt> {functionNameUpper}RequestAndWaitForReceiptAsync({messageType} {messageVariableName}, CancellationTokenSource cancellationToken = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs} return ContractHandler.SendRequestAndWaitForReceiptAsync({messageVariableName}, cancellationToken);
{SpaceUtils.TwoTabs}}}";

                var transactionRequestAndReceiptWithoutInput =
                    $@"{SpaceUtils.TwoTabs}public Task<TransactionReceipt> {functionNameUpper}RequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs} return ContractHandler.SendRequestAndWaitForReceiptAsync<{messageType}>(null, cancellationToken);
{SpaceUtils.TwoTabs}}}";

                if (functionABIModel.HasNoInputParameters())
                {
                    return transactionRequestWithInput + Environment.NewLine + transactionRequestWithoutInput +
                           Environment.NewLine +
                           transactionRequestAndReceiptWithInput +
                           Environment.NewLine +
                           transactionRequestAndReceiptWithoutInput;
                }

                return transactionRequestWithInput + Environment.NewLine + transactionRequestAndReceiptWithInput;
            }

            return null;
        }
    }
}