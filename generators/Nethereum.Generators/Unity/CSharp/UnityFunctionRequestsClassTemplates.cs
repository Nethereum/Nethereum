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
            var functionCQSMessageModel = new FunctionCQSMessageModel(functionABI, _model.CQSNamespace);
            var functionOutputDTOModel = new FunctionOutputDTOModel(functionABI, _model.FunctionOutputNamespace);
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
$@"{SpaceUtils.TwoTabs}public IEnumerator Query(BlockParameter blockParameter = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}var {messageVariableName} = new {messageType}();
{SpaceUtils.ThreeTabs}yield return Query({messageVariableName}, blockParameter);
{SpaceUtils.TwoTabs}}}";
                }
                else
                {
                    queryMethod =
$@"{SpaceUtils.TwoTabs}public IEnumerator Query({_parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, BlockParameter blockParameter = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}var {messageVariableName} = new {messageType}();
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.FourTabs)}
{SpaceUtils.ThreeTabs}yield return Query({messageVariableName}, blockParameter);
{SpaceUtils.TwoTabs}}}";
                }
                var classQuery =
    $@"{SpaceUtils.OneTab}public partial class {functionNameUpper}QueryRequest : ContractFunctionQueryRequest<{messageType}, {functionOutputDTOType}>
{SpaceUtils.OneTab}{{

{SpaceUtils.TwoTabs}public {functionNameUpper}QueryRequest(IContractQueryUnityRequestFactory contractQueryUnityRequestFactory, string contractAddress) : base(contractQueryUnityRequestFactory, contractAddress)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.TwoTabs}}}

{SpaceUtils.TwoTabs}public {functionNameUpper}QueryRequest(string url, string contractAddress, string defaultAccount = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, contractAddress, defaultAccount, jsonSerializerSettings, requestHeaders)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.TwoTabs}}}

{queryMethod}

{SpaceUtils.OneTab}}}";
                return classQuery;
            }

            if (functionABIModel.IsTransaction())
            {
                var functionOutputDTOType = functionOutputDTOModel.GetTypeName();
                var txnMethod = string.Empty;
                if (functionABIModel.HasNoInputParameters())
                {
                    txnMethod =
$@"{SpaceUtils.TwoTabs}public IEnumerator SignAndSendTransaction(BlockParameter blockParameter = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}var {messageVariableName} = new {messageType}();
{SpaceUtils.ThreeTabs}yield return SignAndSendTransaction({messageVariableName});
{SpaceUtils.TwoTabs}}}";
                }
                else
                {
                    txnMethod =
$@"{SpaceUtils.TwoTabs}public IEnumerator SignAndSendTransaction({_parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters)}, BlockParameter blockParameter = null)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}var {messageVariableName} = new {messageType}();
{_parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, messageVariableName, SpaceUtils.FourTabs)}
{SpaceUtils.ThreeTabs}yield return SignAndSendTransaction({messageVariableName});
{SpaceUtils.TwoTabs}}}";
                }
                var classTxn =
    $@"{SpaceUtils.OneTab}public partial class {functionNameUpper}TransactionRequest : ContractFunctionTransactionRequest<{messageType}>
{SpaceUtils.OneTab}{{

{SpaceUtils.TwoTabs}public {functionNameUpper}TransactionRequest(IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, string contractAddress) : base(contractTransactionUnityRequestFactory, contractAddress)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.TwoTabs}}}

{SpaceUtils.TwoTabs}public {functionNameUpper}TransactionRequest(string url, BigInteger chainId, string privateKey, string contractAddress, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null) : base(url, chainId, privateKey, contractAddress, jsonSerializerSettings, requestHeaders)
{SpaceUtils.TwoTabs}{{
{SpaceUtils.TwoTabs}}}

{txnMethod}

{SpaceUtils.OneTab}}}";
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