using System;
using Nethereum.ABI.Autogen.Configuration;

namespace Nethereum.ABI.Autogen
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                //GenerateTestConfigFile();

                return new App().Execute(args);
            }
            catch (Exception ex)
            {
                System.Console.Write(ex);
                return 1;
            }
        }

        private static void GenerateTestConfigFile()
        {
            var configFilePath =
                $@"C:\dev\repos\nethereum\src\Nethereum.Generators.Nuget.Test\{GeneratorConfigurationFactory.ConfigFileName}";

            sample.EIP20GeneratorConfig.CreateTestGeneratorConfigFile(configFilePath);
        }
    }
}
