using Nethereum.ABI.Decoders;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.Contracts.IntegrationTests.EncodingInputOutput
{
    public class UsingNewKeywordOnProperties
    {
        [Fact]
        public async Task ShouldBeSupportedForSomeOrAllProperties()
        {
            // the result of a known query
            var jsonEncododeQueryBytes = "{'rawBytes':'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAUZvcmQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAARmllc3RhAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAgAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAARQSBuaWNlIGxpdHRsZSBjYXIAAAAAAAAAAAAAAAAAAAA='}";

            var deserializedWrapper = JObject.Parse(jsonEncododeQueryBytes);
            var rawOutputFromQuery = (byte[])deserializedWrapper["rawBytes"].ToObject(typeof(byte[]));

            //// vanilla code generation - no properties overriden or changed with "new" operator
            //// bytes32 properties are returned as byte[]
            var codeGeneratedFunctionDto = new GetVehicleOutputDTO();
            var vanillaProperties = PropertiesExtractor.GetPropertiesWithParameterAttribute(codeGeneratedFunctionDto.GetType()).ToArray();
            new FunctionCallDecoder().DecodeAttributes(rawOutputFromQuery, codeGeneratedFunctionDto, vanillaProperties);

            var bytes32Decoder = new Bytes32TypeDecoder();

            Assert.Equal(BigInteger.One, codeGeneratedFunctionDto.Vehicle.Id);
            Assert.Equal("Ford", bytes32Decoder.Decode<string>(codeGeneratedFunctionDto.Vehicle.Manufacturer));
            Assert.Equal("Fiesta", bytes32Decoder.Decode<string>(codeGeneratedFunctionDto.Vehicle.Model));
            Assert.Equal("A nice little car", codeGeneratedFunctionDto.Vehicle.Description);

            //// repeating each property on dto with "new" keyword in order to return a different .net type
            var dtoWhereEveryPropertyIsNew = new GetVehicleOutputDTO_WithAllNewPropertyTypes();
            var propertiesForAllNew = PropertiesExtractor.GetPropertiesWithParameterAttribute(dtoWhereEveryPropertyIsNew.GetType()).ToArray();
            new FunctionCallDecoder().DecodeAttributes(rawOutputFromQuery, dtoWhereEveryPropertyIsNew, propertiesForAllNew);

            Assert.Equal(BigInteger.One, dtoWhereEveryPropertyIsNew.Vehicle.Id);
            Assert.Equal("Ford", dtoWhereEveryPropertyIsNew.Vehicle.Manufacturer);
            Assert.Equal("Fiesta", dtoWhereEveryPropertyIsNew.Vehicle.Model);
            Assert.Equal("A nice little car", dtoWhereEveryPropertyIsNew.Vehicle.Description);

            // custom "vehicle_custom" class which inherits the code generated "vehicle" class
            // bytes32 properties for Manufacturer and Model created as "new" properties
            var modifiedDtoWithSomeNewProperties = new GetVehicleOutputDTO_WithSomeNewPropertyTypes();
            var propertiesForModifiedDto = PropertiesExtractor.GetPropertiesWithParameterAttribute(modifiedDtoWithSomeNewProperties.GetType()).ToArray();
            new FunctionCallDecoder().DecodeAttributes(rawOutputFromQuery, modifiedDtoWithSomeNewProperties, propertiesForModifiedDto);

            

            Assert.Equal(BigInteger.One, modifiedDtoWithSomeNewProperties.Vehicle.Id);
            Assert.Equal("Ford", modifiedDtoWithSomeNewProperties.Vehicle.Manufacturer);
            Assert.Equal("Fiesta", modifiedDtoWithSomeNewProperties.Vehicle.Model);
            Assert.Equal("A nice little car", modifiedDtoWithSomeNewProperties.Vehicle.Description);
        }

        public partial class DecodeTestDeployment : DecodeTestDeploymentBase
        {
            public DecodeTestDeployment() : base(BYTECODE) { }
            public DecodeTestDeployment(string byteCode) : base(byteCode) { }
        }

        public class DecodeTestDeploymentBase : ContractDeploymentMessage
        {
            public static string BYTECODE = "608060405234801561001057600080fd5b5060408051608081018252600180825263119bdc9960e21b60208084019182526546696573746160d01b8486019081528551808701909652601186527020903734b1b2903634ba3a36329031b0b960791b8683015260608501958652600093845292815283517fada5013122d395ba3c54772283fb069b10426056ef8ca54750cb9bb552a59e7d90815591517fada5013122d395ba3c54772283fb069b10426056ef8ca54750cb9bb552a59e7e5591517fada5013122d395ba3c54772283fb069b10426056ef8ca54750cb9bb552a59e7f5592518051929392610116927fada5013122d395ba3c54772283fb069b10426056ef8ca54750cb9bb552a59e80920190610225565b505060408051608081018252600280825262424d5760e81b60208084019182526235333560e81b84860190815285518087019096526011865270412062696720666173742065737461746560781b8683015260608501958652600093845292815283517fabbb5caa7dda850e60932de0934eb1f9d0f59695050f761dc64e443e5030a56990815591517fabbb5caa7dda850e60932de0934eb1f9d0f59695050f761dc64e443e5030a56a5591517fabbb5caa7dda850e60932de0934eb1f9d0f59695050f761dc64e443e5030a56b559251805192945061021c927fabbb5caa7dda850e60932de0934eb1f9d0f59695050f761dc64e443e5030a56c929190910190610225565b509050506102c0565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f1061026657805160ff1916838001178555610293565b82800160010185558215610293579182015b82811115610293578251825591602001919060010190610278565b5061029f9291506102a3565b5090565b6102bd91905b8082111561029f57600081556001016102a9565b90565b610219806102cf6000396000f3fe608060405234801561001057600080fd5b506004361061002b5760003560e01c8063df7ebb7b14610030575b600080fd5b61004361003e366004610151565b610059565b6040516100509190610169565b60405180910390f35b61006161012b565b60008281526020818152604091829020825160808101845281548152600180830154828501526002808401548387015260038401805487519381161561010002600019011691909104601f81018690048602830186019096528582529194929360608601939192919083018282801561011b5780601f106100f05761010080835404028352916020019161011b565b820191906000526020600020905b8154815290600101906020018083116100fe57829003601f168201915b5050505050815250509050919050565b604080516080810182526000808252602082018190529181019190915260608082015290565b600060208284031215610162578081fd5b5035919050565b600060208252825160208301526020830151604083015260408301516060830152606083015160808084015280518060a0850152825b818110156101bc57602081840181015160c087840101520161019f565b818111156101cd578360c083870101525b50601f01601f19169290920160c001939250505056fea2646970667358221220149deb6f3ad594624e2ea4af617546b0db87e2ee084c878bdd3edc465b31933c64736f6c63430006010033";
            public DecodeTestDeploymentBase() : base(BYTECODE) { }
            public DecodeTestDeploymentBase(string byteCode) : base(byteCode) { }

        }

        public partial class GetVehicleFunction : GetVehicleFunctionBase { }

        [Function("getVehicle", typeof(GetVehicleOutputDTO))]
        public class GetVehicleFunctionBase : FunctionMessage
        {
            [Parameter("uint256", "_id", 1)]
            public virtual BigInteger Id { get; set; }
        }

        public partial class GetVehicleOutputDTO : GetVehicleOutputDTOBase { }

        [FunctionOutput]
        public class GetVehicleOutputDTOBase : IFunctionOutputDTO
        {
            [Parameter("tuple", "_vehicle", 1)]
            public virtual Vehicle Vehicle { get; set; }
        }

        public partial class Vehicle : VehicleBase { }

        public class VehicleBase
        {
            [Parameter("uint256", "id", 1)]
            public virtual BigInteger Id { get; set; }
            [Parameter("bytes32", "manufacturer", 2)]
            public virtual byte[] Manufacturer { get; set; }
            [Parameter("bytes32", "model", 3)]
            public virtual byte[] Model { get; set; }
            [Parameter("string", "description", 4)]
            public virtual string Description { get; set; }
        }

        [Function("getVehicle", typeof(GetVehicleOutputDTO_WithSomeNewPropertyTypes))]
        public class GetVehicleFunction_Custom : FunctionMessage
        {
            [Parameter("uint256", "_id", 1)]
            public virtual BigInteger Id { get; set; }
        }

        /// <summary>
        /// A copy of the GetVehicleOutputDTO but using Vehicle_WithNewPropertyTypes for the Vehicle struct
        /// </summary>
        [FunctionOutput]
        public class GetVehicleOutputDTO_WithSomeNewPropertyTypes : IFunctionOutputDTO
        {
            [Parameter("tuple", "_vehicle", 1)]
            public virtual Vehicle_WithSomeNewPropertyTypes Vehicle { get; set; }
        }

        public partial class Vehicle_WithSomeNewPropertyTypes : Vehicle
        {
            [Parameter("bytes32", "manufacturer", 2)]
            public new string Manufacturer { get; set; }

            [Parameter("bytes32", "model", 3)]
            public new string Model { get; set; }
        }

        /// <summary>
        /// A copy of the GetVehicleOutputDTO but using Vehicle_WithNewPropertyTypes for the Vehicle struct
        /// </summary>
        [FunctionOutput]
        public class GetVehicleOutputDTO_WithAllNewPropertyTypes : IFunctionOutputDTO
        {
            [Parameter("tuple", "_vehicle", 1)]
            public virtual Vehicle_WithAllNewPropertyTypes Vehicle { get; set; }
        }

        public partial class Vehicle_WithAllNewPropertyTypes : Vehicle
        {
            [Parameter("uint256", "id", 1)]
            public new BigInteger Id { get; set; }

            [Parameter("bytes32", "manufacturer", 2)]
            public new string Manufacturer { get; set; }

            [Parameter("bytes32", "model", 3)]
            public new string Model { get; set; }

            [Parameter("string", "description", 4)]
            public new string Description { get; set; }
        }

    }

}
