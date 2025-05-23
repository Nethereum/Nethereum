using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.Service
{

    public class BlazorPageServiceCSharpRazorTemplate: ClassTemplateBase
    {
        private BlazorFunctionComponentsTemplate _blazorFunctionComponentsTemplate;
        public BlazorPageServiceModel Model => (BlazorPageServiceModel)ClassModel;

        public BlazorPageServiceCSharpRazorTemplate(BlazorPageServiceModel model): base(model)
        {
            System.Console.WriteLine("Initialising BlazorPageServiceCSharpRazorTemplate");
            _blazorFunctionComponentsTemplate = new BlazorFunctionComponentsTemplate(model);
            ClassFileTemplate = new RazorClassFileTemplate(Model, this);
            System.Console.WriteLine("Finished Initialising BlazorPageServiceCSharpRazorTemplate");
        }

        public override string GenerateClass()
        {

            var components = _blazorFunctionComponentsTemplate.GenerateComponents(includeDeploymentComponent: true);

            return
$@"@page ""/{Model.ContractName.ToLowerInvariant()}""
@page ""/{Model.ContractName.ToLowerInvariant()}/{{ContractAddress}}""

@inject SelectedEthereumHostProviderService selectedHostProviderService

<MudContainer MaxWidth=""MaxWidth.Medium"" Class=""mt-4"">

{SpaceUtils.Two___Tabs}<MudText Typo=""Typo.h5"" Class=""mb-4"">{Model.ContractName}</MudText>

{SpaceUtils.Two___Tabs}<MudTextField @bind-Value=""ContractAddress"" Label=""{Model.ContractName} Contract Address"" Variant=""Variant.Outlined"" Class=""mb-4"" />

{components}

</MudContainer>

@code
{SpaceUtils.One__Tab}{{
{SpaceUtils.One__Tab}[Parameter]
{SpaceUtils.One__Tab}public string ContractAddress {{ get; set; }}

{SpaceUtils.Two___Tabs}private void ContractAddressChanged(string address)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}ContractAddress = address;
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.One__Tab}}}";
        }
    }
}
