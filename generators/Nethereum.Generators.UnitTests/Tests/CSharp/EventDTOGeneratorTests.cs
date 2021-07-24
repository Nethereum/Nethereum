using Nethereum.Generators.Core;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;
using Xunit;

namespace Nethereum.Generators.Tests.CSharp
{
    public class EventDTOGeneratorTests: GeneratorTestBase<EventDTOGenerator>
    {
        static EventDTOGenerator CreateGenerator()
        {
            var contractAbi = new ContractABI();
            var eventAbi = new EventABI("ItemAdded", contractAbi){ InputParameters = new[] { new ParameterABI("uint256", "itemId") } };
            contractAbi.Events = new EventABI[] { eventAbi };
            return new EventDTOGenerator(eventAbi, "DefaultNamespace", CodeGenLanguage.CSharp);
        }

        public EventDTOGeneratorTests():base(CreateGenerator(), "CSharp")
        {
        }

        [Fact]
        public override void GeneratesExpectedFileContent()
        {
            GenerateAndCheckFileContent("EventDTO.01.csharp.txt");
        }

        [Fact]
        public override void GeneratesExpectedFileName()
        {
            GenerateAndCheckFileName("ItemAddedEventDTO.cs");
        }
    }
}