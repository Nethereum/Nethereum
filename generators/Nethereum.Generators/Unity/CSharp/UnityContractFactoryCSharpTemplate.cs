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
$@"{SpaceUtils.OneTab}public partial class {Model.GetTypeName()} 
{SpaceUtils.OneTab}{{
{SpaceUtils.TwoTabs}public string ContractAddress {{ get; protected set; }}
{SpaceUtils.TwoTabs}public IContractTransactionUnityRequestFactory ContractTransactionUnityRequestFactory {{ get; protected set; }}
{SpaceUtils.TwoTabs}public IContractQueryUnityRequestFactory ContractQueryUnityRequestFactory {{ get; protected set; }}
{SpaceUtils.TwoTabs}public {Model.GetTypeName()}(string contractAddress, IContractTransactionUnityRequestFactory contractTransactionUnityRequestFactory, IContractQueryUnityRequestFactory contractQueryUnityRequestFactory)
            {{
                ContractAddress = contractAddress;
                ContractTransactionUnityRequestFactory = contractTransactionUnityRequestFactory;
                ContractQueryUnityRequestFactory = contractQueryUnityRequestFactory;
            }}

{methods}

{SpaceUtils.OneTab}}}";
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
{SpaceUtils.TwoTabs}public {functionNameUpper}QueryRequest Create{functionNameUpper}QueryRequest()
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}return new {functionNameUpper}QueryRequest(ContractQueryUnityRequestFactory, ContractAddress);
{SpaceUtils.TwoTabs}}}";
            }

            if (functionABIModel.IsTransaction())
            {
                return $@"
{SpaceUtils.TwoTabs}public {functionNameUpper}TransactionRequest Create{functionNameUpper}TransactionRequest()
{SpaceUtils.TwoTabs}{{
{SpaceUtils.ThreeTabs}return new {functionNameUpper}TransactionRequest(ContractTransactionUnityRequestFactory, ContractAddress);
{SpaceUtils.TwoTabs}}}";
            }



            return null;
        }

        private string GenerateLineBreak()
        {
            return Environment.NewLine + Environment.NewLine;
        }


    }
}