using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.DTOs;

namespace Nethereum.Generators.MudTable
{
    public class MudTableCSharpTemplate : ClassTemplateBase<MudTableModel>
    {
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
{SpaceUtils.Two___Tabs}public {Model.GetTypeName()}() : base(""{Model.Name}"")
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}}}
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
{SpaceUtils.Two___Tabs}public {Model.GetTypeName()}() : base(""{Model.Name}"")
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Two___Tabs}
{SpaceUtils.Two___Tabs}}}
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
{SpaceUtils.One__Tab}}}";
        }
    }
}