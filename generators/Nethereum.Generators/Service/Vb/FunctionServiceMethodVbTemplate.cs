using System;
using System.Linq;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Service
{
    public class FunctionServiceMethodVbTemplate
    {
        private readonly ServiceModel _model;
        private CommonGenerators _commonGenerators;
        private ITypeConvertor _typeConvertor;
        private ParameterABIFunctionDTOVbTemplate _parameterAbiFunctionDtoVbTemplate;

        public FunctionServiceMethodVbTemplate(ServiceModel model)
        {
            _model = model;
            _typeConvertor = new ABITypeToVBType();
            _commonGenerators = new CommonGenerators();
            _parameterAbiFunctionDtoVbTemplate = new ParameterABIFunctionDTOVbTemplate();
        }

        public string GenerateMethods()
        {
            var functions = _model.ContractABI.Functions;
            return string.Join(Environment.NewLine, functions.Select(GenerateMethod));
        }

        public string GenerateMethod(FunctionABI functionABI)
        {

            var functionCQSMessageModel = new FunctionCQSMessageModel(functionABI, _model.CQSNamespace, null);
            var functionOutputDTOModel = new FunctionOutputDTOModel(functionABI, _model.FunctionOutputNamespace, null);
            var functionABIModel = new FunctionABIModel(functionABI, _typeConvertor, CodeGenLanguage.Vb);

            var messageType = functionCQSMessageModel.GetTypeName();
            var messageVariableName = functionCQSMessageModel.GetVariableName();
            var functionNameUpper = _commonGenerators.GenerateClassName(functionABI.Name);

            if (functionABIModel.IsMultipleOutput() && !functionABIModel.IsTransaction())
            {

                var functionOutputDTOType = functionOutputDTOModel.GetTypeName();
                var returnWithInputParam =
                    $@"{SpaceUtils.Two___Tabs}Public Function {functionNameUpper}QueryAsync(ByVal {messageVariableName} As {messageType}, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of {functionOutputDTOType})
{SpaceUtils.Two___Tabs}
{SpaceUtils.Three____Tabs}Return ContractHandler.QueryDeserializingToObjectAsync(Of {messageType}, {functionOutputDTOType})({messageVariableName}, blockParameter)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}End Function";


                var returnWithoutInputParam =
                    $@"{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}Public Function {functionNameUpper}QueryAsync(ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of {functionOutputDTOType})
{SpaceUtils.Two___Tabs}
{SpaceUtils.Three____Tabs}return ContractHandler.QueryDeserializingToObjectAsync(Of {messageType}, {functionOutputDTOType})(Nothing, blockParameter)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}End Function
";

                var returnWithSimpleParams =
                    $@"{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}Public Function {functionNameUpper}QueryAsync({_parameterAbiFunctionDtoVbTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of {functionOutputDTOType})
{SpaceUtils.Two___Tabs}
{SpaceUtils.Three____Tabs}Dim {messageVariableName} = New {messageType}()
{_parameterAbiFunctionDtoVbTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.Four_____Tabs)}
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}Return ContractHandler.QueryDeserializingToObjectAsync(Of {messageType}, {functionOutputDTOType})({messageVariableName}, blockParameter)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}End Function";

                if (functionABIModel.HasNoInputParameters())
                {
                    return returnWithInputParam + GenerateLineBreak() + returnWithoutInputParam + GenerateLineBreak();
                }
                else
                {
                    return returnWithInputParam + GenerateLineBreak() + returnWithSimpleParams + GenerateLineBreak();
                }

            }

            if (functionABIModel.IsSingleOutput() && !functionABIModel.IsTransaction())
                if (functionABI.OutputParameters != null && functionABI.OutputParameters.Length == 1 &&
                    functionABI.Constant)
                {
                    var type = functionABIModel.GetSingleOutputReturnType();

                    var returnWithInputParam =
                        $@"{SpaceUtils.Two___Tabs}Public Function {functionNameUpper}QueryAsync(ByVal {messageVariableName} As {messageType}, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of {type})
{SpaceUtils.Two___Tabs}
{SpaceUtils.Three____Tabs}Return ContractHandler.QueryAsync(Of {messageType}, {type})({messageVariableName}, blockParameter)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}End Function";

                    var returnWithoutInputParam =
                        $@"{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}Public Function {functionNameUpper}QueryAsync(ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of {type})
{SpaceUtils.Two___Tabs}
{SpaceUtils.Three____Tabs}return ContractHandler.QueryAsync(Of {messageType}, {type})(Nothing, blockParameter)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}End Function
";

                    var returnWithSimpleParams =
                        $@"{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}Public Function {functionNameUpper}QueryAsync({_parameterAbiFunctionDtoVbTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of {type})
{SpaceUtils.Two___Tabs}
{SpaceUtils.Three____Tabs}Dim {messageVariableName} = New {messageType}()
{_parameterAbiFunctionDtoVbTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.Four_____Tabs)}
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}Return ContractHandler.QueryAsync(Of {messageType}, {type})({messageVariableName}, blockParameter)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}End Function";

                    if (functionABIModel.HasNoInputParameters())
                    {
                        return returnWithInputParam + GenerateLineBreak() + returnWithoutInputParam + GenerateLineBreak();
                    }
                    else
                    {
                        return returnWithInputParam + GenerateLineBreak() + returnWithSimpleParams + GenerateLineBreak();
                    }
                }

            if (functionABIModel.IsTransaction())
            {
                var transactionRequestWithInput =
                    $@"{SpaceUtils.Two___Tabs}Public Function {functionNameUpper}RequestAsync(ByVal {messageVariableName} As {messageType}) As Task(Of String)
{SpaceUtils.Two___Tabs}            
{SpaceUtils.Three____Tabs}Return ContractHandler.SendRequestAsync(Of {messageType})({messageVariableName})
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}End Function";

                var transactionRequestWithoutInput =
                    $@"{SpaceUtils.Two___Tabs}Public Function {functionNameUpper}RequestAsync() As Task(Of String)
{SpaceUtils.Two___Tabs}            
{SpaceUtils.Three____Tabs}Return ContractHandler.SendRequestAsync(Of {messageType})
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}End Function";

                var transactionRequestWithSimpleParams =
                $@"{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}Public Function {functionNameUpper}RequestAsync({_parameterAbiFunctionDtoVbTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}) As Task(Of String)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Three____Tabs}Dim {messageVariableName} = New {messageType}()
{_parameterAbiFunctionDtoVbTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.Four_____Tabs)}
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}Return ContractHandler.SendRequestAsync(Of {messageType})({messageVariableName})
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}End Function";

                var transactionRequestAndReceiptWithInput =
                    $@"{SpaceUtils.Two___Tabs}Public Function {functionNameUpper}RequestAndWaitForReceiptAsync(ByVal {messageVariableName} As {messageType}, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Three____Tabs}Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of {messageType})({messageVariableName}, cancellationToken)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}End Function";

                var transactionRequestAndReceiptWithoutInput =
                    $@"{SpaceUtils.Two___Tabs}Public Function {functionNameUpper}RequestAndWaitForReceiptAsync(ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Three____Tabs}Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of {messageType})(Nothing, cancellationToken)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}End Function";


                var transactionRequestAndReceiptWithSimpleParams =
                    $@"{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}Public Function {functionNameUpper}RequestAndWaitForReceiptAsync({_parameterAbiFunctionDtoVbTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Three____Tabs}Dim {messageVariableName} = New {messageType}()
{_parameterAbiFunctionDtoVbTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.Four_____Tabs)}
{SpaceUtils.Three____Tabs}
{SpaceUtils.Three____Tabs}Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of {messageType})({messageVariableName}, cancellationToken)
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}End Function";


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