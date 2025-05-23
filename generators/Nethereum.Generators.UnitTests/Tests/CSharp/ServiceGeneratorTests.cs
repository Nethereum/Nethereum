using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;
using Nethereum.Generators.Service;
using Xunit;

namespace Nethereum.Generators.Tests.CSharp
{
    public class ServiceGeneratorTests: GeneratorTestBase<ServiceGenerator>
    {
        static ServiceGenerator CreateGenerator()
        {
            var contractABI = new ContractABI
            {
                Constructor = new ConstructorABI
                {
                    InputParameters = new[] {new ParameterABI("byte32", "owner")}
                },
            };
            contractABI.Functions = new[]
            {
                new FunctionABI("addAdministrator", false, contractABI)
                {
                    InputParameters = new[] {new ParameterABI("bytes32", "administratorId", 1)},
                    OutputParameters = new[] {new ParameterABI("bool", 1)}
                }
            };
            contractABI.Events = new[]
            {
                new EventABI("AdministratorAdded", contractABI)
                {
                    InputParameters = new[] {new ParameterABI("bytes32", "administratorId", 1)}
                }
            };


            return new ServiceGenerator(contractABI, "StandardContract", "0x123456", "DefaultNamespace", "CQS", "Functions", null, CodeGenLanguage.CSharp);
        }


        public ServiceGeneratorTests():base(CreateGenerator(), "CSharp")
        {
        }

        [Fact]
        public override void GeneratesExpectedFileContent()
        {
            GenerateAndCheckFileContent("ServiceGenerator.01.csharp.txt");
        }

        [Fact]
        public override void GeneratesExpectedFileName()
        {
            GenerateAndCheckFileName("StandardContractService.cs");
        }
    }
}