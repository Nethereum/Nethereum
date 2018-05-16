using System;

namespace Nethereum.Generator.Console
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
                $@"C:\dev\test\Nethereum.Generators.Nuget.Test\{GeneratorConfigurationFactory.ConfigFileName}";

            sample.StandardContractConfigCreator.CreateTestGeneratorConfigFile(configFilePath);
        }
    }
}
