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
        private ParameterABIFunctionDTOCSharpTemplate _parameterAbiFunctionDtocSharpTemplate;

        public FunctionServiceMethodCSharpTemplate(ServiceModel model)
        {
            _model = model;
            _typeConvertor = new ABITypeToCSharpType();
            _commonGenerators = new CommonGenerators();
            _parameterAbiFunctionDtocSharpTemplate = new ParameterABIFunctionDTOCSharpTemplate();
        }

        public string GenerateMethods()
        {
            var functions = _model.ContractABI.Functions;
            return string.Join(GenerateLineBreak(), functions.Select(GenerateMethod));
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
$@"{SpaceUtils.TwoTabs}public Task<{functionOutputDTOType}> {functionNameUpper}QueryAsync(BlockParameter blockParameter = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}return ContractHandler.QueryDeserializingToObjectAsync<{messageType}, {functionOutputDTOType}>(null, blockParameter);
{SpaceUtils.TwoTabs}}}";

                var returnWithSimpleParams =
$@"{SpaceUtils.TwoTabs}public Task<{functionOutputDTOType}> {functionNameUpper}QueryAsync({_parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, BlockParameter blockParameter = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}var {messageVariableName} = new {messageType}();
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.FourTabs)}
{SpaceUtils.ThreeTabs}
{SpaceUtils.ThreeTabs}return ContractHandler.QueryDeserializingToObjectAsync<{messageType}, {functionOutputDTOType}>({messageVariableName}, blockParameter);
{SpaceUtils.TwoTabs}}}";

                if (functionABIModel.HasNoInputParameters())
                {
                    return returnWithInputParam + GenerateLineBreak() + returnWithoutInputParam;
                }
                else
                {
                    return returnWithInputParam + GenerateLineBreak() + returnWithSimpleParams;
                }
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

                var returnWithSimpleParams =
                    $@"{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}public Task<{type}> {functionNameUpper}QueryAsync({_parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, BlockParameter blockParameter = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}var {messageVariableName} = new {messageType}();
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.FourTabs)}
{SpaceUtils.ThreeTabs}
{SpaceUtils.ThreeTabs}return ContractHandler.QueryAsync<{messageType}, {type}>({messageVariableName}, blockParameter);
{SpaceUtils.TwoTabs}}}";

                if (functionABIModel.HasNoInputParameters())
                {
                    return returnWithInputParam + GenerateLineBreak() + returnWithoutInputParam;
                }
                else
                {
                    return returnWithInputParam + GenerateLineBreak() + returnWithSimpleParams;
                }
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


                var transactionRequestWithSimpleParams =
                    $@"{SpaceUtils.TwoTabs}public Task<string> {functionNameUpper}RequestAsync({_parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)})
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}var {messageVariableName} = new {messageType}();
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.FourTabs)}
{SpaceUtils.ThreeTabs}
{SpaceUtils.ThreeTabs} return ContractHandler.SendRequestAsync({messageVariableName});
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

                var transactionRequestAndReceiptWithSimpleParams =
                    $@"{SpaceUtils.TwoTabs}public Task<TransactionReceipt> {functionNameUpper}RequestAndWaitForReceiptAsync({_parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, CancellationTokenSource cancellationToken = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}var {messageVariableName} = new {messageType}();
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.FourTabs)}
{SpaceUtils.ThreeTabs}
{SpaceUtils.ThreeTabs} return ContractHandler.SendRequestAndWaitForReceiptAsync({messageVariableName}, cancellationToken);
{SpaceUtils.TwoTabs}}}";

                if (functionABIModel.HasNoInputParameters())
                {
                    return transactionRequestWithInput + GenerateLineBreak() 
                           + transactionRequestWithoutInput +
                           GenerateLineBreak() +
                           transactionRequestAndReceiptWithInput +
                           GenerateLineBreak() +
                           transactionRequestAndReceiptWithoutInput;
                }

                return transactionRequestWithInput + GenerateLineBreak() + transactionRequestAndReceiptWithInput +
                       GenerateLineBreak() +
                       transactionRequestWithSimpleParams +
                       GenerateLineBreak() +
                       transactionRequestAndReceiptWithSimpleParams;
            }

            return null;
        }

        private string GenerateLineBreak()
        {
            return Environment.NewLine + Environment.NewLine;
        }
    }
}