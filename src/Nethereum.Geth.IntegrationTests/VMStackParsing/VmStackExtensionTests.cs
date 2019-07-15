using Nethereum.Geth.VMStackParsing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Geth.IntegrationTests.VMStackParsing
{
    public class VmStackExtensionTests
    {
        [Fact]
        public async Task CanFindInterContractCalls()
        {
            var stackTrace = await GetTestStackTrace("StackTrace_Calls.json");

            var interContractCalls = stackTrace.GetInterContractCalls("0x786a30e1ab0c58303c85419b9077657ad4fdb0ea").ToArray();

            Assert.Equal(4, interContractCalls.Length);
            Assert.Equal(OpCodes.Call, interContractCalls[0].Op);
            Assert.Equal("0x786a30e1ab0c58303c85419b9077657ad4fdb0ea", interContractCalls[0].From);
            Assert.Equal("0xd0828aeb00e4db6813e2f330318ef94d2bba2f60", interContractCalls[0].To);
            Assert.Equal(OpCodes.Call, interContractCalls[1].Op);
            Assert.Equal("0xd0828aeb00e4db6813e2f330318ef94d2bba2f60", interContractCalls[1].From);
            Assert.Equal("0x243e72b69141f6af525a9a5fd939668ee9f2b354", interContractCalls[1].To);
            Assert.Equal(OpCodes.Call, interContractCalls[2].Op);
            Assert.Equal("0x786a30e1ab0c58303c85419b9077657ad4fdb0ea", interContractCalls[2].From);
            Assert.Equal("0x6c498f0f83d0bbec758ee7f23e13c9ee522a4c8f", interContractCalls[2].To);
            Assert.Equal(OpCodes.Call, interContractCalls[3].Op);
            Assert.Equal("0x6c498f0f83d0bbec758ee7f23e13c9ee522a4c8f", interContractCalls[3].From);
            Assert.Equal("0x2a212f50a2a020010ea88cc33fc4c333e6a5c5c4", interContractCalls[3].To);
        }

        [Fact]
        public async Task CanFindInterContractCreations()
        {
            var stackTrace = await GetTestStackTrace("StackTrace_Creations.json");
            var interContractCalls = stackTrace.GetInterContractCalls("0xd0828aeb00e4db6813e2f330318ef94d2bba2f60").ToArray();

            Assert.Single(interContractCalls);
            Assert.Equal(OpCodes.Create, interContractCalls[0].Op);
            Assert.Equal("0xd0828aeb00e4db6813e2f330318ef94d2bba2f60", interContractCalls[0].From);
            Assert.Equal("0xfa851037cfc11895f9446e6e7e826bdafb3cdfcd", interContractCalls[0].To);
        }

        [Fact]
        public async Task CanFindInterContractDelegateCalls()
        {
            var stackTrace = await GetTestStackTrace("StackTrace_DelegateCalls.json");
            var interContractCalls = stackTrace.GetInterContractCalls("0x786a30e1ab0c58303c85419b9077657ad4fdb0ea").ToArray();

            Assert.Single(interContractCalls);
            Assert.Equal(OpCodes.DelegateCall, interContractCalls[0].Op);
            Assert.Equal("0x786a30e1ab0c58303c85419b9077657ad4fdb0ea", interContractCalls[0].From);
            Assert.Equal("0xd0828aeb00e4db6813e2f330318ef94d2bba2f60", interContractCalls[0].To);
        }

        [Fact]
        public async Task CanFindInterContractSelfDestructs()
        {
            var stackTrace = await GetTestStackTrace("StackTrace_SelfDestruct.json");
            var interContractCalls = stackTrace.GetInterContractCalls("0xd2e474c616cc60fb95d8b5f86c1043fa4552611b").ToArray();

            Assert.Equal(2, interContractCalls.Length);
            Assert.Equal(OpCodes.Call, interContractCalls[0].Op);
            Assert.Equal("0xd2e474c616cc60fb95d8b5f86c1043fa4552611b", interContractCalls[0].From);
            Assert.Equal("0xd8409cd9cf27b3a6d6532292340a2d050f335c61", interContractCalls[0].To);
            Assert.Equal(OpCodes.SelfDestruct, interContractCalls[1].Op);
            Assert.Equal("0xd2e474c616cc60fb95d8b5f86c1043fa4552611b", interContractCalls[1].From);
            Assert.Equal("0xd8409cd9cf27b3a6d6532292340a2d050f335c61", interContractCalls[1].To);
        }

        private async Task<JObject> GetTestStackTrace(string name)
        {
            JObject jObject = null;
            using (var stream = GetResourceStream(name))
            {
                using (var streamReader = new StreamReader(stream))
                {
                    using (var jsonReader = new JsonTextReader(streamReader))
                    {
                        jObject = await JObject.LoadAsync(jsonReader);
                    }
                }
            }

            return jObject;
        }

        private Stream GetResourceStream(string name)
        {
            return GetType().Assembly.GetManifestResourceStream(
                 $"{GetType().Namespace}.TestData.{name}");
        }
    }
}
