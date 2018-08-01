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
                    $@"{SpaceUtils.TwoTabs}Public Function {functionNameUpper}QueryAsync(ByVal {messageVariableName} As {messageType}, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of {functionOutputDTOType})
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Return ContractHandler.QueryDeserializingToObjectAsync(Of {messageType}, {functionOutputDTOType})({messageVariableName}, blockParameter)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";


                var returnWithoutInputParam =
                    $@"{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}Public Function {functionNameUpper}QueryAsync(ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of {functionOutputDTOType})
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}return ContractHandler.QueryDeserializingToObjectAsync(Of {messageType}, {functionOutputDTOType})(Nothing, blockParameter)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function
";

                var returnWithSimpleParams =
                    $@"{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}Public Function {functionNameUpper}QueryAsync({_parameterAbiFunctionDtoVbTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of {functionOutputDTOType})
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Dim {messageVariableName} = New {messageType}()
{_parameterAbiFunctionDtoVbTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.FourTabs)}
{SpaceUtils.ThreeTabs}
{SpaceUtils.ThreeTabs}Return ContractHandler.QueryDeserializingToObjectAsync(Of {messageType}, {functionOutputDTOType})({messageVariableName}, blockParameter)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";

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
                        $@"{SpaceUtils.TwoTabs}Public Function {functionNameUpper}QueryAsync(ByVal {messageVariableName} As {messageType}, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of {type})
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Return ContractHandler.QueryAsync(Of {messageType}, {type})({messageVariableName}, blockParameter)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";

                    var returnWithoutInputParam =
                        $@"{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}Public Function {functionNameUpper}QueryAsync(ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of {type})
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}return ContractHandler.QueryAsync(Of {messageType}, {type})(Nothing, blockParameter)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function
";

                    var returnWithSimpleParams =
                        $@"{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}Public Function {functionNameUpper}QueryAsync({_parameterAbiFunctionDtoVbTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of {type})
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Dim {messageVariableName} = New {messageType}()
{_parameterAbiFunctionDtoVbTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.FourTabs)}
{SpaceUtils.ThreeTabs}
{SpaceUtils.ThreeTabs}Return ContractHandler.QueryAsync(Of {messageType}, {type})({messageVariableName}, blockParameter)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";

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
                    $@"{SpaceUtils.TwoTabs}Public Function {functionNameUpper}RequestAsync(ByVal {messageVariableName} As {messageType}) As Task(Of String)
{SpaceUtils.TwoTabs}            
{SpaceUtils.ThreeTabs}Return ContractHandler.SendRequestAsync(Of {messageType})({messageVariableName})
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";

                var transactionRequestWithoutInput =
                    $@"{SpaceUtils.TwoTabs}Public Function {functionNameUpper}RequestAsync() As Task(Of String)
{SpaceUtils.TwoTabs}            
{SpaceUtils.ThreeTabs}Return ContractHandler.SendRequestAsync(Of {messageType})
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";

                var transactionRequestWithSimpleParams =
                $@"{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}Public Function {functionNameUpper}RequestAsync({_parameterAbiFunctionDtoVbTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}) As Task(Of String)
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Dim {messageVariableName} = New {messageType}()
{_parameterAbiFunctionDtoVbTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.FourTabs)}
{SpaceUtils.ThreeTabs}
{SpaceUtils.ThreeTabs}Return ContractHandler.SendRequestAsync(Of {messageType})({messageVariableName})
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";

                var transactionRequestAndReceiptWithInput =
                    $@"{SpaceUtils.TwoTabs}Public Function {functionNameUpper}RequestAndWaitForReceiptAsync(ByVal {messageVariableName} As {messageType}, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of {messageType})({messageVariableName}, cancellationToken)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";

                var transactionRequestAndReceiptWithoutInput =
                    $@"{SpaceUtils.TwoTabs}Public Function {functionNameUpper}RequestAndWaitForReceiptAsync(ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of {messageType})(Nothing, cancellationToken)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";


                var transactionRequestAndReceiptWithSimpleParams =
                    $@"{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}Public Function {functionNameUpper}RequestAndWaitForReceiptAsync({_parameterAbiFunctionDtoVbTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Dim {messageVariableName} = New {messageType}()
{_parameterAbiFunctionDtoVbTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.FourTabs)}
{SpaceUtils.ThreeTabs}
{SpaceUtils.ThreeTabs}Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of {messageType})({messageVariableName}, cancellationToken)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";


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