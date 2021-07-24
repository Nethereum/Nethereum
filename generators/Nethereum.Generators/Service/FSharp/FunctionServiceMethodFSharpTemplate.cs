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

            var functionCQSMessageModel = new FunctionCQSMessageModel(functionABI, _model.CQSNamespace);
            var functionOutputDTOModel = new FunctionOutputDTOModel(functionABI, _model.FunctionOutputNamespace);
            var functionABIModel = new FunctionABIModel(functionABI, _typeConvertor, CodeGenLanguage.FSharp);

            var messageType = functionCQSMessageModel.GetTypeName();
            var messageVariableName = functionCQSMessageModel.GetVariableName();
            var functionNameUpper = _commonGenerators.GenerateClassName(functionABI.Name);

            if (functionABIModel.IsMultipleOutput() && !functionABIModel.IsTransaction())
            {

                var functionOutputDTOType = functionOutputDTOModel.GetTypeName();

                return
                    $@"{SpaceUtils.TwoTabs}member this.{functionNameUpper}QueryAsync({messageVariableName}: {messageType}, ?blockParameter: BlockParameter): Task<{functionOutputDTOType}> =
{SpaceUtils.ThreeTabs}let blockParameterVal = defaultArg blockParameter null
{SpaceUtils.ThreeTabs}this.ContractHandler.QueryDeserializingToObjectAsync<{messageType}, {functionOutputDTOType}>({messageVariableName}, blockParameterVal)
{SpaceUtils.ThreeTabs}";
            }

            if (functionABIModel.IsSingleOutput() && !functionABIModel.IsTransaction())
                if (functionABI.OutputParameters != null && functionABI.OutputParameters.Length == 1 &&
                    functionABI.Constant)
                {
                    var type = functionABIModel.GetSingleOutputReturnType();

                    return
                        $@"{SpaceUtils.TwoTabs}member this.{functionNameUpper}QueryAsync({messageVariableName}: {messageType}, ?blockParameter: BlockParameter): Task<{type}> =
{SpaceUtils.ThreeTabs}let blockParameterVal = defaultArg blockParameter null
{SpaceUtils.ThreeTabs}this.ContractHandler.QueryAsync<{messageType}, {type}>({messageVariableName}, blockParameterVal)
{SpaceUtils.ThreeTabs}";
                }

            if (functionABIModel.IsTransaction())
            {
                var transactionRequest =
                    $@"{SpaceUtils.TwoTabs}member this.{functionNameUpper}RequestAsync({messageVariableName}: {messageType}): Task<string> =
{SpaceUtils.ThreeTabs}this.ContractHandler.SendRequestAsync({messageVariableName});
{SpaceUtils.TwoTabs}";

                var transactionRequestAndReceipt =
                    $@"{SpaceUtils.TwoTabs}member this.{functionNameUpper}RequestAndWaitForReceiptAsync({messageVariableName}: {messageType}, ?cancellationTokenSource : CancellationTokenSource): Task<TransactionReceipt> =
{SpaceUtils.ThreeTabs}let cancellationTokenSourceVal = defaultArg cancellationTokenSource null
{SpaceUtils.ThreeTabs}this.ContractHandler.SendRequestAndWaitForReceiptAsync({messageVariableName}, cancellationTokenSourceVal);
{SpaceUtils.TwoTabs}";

                return transactionRequest + Environment.NewLine + transactionRequestAndReceipt;
            }

            return null;
        }
    }
}