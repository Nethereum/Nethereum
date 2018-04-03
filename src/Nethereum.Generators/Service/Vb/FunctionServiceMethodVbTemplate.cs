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

        public FunctionServiceMethodVbTemplate(ServiceModel model)
        {
            _model = model;
            _typeConvertor = new ABITypeToVBType();
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
                return
                    $@"{SpaceUtils.TwoTabs}Public Function {functionNameUpper}QueryAsync(ByVal {messageVariableName} As {messageType}, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of {functionOutputDTOType})
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Return ContractHandler.QueryDeserializingToObjectAsync(Of {messageType}, {functionOutputDTOType})({messageVariableName}, blockParameter)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";
            }

            if (functionABIModel.IsSingleOutput() && !functionABIModel.IsTransaction())
                if (functionABI.OutputParameters != null && functionABI.OutputParameters.Length == 1 &&
                    functionABI.Constant)
                {
                    var type = functionABIModel.GetSingleOutputReturnType();

                    return
                        $@"{SpaceUtils.TwoTabs}Public Function {functionNameUpper}QueryAsync(ByVal {messageVariableName} As {messageType}, ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of {type})
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Return ContractHandler.QueryAsync(Of {messageType}, {type})({messageVariableName}, blockParameter)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";
                }

            if (functionABIModel.IsTransaction())
            {
                var transactionRequest =
                    $@"{SpaceUtils.TwoTabs}Public Function {functionNameUpper}RequestAsync(ByVal {messageVariableName} As {messageType}) As Task(Of String)
{SpaceUtils.TwoTabs}            
{SpaceUtils.ThreeTabs}Return ContractHandler.SendRequestAsync({messageVariableName})
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";

                var transactionRequestAndReceipt =
                    $@"{SpaceUtils.TwoTabs}Public Function {functionNameUpper}RequestAndWaitForReceiptAsync(ByVal {messageVariableName} As {messageType}, ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)
{SpaceUtils.TwoTabs}
{SpaceUtils.ThreeTabs}Return ContractHandler.SendRequestAndWaitForReceiptAsync({messageVariableName}, cancellationToken)
{SpaceUtils.TwoTabs}
{SpaceUtils.TwoTabs}End Function";

                return transactionRequest + Environment.NewLine + transactionRequestAndReceipt;
            }

            return null;
        }
    }
}