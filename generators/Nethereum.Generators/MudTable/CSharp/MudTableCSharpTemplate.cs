using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;
using System;
using System.Linq;

namespace Nethereum.Generators.MudTable
{
    public class MudTableCSharpTemplate : ClassTemplateBase
    {
        public MudTableModel Model => (MudTableModel)ClassModel;
        private ParameterABIFunctionDTOCSharpTemplate _parameterAbiFunctionDtocSharpTemplate;
        public MudTableCSharpTemplate(MudTableModel model) : base(model)
        {
            _parameterAbiFunctionDtocSharpTemplate = new ParameterABIFunctionDTOCSharpTemplate();
            ClassFileTemplate = new CSharpClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {
            if (Model.IsSingleton())
            {
                return
    $@"{GetSingletonServiceClass()}
{SpaceUtils.One__Tab}
{SpaceUtils.One__Tab}public partial class {Model.GetTypeName()} : TableRecordSingleton<{Model.GetTypeName()}.{Model.GetValueTypeName()}> 
{SpaceUtils.One__Tab}{{
{SpaceUtils.Two___Tabs}public {Model.GetTypeName()}() : {GetBaseConstructor()}
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.NoTabs}
{GenerateDirectAccessProperties(Model.MudTable.ValueSchema, false, SpaceUtils.Two___Tabs)}
{SpaceUtils.NoTabs}
{SpaceUtils.Two___Tabs}public partial class {Model.GetValueTypeName()}
{SpaceUtils.Two___Tabs}{{
{_parameterAbiFunctionDtocSharpTemplate.GenerateAllProperties(Model.MudTable.ValueSchema, SpaceUtils.Three____Tabs)}          
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.One__Tab}}}";
            }
            else
            {
                return
                    $@"{GetServiceClass()}
{SpaceUtils.One__Tab}
{SpaceUtils.One__Tab}public partial class {Model.GetTypeName()} : TableRecord<{Model.GetTypeName()}.{Model.GetKeyTypeName()}, {Model.GetTypeName()}.{Model.GetValueTypeName()}> 
{SpaceUtils.One__Tab}{{
{SpaceUtils.Two___Tabs}public {Model.GetTypeName()}() : {GetBaseConstructor()}
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}}}
{GenerateDirectAccessProperties(Model.MudTable.Keys, true, SpaceUtils.Two___Tabs)}
{GenerateDirectAccessProperties(Model.MudTable.ValueSchema, false, SpaceUtils.Two___Tabs)}
{SpaceUtils.NoTabs}
{SpaceUtils.Two___Tabs}public partial class {Model.GetKeyTypeName()}
{SpaceUtils.Two___Tabs}{{
{_parameterAbiFunctionDtocSharpTemplate.GenerateAllProperties(Model.MudTable.Keys, SpaceUtils.Three____Tabs)}
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.NoTabs}
{SpaceUtils.Two___Tabs}public partial class {Model.GetValueTypeName()}
{SpaceUtils.Two___Tabs}{{
{_parameterAbiFunctionDtocSharpTemplate.GenerateAllProperties(Model.MudTable.ValueSchema, SpaceUtils.Three____Tabs)}          
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.One__Tab}}}";
            }

        }

        public string GenerateDirectAccessProperties(ParameterABI[] parameters, bool isKey, string spacing)
        {
            return string.Join(Environment.NewLine, parameters.Select(x => GenerateDirectAccessProperty(x, isKey, spacing)));
        }

        public string GenerateDirectAccessProperty(ParameterABI parameter, bool isKey, string spacing)
        {
            var typeMapper = new ABITypeToCSharpType();
            var parameterAbiModelTypeMap = new ParameterABIModelTypeMap(typeMapper, CodeGenLanguage.CSharp);
            var parameterModel = new ParameterABIModel(parameter, CodeGenLanguage.CSharp);
            var access = isKey ? "Keys" : "Values";
            return
                $@"{spacing}/// <summary>
{spacing}/// Direct access to the {(isKey ? "key" : "value")} property '{parameterModel.GetPropertyName()}'.
{spacing}/// </summary>
{spacing}public virtual {parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter)} {parameterModel.GetPropertyName()} => {access}.{parameterModel.GetPropertyName()};";
        }

        public string GetBaseConstructor()
        {
            if (string.IsNullOrEmpty(Model.MudTable.MudNamespace))
            {
                return $@"base(""{Model.Name}"")";
            }
            else
            {
                return $@"base(""{Model.MudTable.MudNamespace}"", ""{Model.Name}"")";
            }
        }

        public string GetSingletonServiceClass()
        {
            return $@"{SpaceUtils.One__Tab}public partial class {Model.GetServiceTypeName()} : TableSingletonService<{Model.GetTypeName()},{Model.GetTypeName()}.{Model.GetValueTypeName()}>
{SpaceUtils.One__Tab}{{ 
{SpaceUtils.Two___Tabs}public {Model.GetServiceTypeName()}(IWeb3 web3, string contractAddress) : base(web3, contractAddress) {{}}
{SpaceUtils.One__Tab}}}";
        }

