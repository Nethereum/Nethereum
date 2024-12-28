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
            var allFunctions = functions.Select(GenerateMethod).ToList();
            allFunctions.Add(GenerateGetFunctionTypes());
            allFunctions.Add(GenerateGetEventTypes());
            allFunctions.Add(GenerateGetErrorTypes());
            return string.Join(GenerateLineBreak(), allFunctions);
        }

        public string GenerateGetFunctionTypes()
        {
            var functions = _model.ContractABI.Functions;
            var funtionTypesMethod =
$@"{SpaceUtils.Two___Tabs}public override List<Type> GetAllFunctionTypes()
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}return new List<Type>
{SpaceUtils.Three____Tabs}{{
{string.Join($",{Environment.NewLine}", functions.Select(x => $"{SpaceUtils.Four_____Tabs}typeof({new FunctionCQSMessageModel(x, _model.CQSNamespace).GetTypeName()})"))}
{SpaceUtils.Three____Tabs}}};
{SpaceUtils.Two___Tabs}}}";
            return funtionTypesMethod;
        }

        public string GenerateGetEventTypes()
        {
            var events = _model.ContractABI.Events;
            var eventTypesMethod =
$@"{SpaceUtils.Two___Tabs}public override List<Type> GetAllEventTypes()
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}return new List<Type>
{SpaceUtils.Three____Tabs}{{
{string.Join($",{Environment.NewLine}", events.Select(x => $"{SpaceUtils.Four_____Tabs}typeof({new EventDTOModel(x, _model.CQSNamespace).GetTypeName()})"))}
{SpaceUtils.Three____Tabs}}};
{SpaceUtils.Two___Tabs}}}";
            return eventTypesMethod;
        }

        public string GenerateGetErrorTypes()
        {
            var errors = _model.ContractABI.Errors;
            var errorsMethod =
$@"{SpaceUtils.Two___Tabs}public override List<Type> GetAllErrorTypes()
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}return new List<Type>
{SpaceUtils.Three____Tabs}{{
{string.Join($",{Environment.NewLine}", errors.Select(x => $"{SpaceUtils.Four_____Tabs}typeof({new ErrorDTOModel(x, _model.CQSNamespace).GetTypeName()})"))}
{SpaceUtils.Three____Tabs}}};
{SpaceUtils.Two___Tabs}}}";
            return errorsMethod;
        }

        public string GenerateMethod(FunctionABI functionABI)
        {
            var functionCQSMessageModel = new FunctionCQSMessageModel(functionABI, _model.CQSNamespace);
            var functionOutputDTOModel = new FunctionOutputDTOModel(functionABI, _model.FunctionOutputNamespace);
            var functionABIModel = new FunctionABIModel(functionABI, _typeConvertor, CodeGenLanguage.CSharp);

            var messageType = functionCQSMessageModel.GetTypeName();
            var messageVariableName = functionCQSMessageModel.GetVariableName();
            var functionNameUpper = _commonGenerators.GenerateClassName(functionABI.Name);

            if (functionABIModel.IsMultipleOutput() && !functionABIModel.IsTransaction())
            {
                var functionOutputDTOType = functionOutputDTOModel.GetTypeName();

                var returnWithInputParam =
$@"{SpaceUtils.Two___Tabs}public virtual Task<{functionOutputDTOType}> {functionNameUpper}QueryAsync({messageType} {messageVariableName}, BlockParameter blockParameter = null)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}return ContractHandler.QueryDeserializingToObjectAsync<{messageType}, {functionOutputDTOType}>({messageVariableName}, blockParameter);
{SpaceUtils.Two___Tabs}}}";

                var returnWithoutInputParam =
$@"{SpaceUtils.Two___Tabs}public virtual Task<{functionOutputDTOType}> {functionNameUpper}QueryAsync(BlockParameter blockParameter = null)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}return ContractHandler.QueryDeserializingToObjectAsync<{messageType}, {functionOutputDTOType}>(null, blockParameter);
{SpaceUtils.Two___Tabs}}}";

                var returnWithSimpleParams =
