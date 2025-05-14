using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;

namespace Nethereum.Generators.BlazorServicePage
{
    public class BlazorFunctionComponentsTemplate
    {
        private readonly BlazorPageServiceModel _model;
        private readonly ITypeConvertor _typeConvertor;
        private readonly CommonGenerators _commonGenerators;

        public BlazorFunctionComponentsTemplate(BlazorPageServiceModel model)
        {
            _model = model;
            _typeConvertor = new ABITypeToCSharpType();
            _commonGenerators = new CommonGenerators();
        }

        public string GenerateComponents(bool includeDeploymentComponent = false)
        {
            var components = new List<string>();

            if (includeDeploymentComponent)
            {
                var deploymentComponent = GenerateDeploymentComponent();
                if (!string.IsNullOrWhiteSpace(deploymentComponent))
                {
                    components.Add(deploymentComponent);
                }
            }

            components.AddRange(_model.ContractABI.Functions
                .Select(GenerateComponent)
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            return string.Join(Environment.NewLine + Environment.NewLine, components);
        }

        public string GenerateComponent(FunctionABI functionABI)
        {
            var functionModel = new FunctionABIModel(functionABI, _typeConvertor, CodeGenLanguage.CSharp);
            var functionCqsModel = new FunctionCQSMessageModel(functionABI, _model.CQSNamespace, null);
            var outputDtoModel = new FunctionOutputDTOModel(functionABI, _model.FunctionOutputNamespace, null);

            var messageType = functionCqsModel.GetTypeName();
            var serviceType = $"typeof({_model.GetServiceTypeName()})";
            var functionNameUpper = _commonGenerators.GenerateClassName(functionABI.Name);
            var title = functionABI.Name;

            if (functionModel.IsTransaction())
            {
                return
$@"{SpaceUtils.Two___Tabs}<TransactionFunctionComponent TFunctionMessage=""{messageType}""
{SpaceUtils.Three____Tabs}Title=""{title}""
{SpaceUtils.Three____Tabs}ContractAddress=""@ContractAddress""
{SpaceUtils.Three____Tabs}HostProvider=""selectedHostProviderService""
{SpaceUtils.Three____Tabs}ServiceType=""{serviceType}""
{SpaceUtils.Three____Tabs}ServiceRequestMethodName=""{functionNameUpper}RequestAsync""
{SpaceUtils.Three____Tabs}ServiceRequestAndWaitForReceiptMethodName=""{functionNameUpper}RequestAndWaitForReceiptAsync"" />";
            }

            if (!functionModel.IsTransaction())
            {
                string outputType;
                if (functionModel.IsMultipleOutput())
                {
                    outputType = outputDtoModel.GetTypeName();
                }
                else if (functionModel.IsSingleOutput())
                {
                    outputType = functionModel.GetSingleOutputReturnType();
                }
                else
                {
                    return null;
                }

                return
$@"{SpaceUtils.Two___Tabs}<QueryFunctionComponent TFunctionMessage=""{messageType}"" TFunctionOutput=""{outputType}""
{SpaceUtils.Three____Tabs}Title=""{title}""
{SpaceUtils.Three____Tabs}ContractAddress=""@ContractAddress""
{SpaceUtils.Three____Tabs}HostProvider=""selectedHostProviderService""
{SpaceUtils.Three____Tabs}ServiceType=""{serviceType}""
{SpaceUtils.Three____Tabs}ServiceMethodName=""{functionNameUpper}QueryAsync"" />";
            }
            return null;
        }

        public string GenerateDeploymentComponent()
        {
           
            if (string.IsNullOrWhiteSpace(_model.GetContractDeploymentTypeName()))
                return null;

            var deploymentType = _model.GetContractDeploymentTypeName();
            var serviceType = $"typeof({_model.GetTypeName()})";

            return
$@"{SpaceUtils.Two___Tabs}<ContractDeploymentComponent TDeploymentMessage=""{deploymentType}""
{SpaceUtils.Three____Tabs}HostProvider=""selectedHostProviderService""
{SpaceUtils.Three____Tabs}ServiceType=""{serviceType}""
{SpaceUtils.Three____Tabs}ContractAddressChanged=""ContractAddressChanged"" />";
        }
    }
}
