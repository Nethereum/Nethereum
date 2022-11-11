using System;
using System.Linq;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Console.CSharp
{
    public class FunctionMockupMethodCSharpTemplate
    {
        private readonly ContractABI _contractAbi;
        private CommonGenerators _commonGenerators;
        private ITypeConvertor _typeConvertor;
        private ParameterABIFunctionDTOCSharpTemplate _parameterAbiFunctionDtocSharpTemplate;

        public FunctionMockupMethodCSharpTemplate(ContractABI contractAbi)
        {
            _contractAbi = contractAbi;
            _typeConvertor = new ABITypeToCSharpType();
            _commonGenerators = new CommonGenerators();
            _parameterAbiFunctionDtocSharpTemplate = new ParameterABIFunctionDTOCSharpTemplate();
        }

        public string GenerateMethods()
        {
            var functions = _contractAbi.Functions;
            return string.Join(GenerateLineBreak(), functions.Select(GetFunctionDisplay));
        }

        private string GetFunctionDisplay(FunctionABI functionAbi)
        {
            return $@"
{SpaceUtils.ThreeTabs}/** Function: {functionAbi.Name}**/
{SpaceUtils.ThreeTabs}/*
{GenerateMethod(functionAbi)}
{SpaceUtils.ThreeTabs}*/";
        }

        public string GenerateMethod(FunctionABI functionABI)
        {
            var functionCQSMessageModel = new FunctionCQSMessageModel(functionABI, null);
            var functionOutputDTOModel = new FunctionOutputDTOModel(functionABI, null);
            var functionABIModel = new FunctionABIModel(functionABI, _typeConvertor, CodeGenLanguage.CSharp);

            var messageType = functionCQSMessageModel.GetTypeName();
            var messageVariableName = functionCQSMessageModel.GetVariableName();
            
            var functionNameUpper = _commonGenerators.GenerateClassName(functionABI.Name);

            if (functionABIModel.IsMultipleOutput() && !functionABIModel.IsTransaction())
            {
                var functionOutputDTOType = functionOutputDTOModel.GetTypeName();
                var functionOutputVariableName = functionOutputDTOModel.GetVariableName();

                var returnWithInputParam =
                    $@"{SpaceUtils.ThreeTabs}var {messageVariableName} = new {messageType}(); 
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.ThreeTabs)}
{SpaceUtils.ThreeTabs}var {functionOutputVariableName} = await contractHandler.QueryDeserializingToObjectAsync<{messageType}, {functionOutputDTOType}>({messageVariableName});";

                var returnWithoutInputParam =
                    $@"{SpaceUtils.ThreeTabs}var {functionOutputVariableName} = await contractHandler.QueryDeserializingToObjectAsync<{messageType}, {functionOutputDTOType}>();";

                if (functionABIModel.HasNoInputParameters())
                {
                    return returnWithoutInputParam;
                }
                else
                {
                    return returnWithInputParam;
                }
            }

            if(functionABIModel.IsSingleOutput() && !functionABIModel.IsTransaction())
            if (functionABI.OutputParameters != null && functionABI.OutputParameters.Length == 1 &&
                functionABI.Constant)
            {
                var type = functionABIModel.GetSingleOutputReturnType();
                var returnName = functionCQSMessageModel.GetVariableName() + "Return";
                var returnWithInputParam = 
                    $@"{SpaceUtils.ThreeTabs}var {messageVariableName} = new {messageType}();
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.ThreeTabs)}
{SpaceUtils.ThreeTabs}var {returnName} = await contractHandler.QueryAsync<{messageType}, {type}>({messageVariableName});";


                var returnWithoutInputParam =
                    $@"{SpaceUtils.ThreeTabs}var {returnName} = await contractHandler.QueryAsync<{messageType}, {type}>();";


                if (functionABIModel.HasNoInputParameters())
                {
                    return returnWithoutInputParam;
                }
                else
                {
                    return returnWithInputParam;
                }
            }

            if(functionABIModel.IsTransaction())
            {

                var returnName = functionCQSMessageModel.GetVariableName() + "TxnReceipt";
                var transactionRequestAndReceiptWithoutInput = 
                    $@"{SpaceUtils.ThreeTabs}var {returnName} = await contractHandler.SendRequestAndWaitForReceiptAsync<{messageType}>();";

                var transactionRequestAndReceiptWithSimpleParams = 
                    $@"{SpaceUtils.ThreeTabs}var {messageVariableName} = new {messageType}();
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.ThreeTabs)}
{SpaceUtils.ThreeTabs}var {returnName} = await contractHandler.SendRequestAndWaitForReceiptAsync({messageVariableName});";

                if (functionABIModel.HasNoInputParameters())
                {
                    return transactionRequestAndReceiptWithoutInput;
                }

                return transactionRequestAndReceiptWithSimpleParams;
            }

            return null;
        }

        private string GenerateLineBreak()
        {
            return Environment.NewLine + Environment.NewLine;
        }
    }
}