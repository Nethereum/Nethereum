using System;
using System.Linq;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Service
{
    public class FunctionServiceMethodFSharpTemplate
    {
        private readonly ServiceModel _model;
        private CommonGenerators _commonGenerators;
        private ITypeConvertor _typeConvertor;

        public FunctionServiceMethodFSharpTemplate(ServiceModel model)
        {
            _model = model;
            _typeConvertor = new ABITypeToFSharpType();
            _commonGenerators = new CommonGenerators();
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
            var functionABIModel = new FunctionABIModel(functionABI, _typeConvertor, CodeGenLanguage.FSharp);

            var messageType = functionCQSMessageModel.GetTypeName();
            var messageVariableName = functionCQSMessageModel.GetVariableName();
            var functionNameUpper = _commonGenerators.GenerateClassName(functionABI.Name);

            if (functionABIModel.IsMultipleOutput() && !functionABIModel.IsTransaction())
            {

                var functionOutputDTOType = functionOutputDTOModel.GetTypeName();

                return
                    $@"{SpaceUtils.Two___Tabs}member this.{functionNameUpper}QueryAsync({messageVariableName}: {messageType}, ?blockParameter: BlockParameter): Task<{functionOutputDTOType}> =
{SpaceUtils.Three____Tabs}let blockParameterVal = defaultArg blockParameter null
{SpaceUtils.Three____Tabs}this.ContractHandler.QueryDeserializingToObjectAsync<{messageType}, {functionOutputDTOType}>({messageVariableName}, blockParameterVal)
{SpaceUtils.Three____Tabs}";
            }

            if (functionABIModel.IsSingleOutput() && !functionABIModel.IsTransaction())
                if (functionABI.OutputParameters != null && functionABI.OutputParameters.Length == 1 &&
                    functionABI.Constant)
                {
                    var type = functionABIModel.GetSingleOutputReturnType();

                    return
                        $@"{SpaceUtils.Two___Tabs}member this.{functionNameUpper}QueryAsync({messageVariableName}: {messageType}, ?blockParameter: BlockParameter): Task<{type}> =
{SpaceUtils.Three____Tabs}let blockParameterVal = defaultArg blockParameter null
{SpaceUtils.Three____Tabs}this.ContractHandler.QueryAsync<{messageType}, {type}>({messageVariableName}, blockParameterVal)
{SpaceUtils.Three____Tabs}";
                }

            if (functionABIModel.IsTransaction())
            {
                var transactionRequest =
                    $@"{SpaceUtils.Two___Tabs}member this.{functionNameUpper}RequestAsync({messageVariableName}: {messageType}): Task<string> =
{SpaceUtils.Three____Tabs}this.ContractHandler.SendRequestAsync({messageVariableName});
{SpaceUtils.Two___Tabs}";

                var transactionRequestAndReceipt =
                    $@"{SpaceUtils.Two___Tabs}member this.{functionNameUpper}RequestAndWaitForReceiptAsync({messageVariableName}: {messageType}, ?cancellationTokenSource : CancellationTokenSource): Task<TransactionReceipt> =
{SpaceUtils.Three____Tabs}let cancellationTokenSourceVal = defaultArg cancellationTokenSource null
{SpaceUtils.Three____Tabs}this.ContractHandler.SendRequestAndWaitForReceiptAsync({messageVariableName}, cancellationTokenSourceVal);
{SpaceUtils.Two___Tabs}";

                return transactionRequest + Environment.NewLine + transactionRequestAndReceipt;
            }

            return null;
        }
    }
}