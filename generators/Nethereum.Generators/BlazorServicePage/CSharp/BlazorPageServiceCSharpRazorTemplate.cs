using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.Generators.BlazorServicePage
{
    class BlazorPageServiceCSharpRazorTemplate : ClassTemplateBase<BlazorPageServiceModel>
    {
        private readonly BlazorFunctionComponentsTemplate _blazorFunctionComponentsTemplate;

        public BlazorPageServiceCSharpRazorTemplate(BlazorPageServiceModel model) : base(model)
        {
            _blazorFunctionComponentsTemplate = new BlazorFunctionComponentsTemplate(model);
            ClassFileTemplate = new RazorClassFileTemplate(Model, this);
        }

        public override string GenerateClass()
        {

            var components = _blazorFunctionComponentsTemplate.GenerateComponents(includeDeploymentComponent: true);

            return
$@"@page ""/{Model.ContractName.ToLowerInvariant()}""
@rendermode InteractiveWebAssembly
@inject SelectedEthereumHostProviderService selectedHostProviderService

<MudContainer MaxWidth=""MaxWidth.Medium"" Class=""mt-4"">

{SpaceUtils.Two___Tabs}<MudText Typo=""Typo.h5"" Class=""mb-4"">{Model.ContractName}</MudText>

{SpaceUtils.Two___Tabs}<MudTextField @bind-Value=""ContractAddress"" Label=""{Model.ContractName} Contract Address"" Variant=""Variant.Outlined"" Class=""mb-4"" />

{components}

</MudContainer>

@code
{SpaceUtils.One__Tab}{{
{SpaceUtils.Two___Tabs}private string ContractAddress;

{SpaceUtils.Two___Tabs}private void ContractAddressChanged(string address)
{SpaceUtils.Two___Tabs}{{
{SpaceUtils.Three____Tabs}ContractAddress = address;
{SpaceUtils.Two___Tabs}}}
{SpaceUtils.One__Tab}}}";
        }
    }
}
