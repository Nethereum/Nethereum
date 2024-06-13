using System;
using System.Linq;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.Unity.CSharp
{
    public class UnityContractFactoryCSharpTemplate : ClassTemplateBase<UnityContractFactoryModel>
    {
        private readonly UnityContractFactoryModel _model;
        private CommonGenerators _commonGenerators;
        private ITypeConvertor _typeConvertor;

        public UnityContractFactoryCSharpTemplate(UnityContractFactoryModel model) : base(model)
        {
            _model = model;
            _typeConvertor = new ABITypeToCSharpType();
            _commonGenerators = new CommonGenerators();
            ClassFileTemplate = new CSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            var functions = _model.ContractABI.Functions;
            var methods = string.Join(GenerateLineBreak(), functions.Select(GenerateMethod));
            var classTxn =
$@"{SpaceUtils.One__Tab}public partial class {Model.GetTypeName()} 
{SpaceUtils.One__Tab}{{
{SpaceUtils.Two___Tabs}public string ContractAddress {{ get; protected set; }}
{SpaceUtils.Two___Tabs}public IContractTransactionUnityRequestFactory ContractTransactionUnityRequestFactory {{ get; protected set; }}
{SpaceUtils.Two___Tabs}public IContractQueryUnityRequestFactory ContractQueryUnityRequestFactory {{ get; protected set; }}
{SpaceUtils.Two___Tabs}public {Model.GetTypeName()}(string contractAddress, IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, IContractQueryUnityRequestFactory contractQueryUnityRequestFactory)
            {{
                ContractAddress = contractAddress;
                ContractTransactionUnityRequestFactory = contractTransactionUnityRequestFactory;
                ContractQueryUnityRequestFactory = contractQueryUnityRequestFactory;
            }}

{methods}

{SpaceUtils.One__Tab}}}";
            return classTxn;

        }

        public string GenerateMethod(FunctionABI functionABI)
        {
            var functionNameUpper = _commonGenerators.GenerateClassName(functionABI.GetFunctionTypeNameBasedOnOverloads());
            var functionABIModel = new FunctionABIModel(functionABI, _typeConvertor, CodeGenLanguage.CSharp);
            if (!functionABIModel.IsTransaction())
            {
                return
    $@"
{SpaceUtils.Two___Tabs}public {functionNameUpper}QueryRequest Create{functionNameUpper}QueryRequest()
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}return new {functionNameUpper}QueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
{SpaceUtils.Two___Tabs}}}";
            }

            if (functionABIModel.IsTransaction())
            {
                return $@"
{SpaceUtils.Two___Tabs}public {functionNameUpper}TransactionRequest Create{functionNameUpper}TransactionRequest()
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}return new {functionNameUpper}TransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
{SpaceUtils.Two___Tabs}}}";
            }



            return null;
        }

        private string GenerateLineBreak()
        {
            return Environment.NewLine + Environment.NewLine;
        }


    }
}