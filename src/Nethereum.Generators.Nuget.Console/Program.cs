using System;

namespace Nethereum.Generators.Nuget.Console
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
                @"C:\dev\repos\nethereum\src\Nethereum.Generators.Nuget.Test\Nethereum.Generator.config";

            sample.EIP20GeneratorConfig.CreateTestGeneratorConfigFile(configFilePath);
        }
    }
}