        public string GetServiceClass()
        {
            return $@"{SpaceUtils.One__Tab}public partial class {Model.GetServiceTypeName()} : TableService<{Model.GetTypeName()}, {Model.GetTypeName()}.{Model.GetKeyTypeName()}, {Model.GetTypeName()}.{Model.GetValueTypeName()}>
{SpaceUtils.One__Tab}{{ 
{SpaceUtils.Two___Tabs}public {Model.GetServiceTypeName()}(IWeb3 web3, string contractAddress) : base(web3, contractAddress) {{}}
{GenerateGetTableRecordMethod()}
{GenerateSetRecordRequestMethod()}
{GenerateSetRecordRequestAndWaitMethod()}
{SpaceUtils.One__Tab}}}";
        }


        private string GenerateGetTableRecordMethod()
        {
            var keyParams = GenerateKeyPropertiesInitialization(SpaceUtils.Three____Tabs);
            return $@"{SpaceUtils.Two___Tabs}public virtual Task<{Model.GetTypeName()}> GetTableRecordAsync({GenerateMethodParameters(Model.MudTable.Keys)}, BlockParameter blockParameter = null)
{SpaceUtils.Two___Tabs}{{
{keyParams}
{SpaceUtils.Three____Tabs}return GetTableRecordAsync(_key, blockParameter);
{SpaceUtils.Two___Tabs}}}";
        }

        private string GenerateSetRecordRequestMethod()
        {
            var keyParams = GenerateKeyPropertiesInitialization(SpaceUtils.Three____Tabs);
            var valueParams = GenerateValuePropertiesInitialization(SpaceUtils.Three____Tabs);
            return $@"{SpaceUtils.Two___Tabs}public virtual Task<string> SetRecordRequestAsync({GenerateMethodParameters(Model.MudTable.Keys)}, {GenerateValueParameters(Model.MudTable.ValueSchema)})
{SpaceUtils.Two___Tabs}{{
{keyParams}
{valueParams}
{SpaceUtils.Three____Tabs}return SetRecordRequestAsync(_key, _values);
{SpaceUtils.Two___Tabs}}}";
        }

        private string GenerateSetRecordRequestAndWaitMethod()
        {
            var keyParams = GenerateKeyPropertiesInitialization(SpaceUtils.Three____Tabs);
            var valueParams = GenerateValuePropertiesInitialization(SpaceUtils.Three____Tabs);
            return $@"{SpaceUtils.Two___Tabs}public virtual Task<TransactionReceipt> SetRecordRequestAndWaitForReceiptAsync({GenerateMethodParameters(Model.MudTable.Keys)}, {GenerateValueParameters(Model.MudTable.ValueSchema)})
{SpaceUtils.Two___Tabs}{{
{keyParams}
{valueParams}
{SpaceUtils.Three____Tabs}return SetRecordRequestAndWaitForReceiptAsync(_key, _values);
{SpaceUtils.Two___Tabs}}}";
        }

        private string GenerateMethodParameters(ParameterABI[] parameters)
        {
            return string.Join(", ", parameters.Select(p =>
            {
                var parameterModel = new ParameterABIModel(p, CodeGenLanguage.CSharp);
                var typeMapper = new ABITypeToCSharpType();
                var parameterAbiModelTypeMap = new ParameterABIModelTypeMap(typeMapper, CodeGenLanguage.CSharp);
                var type = parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(p);
                return $"{type} {parameterModel.GetVariableName()}";
            }));
        }

        private string GenerateKeyPropertiesInitialization(string spacing)
        {
            return
    $@"{spacing}var _key = new {Model.GetTypeName()}.{Model.GetKeyTypeName()}();
{string.Join(Environment.NewLine, Model.MudTable.Keys.Select(k =>
    $"{spacing}_key.{new ParameterABIModel(k, CodeGenLanguage.CSharp).GetPropertyName()} = {new ParameterABIModel(k, CodeGenLanguage.CSharp).GetVariableName()};"))}";
        }

        private string GenerateValuePropertiesInitialization(string spacing)
        {
            return $@"
{spacing}var _values = new {Model.GetTypeName()}.{Model.GetValueTypeName()}();
{string.Join(Environment.NewLine, Model.MudTable.ValueSchema.Select(v =>
    $"{spacing}_values.{new ParameterABIModel(v, CodeGenLanguage.CSharp).GetPropertyName()} = {new ParameterABIModel(v, CodeGenLanguage.CSharp).GetVariableName()};"))}";
        }

        private string GenerateValueParameters(ParameterABI[] valueSchema)
        {
            return string.Join(", ", valueSchema.Select(v =>
            {
                var parameterModel = new ParameterABIModel(v, CodeGenLanguage.CSharp);
                var typeMapper = new ABITypeToCSharpType();
                var parameterAbiModelTypeMap = new ParameterABIModelTypeMap(typeMapper, CodeGenLanguage.CSharp);
                var type = parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(v);
                return $"{type} {parameterModel.GetVariableName()}";
            }));
        }
    }
}