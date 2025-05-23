
using Nethereum.Generators.Core;
using Nethereum.Generators.Model;
using Nethereum.Generators.CQS;
using Nethereum.Generators.Service;

namespace Nethereum.Generators
{
    public class BlazorPagesGenerator
    {
        public ContractABI ContractABI { get; }
        public string Namespace { get; }
        public string BaseOutputPath { get; }
        public string PathDelimiter { get; }
        public string ContractName { get; }
        public string BaseNamespace { get; }
        public string ServiceNamespace { get; }
        public string CQSNamespace { get; }
        public string DTONamespace { get; }
        public CodeGenLanguage CodeGenLanguage { get; }
        public string SharedTypesNamespace { get; }

        public ContractDeploymentCQSMessageModel ContractDeploymentCQSMessageModel { get; }

        public BlazorPagesGenerator(ContractABI contractABI, string contractName,  string baseNamespace, 
                                                            string serviceNamespace, string cqsNamespace, 
                                                            string dtoNamespace, string sharedTypesNamespace, 
                                                            CodeGenLanguage codeGenLanguage, string baseOutputPath, string pathDelimiter, 
            string @namespace)
        {
            ContractABI = contractABI;
            ContractName = contractName;
            Namespace = @namespace;
            BaseNamespace = baseNamespace;
            ServiceNamespace = serviceNamespace;
            CQSNamespace = cqsNamespace;
            DTONamespace = dtoNamespace;
            SharedTypesNamespace = sharedTypesNamespace;
            CodeGenLanguage = codeGenLanguage;
            BaseOutputPath = baseOutputPath;
            PathDelimiter = pathDelimiter;
        }

        public GeneratedFile GenerateFile()
        {
            var pageNamespace = GetFullNamespace(Namespace);
            var serviceNamespace = GetFullNamespace(ServiceNamespace);
            var dtoNamespace = GetFullNamespace(DTONamespace);
            var cqsNamespace = GetFullNamespace(CQSNamespace);

            string sharedTypesFullNamespace = null;
            if (!string.IsNullOrEmpty(SharedTypesNamespace))
            {
                sharedTypesFullNamespace = GetFullNamespace(SharedTypesNamespace);
            }

            var fullPath = BaseOutputPath;
            System.Console.WriteLine($"Generating Blazor Service Page for {ContractName} in {fullPath}");
            //parameters console
            System.Console.WriteLine("PageNamespace: " + pageNamespace);
            System.Console.WriteLine("ServiceNamespace: " + serviceNamespace);
            System.Console.WriteLine("CQSNamespace: " + cqsNamespace);
            System.Console.WriteLine("DtoNamespace: " + dtoNamespace);
            System.Console.WriteLine("SharedTypesNamespace: " + sharedTypesFullNamespace);
            System.Console.WriteLine("BaseOutputPath: " + BaseOutputPath);
            System.Console.WriteLine("PathDelimiter: " + PathDelimiter);
            System.Console.WriteLine("CodeGenLanguage: " + CodeGenLanguage);

            var generator = new BlazorPageServiceGenerator(ContractABI, ContractName, pageNamespace, serviceNamespace, dtoNamespace, sharedTypesFullNamespace, CodeGenLanguage);
            System.Console.WriteLine($"Generating Blazor Service Page Content for {ContractName} in {fullPath}");
            return  generator.GenerateFileContent(fullPath);

        }

        public string GetFullNamespace(string @namespace)
        {
            if (string.IsNullOrEmpty(BaseNamespace)) return @namespace;
            if (string.IsNullOrEmpty(@namespace)) return BaseNamespace;
            return BaseNamespace + "." + @namespace.TrimStart('.');
        }

    }
}
