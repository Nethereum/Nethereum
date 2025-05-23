using Nethereum.Generators.Core;

namespace Nethereum.Generators.Net.ContractLibrary
{
    public class GenerateClassesCommand
    {
        public string ContractByteCode { get; set; }
        public string Abi { get; set; }
        public string BasePath { get; set; }
        public string ContractName { get; set; }
        public string ServiceNamespace { get; set; }
        public string CqsNamespace { get; set; }
        public string DtoNamesapce { get; set; }

        public string SharedTypesNamespace { get; set; }

        public string[] SharedGeneratedTypes { get; set; }  

        public string PathDelimiter { get; set; }
        public CodeGenLanguage CodeGenLanguage { get; }
        public string BaseNamespace { get; set; }

        public GenerateClassesCommand(string contractByteCode, string abi, string basePath,  string baseNamespace, string contractName, string serviceNamespace, string cqsNamespace, string dtoNamesapce, string sharedTypesNamespace,
            string[] sharedGeneratedTypes, string pathDelimiter, CodeGenLanguage codeGenLanguage)
        {
            ContractByteCode = contractByteCode;
            Abi = abi;
            BasePath = basePath;
            BaseNamespace = baseNamespace;
            ContractName = contractName;
            ServiceNamespace = serviceNamespace;
            CqsNamespace = cqsNamespace;
            DtoNamesapce = dtoNamesapce;
            PathDelimiter = pathDelimiter;
            CodeGenLanguage = codeGenLanguage;
            SharedTypesNamespace = sharedTypesNamespace;
            SharedGeneratedTypes = sharedGeneratedTypes;
        }

        public GenerateClassesCommand(string contractByteCode, string abi, string basePath, string baseNamespace, string contractName, string pathSeparator, CodeGenLanguage codeGenLanguage) 
            : this(contractByteCode, abi, basePath, baseNamespace, contractName, (string)DefaultNamespaceService.SetDefaultService(contractName), (string)DefaultNamespaceService.SetDefaultCqs(contractName), (string)DefaultNamespaceService.SetDefaultDto(contractName), null, null, pathSeparator, codeGenLanguage)
        {

        }

       
    }
}