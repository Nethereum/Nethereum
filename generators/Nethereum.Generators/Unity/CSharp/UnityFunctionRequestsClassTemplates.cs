using System;
using System.Linq;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;
using Nethereum.Generators.Service;

namespace Nethereum.Generators.Unity.CSharp
{

    public class UnityFunctionRequestsCsharpTemplates : ClassTemplateBase<UnityRequestsModel>
    {
        private readonly UnityRequestsModel _model;
        private CommonGenerators _commonGenerators;
        private ITypeConvertor _typeConvertor;
        private ParameterABIFunctionDTOCSharpTemplate _parameterAbiFunctionDtocSharpTemplate;

        public UnityFunctionRequestsCsharpTemplates(UnityRequestsModel model) : base(model)
        {
            _model = model;
            _typeConvertor = new ABITypeToCSharpType();
            _commonGenerators = new CommonGenerators();
            _parameterAbiFunctionDtocSharpTemplate = new ParameterABIFunctionDTOCSharpTemplate();
            ClassFileTemplate = new CSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            var functions = _model.ContractABI.Functions;
            return string.Join(GenerateLineBreak(), functions.Select(GenerateSingleClass));
        }

        public string GenerateSingleClass(FunctionABI functionABI)
        {
            var functionCQSMessageModel = new FunctionCQSMessageModel(functionABI, _model.CQSNamespace, null);
            var functionOutputDTOModel = new FunctionOutputDTOModel(functionABI, _model.FunctionOutputNamespace, null);
            var functionABIModel = new FunctionABIModel(functionABI, _typeConvertor, CodeGenLanguage.CSharp);

            var messageType = functionCQSMessageModel.GetTypeName();
            var messageVariableName = functionCQSMessageModel.GetVariableName();
            var functionNameUpper = _commonGenerators.GenerateClassName(functionABI.GetFunctionTypeNameBasedOnOverloads());

            if (!functionABIModel.IsTransaction())
            {

                var functionOutputDTOType = functionOutputDTOModel.GetTypeName();
                var queryMethod = string.Empty;
                if (functionABIModel.HasNoInputParameters())
                {
                    queryMethod =
$@"{SpaceUtils.Two___Tabs}public IEnumerator Query(BlockParameter blockParameter = null)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}var {messageVariableName} = new {messageType}();
{SpaceUtils.Three____Tabs}yield return Query({messageVariableName}, blockParameter);
{SpaceUtils.Two___Tabs}}}";
                }
                else
                {
                    queryMethod =
$@"{SpaceUtils.Two___Tabs}public IEnumerator Query({_parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, BlockParameter blockParameter = null)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}var {messageVariableName} = new {messageType}();
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.Four_____Tabs)}
{SpaceUtils.Three____Tabs}yield return Query({messageVariableName}, blockParameter);
{SpaceUtils.Two___Tabs}}}";
                }
                var classQuery =
    $@"{SpaceUtils.One__Tab}public partial class {functionNameUpper}QueryRequest : ContractFunctionQueryRequest<{messageType}, {functionOutputDTOType}>
{SpaceUtils.One__Tab}{{

{SpaceUtils.Two___Tabs}public {functionNameUpper}QueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Two___Tabs}}}

{SpaceUtils.Two___Tabs}public {functionNameUpper}QueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Two___Tabs}}}

{queryMethod}

{SpaceUtils.One__Tab}}}";
                return classQuery;
            }

            if (functionABIModel.IsTransaction())
            {
                var functionOutputDTOType = functionOutputDTOModel.GetTypeName();
                var txnMethod = string.Empty;
                if (functionABIModel.HasNoInputParameters())
                {
                    txnMethod =
$@"{SpaceUtils.Two___Tabs}public IEnumerator SignAndSendTransaction(BlockParameter blockParameter = null)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}var {messageVariableName} = new {messageType}();
{SpaceUtils.Three____Tabs}yield return SignAndSendTransaction({messageVariableName});
{SpaceUtils.Two___Tabs}}}";
                }
                else
                {
                    txnMethod =
$@"{SpaceUtils.Two___Tabs}public IEnumerator SignAndSendTransaction({_parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, BlockParameter blockParameter = null)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}var {messageVariableName} = new {messageType}();
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.Four_____Tabs)}
{SpaceUtils.Three____Tabs}yield return SignAndSendTransaction({messageVariableName});
{SpaceUtils.Two___Tabs}}}";
                }
                var classTxn =
    $@"{SpaceUtils.One__Tab}public partial class {functionNameUpper}TransactionRequest : ContractFunctionTransactionRequest<{messageType}>
{SpaceUtils.One__Tab}{{

{SpaceUtils.Two___Tabs}public {functionNameUpper}TransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Two___Tabs}}}

{SpaceUtils.Two___Tabs}public {functionNameUpper}TransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Two___Tabs}}}

{txnMethod}

{SpaceUtils.One__Tab}}}";
                return classTxn;
            }

            return null;
        }

        private string GenerateLineBreak()
        {
            return Environment.NewLine + Environment.NewLine;
        }


    }
}