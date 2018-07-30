using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Nethereum.Generators.Net;
using Xunit;

namespace Nethereum.Generators.UnitTests.Tests
{
    public class RegistryContractTests
    {
        [Fact]
        public void Test()
        {
            var abiFile = @"C:\dev\repos\VehicleMaintenanceRegistry\src\VMR\bin\contracts\Registry.abi";
            var contractABI = new GeneratorModelABIDeserialiser().DeserialiseABI(File.ReadAllText(abiFile));

        }

    }
}