$@"{SpaceUtils.Two___Tabs}public virtual Task<{functionOutputDTOType}> {functionNameUpper}QueryAsync({_parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, BlockParameter blockParameter = null)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}var {messageVariableName} = new {messageType}();
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.Four_____Tabs)}
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}return ContractHandler.QueryDeserializingToObjectAsync<{messageType}, {functionOutputDTOType}>({messageVariableName}, blockParameter);
{SpaceUtils.Two___Tabs}}}";

                if (functionABIModel.HasNoInputParameters())
                {
                    return returnWithInputParam + GenerateLineBreak() + returnWithoutInputParam;
                }
                else
                {
                    return returnWithInputParam + GenerateLineBreak() + returnWithSimpleParams;
                }
            }

            if (functionABIModel.IsSingleOutput() && !functionABIModel.IsTransaction())
                if (functionABI.OutputParameters != null && functionABI.OutputParameters.Length == 1 &&
                    functionABI.Constant)
                {
                    var type = functionABIModel.GetSingleOutputReturnType();

                    var returnWithInputParam =
                        $@"{SpaceUtils.Two___Tabs}public Task<{type}> {functionNameUpper}QueryAsync({messageType} {messageVariableName}, BlockParameter blockParameter = null)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}return ContractHandler.QueryAsync<{messageType}, {type}>({messageVariableName}, blockParameter);
{SpaceUtils.Two___Tabs}}}";


                    var returnWithoutInputParam =
                        $@"{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}public virtual Task<{type}> {functionNameUpper}QueryAsync(BlockParameter blockParameter = null)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}return ContractHandler.QueryAsync<{messageType}, {type}>(null, blockParameter);
{SpaceUtils.Two___Tabs}}}";

                    var returnWithSimpleParams =
                        $@"{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}public virtual Task<{type}> {functionNameUpper}QueryAsync({_parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, BlockParameter blockParameter = null)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}var {messageVariableName} = new {messageType}();
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.Four_____Tabs)}
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}return ContractHandler.QueryAsync<{messageType}, {type}>({messageVariableName}, blockParameter);
{SpaceUtils.Two___Tabs}}}";

                    if (functionABIModel.HasNoInputParameters())
                    {
                        return returnWithInputParam + GenerateLineBreak() + returnWithoutInputParam;
                    }
                    else
                    {
                        return returnWithInputParam + GenerateLineBreak() + returnWithSimpleParams;
                    }
                }

            if (functionABIModel.IsTransaction())
            {
                var transactionRequestWithInput =
                    $@"{SpaceUtils.Two___Tabs}public virtual Task<string> {functionNameUpper}RequestAsync({messageType} {messageVariableName})
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs} return ContractHandler.SendRequestAsync({messageVariableName});
{SpaceUtils.Two___Tabs}}}";

                var transactionRequestWithoutInput =
                    $@"{SpaceUtils.Two___Tabs}public virtual Task<string> {functionNameUpper}RequestAsync()
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs} return ContractHandler.SendRequestAsync<{messageType}>();
{SpaceUtils.Two___Tabs}}}";


                var transactionRequestWithSimpleParams =
                    $@"{SpaceUtils.Two___Tabs}public virtual Task<string> {functionNameUpper}RequestAsync({_parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)})
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}var {messageVariableName} = new {messageType}();
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.Four_____Tabs)}
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs} return ContractHandler.SendRequestAsync({messageVariableName});
{SpaceUtils.Two___Tabs}}}";


                var transactionRequestAndReceiptWithInput =
                    $@"{SpaceUtils.Two___Tabs}public virtual Task<TransactionReceipt> {functionNameUpper}RequestAndWaitForReceiptAsync({messageType} {messageVariableName}, CancellationTokenSource cancellationToken = null)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs} return ContractHandler.SendRequestAndWaitForReceiptAsync({messageVariableName}, cancellationToken);
{SpaceUtils.Two___Tabs}}}";

                var transactionRequestAndReceiptWithoutInput =
                    $@"{SpaceUtils.Two___Tabs}public virtual Task<TransactionReceipt> {functionNameUpper}RequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs} return ContractHandler.SendRequestAndWaitForReceiptAsync<{messageType}>(null, cancellationToken);
{SpaceUtils.Two___Tabs}}}";

                var transactionRequestAndReceiptWithSimpleParams =
                    $@"{SpaceUtils.Two___Tabs}public virtual Task<TransactionReceipt> {functionNameUpper}RequestAndWaitForReceiptAsync({_parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, CancellationTokenSource cancellationToken = null)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}var {messageVariableName} = new {messageType}();
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.Four_____Tabs)}
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs} return ContractHandler.SendRequestAndWaitForReceiptAsync({messageVariableName}, cancellationToken);
{SpaceUtils.Two___Tabs}}}";

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