using Nethereum.Generators.Core;
using Nethereum.Generators.CQS;
using Nethereum.Generators.DTOs;
using Nethereum.Generators.Model;
using Nethereum.Generators.Service;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Nethereum.Generators
{
    public class ContractProjectGenerator
    {
        public ContractABI ContractABI { get; }
        public string ContractName { get; }
        public string ByteCode { get; }
        public string BaseNamespace { get; }
        public string ServiceNamespace { get; }
        public string CQSNamespace { get; }
        public string DTONamespace { get; }
        public string BaseOutputPath { get; }
        public string PathDelimiter { get; }
        public CodeGenLanguage CodeGenLanguage { get; }

        private string ProjectName { get; }

        public ContractProjectGenerator(ContractABI contractABI,
            string contractName,
            string byteCode,
            string baseNamespace,
            string serviceNamespace,
            string cqsNamespace,
            string dtoNamespace,
            string baseOutputPath,
            string pathDelimiter,
            CodeGenLanguage codeGenLanguage)
        {
            ContractABI = contractABI;
            ContractName = contractName;
            ByteCode = byteCode;
            BaseNamespace = baseNamespace;
            ServiceNamespace = serviceNamespace;
            CQSNamespace = cqsNamespace;
            DTONamespace = dtoNamespace;
            BaseOutputPath = baseOutputPath?.TrimEnd(pathDelimiter.ToCharArray());
            PathDelimiter = pathDelimiter;
            CodeGenLanguage = codeGenLanguage;
            if (BaseOutputPath != null)
            {
                if (BaseOutputPath.LastIndexOf(PathDelimiter) > 0)
                {
                     ProjectName = BaseOutputPath.Substring(
                        BaseOutputPath.LastIndexOf(PathDelimiter) + PathDelimiter.Length);
                }
            }
        }

        public GeneratedFile[] GenerateAllMessagesFileAndService()
        {
            var generated = new List<GeneratedFile>();
            generated.Add(GenerateAllMessages());
            generated.Add(GenerateService(singleMessagesFile:true));
            return generated.ToArray();
        }

        public GeneratedFile GenerateAllMessages()
        {
            var cqsFullNamespace = GetFullNamespace(CQSNamespace);
            var cqsFullPath = GetFullPath(CQSNamespace); ;

            var generators = new List<IClassGenerator>();
            generators.Add(GetCQSMessageDeploymentGenerator());
            generators.AddRange(GetAllCQSFunctionMessageGenerators());
            generators.AddRange(GetllEventDTOGenerators());
            generators.AddRange(GetAllFunctionDTOsGenerators());
            //using the same namespace..
            var mainGenerator = new AllMessagesGenerator(generators, ContractName, cqsFullNamespace, CodeGenLanguage);
            return mainGenerator.GenerateFileContent(cqsFullPath);
        }

        public GeneratedFile[] GenerateAll()
        {
            var generated = new List<GeneratedFile>();
            generated.AddRange(GenerateAllCQSMessages());
            generated.AddRange(GenerateAllEventDTOs());
            generated.AddRange(GenerateAllFunctionDTOs());
            generated.Add(GenerateService());
            return generated.ToArray();
        }

        public GeneratedFile GenerateService(bool singleMessagesFile = false)
        {
            var dtoFullNamespace = GetFullNamespace(DTONamespace);
            var cqsFullNamespace = GetFullNamespace(CQSNamespace);

            dtoFullNamespace = singleMessagesFile ? string.Empty : FullyQualifyNamespaceFromImport(dtoFullNamespace);
            cqsFullNamespace = FullyQualifyNamespaceFromImport(cqsFullNamespace);

            var serviceFullNamespace = GetFullNamespace(ServiceNamespace);
            var serviceFullPath = GetFullPath(ServiceNamespace);
            var serviceGenerator = new ServiceGenerator(ContractABI, ContractName, ByteCode, serviceFullNamespace, cqsFullNamespace, dtoFullNamespace, CodeGenLanguage);
            return serviceGenerator.GenerateFileContent(serviceFullPath);
        }

        public List<GeneratedFile> GenerateAllCQSMessages()
        {
            var generated = new List<GeneratedFile>();
            generated.Add(GeneratCQSMessageDeployment());
            generated.AddRange(GeneratCQSFunctionMessages());
            return generated;
        }

        public List<GeneratedFile> GenerateAllFunctionDTOs()
        {
            var generators = GetAllFunctionDTOsGenerators();
            var dtoFullPath = GetFullPath(DTONamespace);
            var generated = new List<GeneratedFile>();
            foreach (var generator in generators)
            {
                GenerateAndAdd(generated, () => generator.GenerateFileContent(dtoFullPath));
            }
            return generated;
        }

        public List<FunctionOutputDTOGenerator> GetAllFunctionDTOsGenerators()
        {
            var dtoFullNamespace = GetFullNamespace(DTONamespace);
            var generators = new List<FunctionOutputDTOGenerator>();
            foreach (var functionABI in ContractABI.Functions)
            {
                var functionOutputDTOGenerator = new FunctionOutputDTOGenerator(functionABI, dtoFullNamespace, CodeGenLanguage);
                generators.Add(functionOutputDTOGenerator);
            }
            return generators;
        }

        public List<GeneratedFile> GenerateAllEventDTOs()
        {
            var generators = GetllEventDTOGenerators();
            var dtoFullPath = GetFullPath(DTONamespace);
            var generated = new List<GeneratedFile>();
            foreach (var generator in generators)
            {
                GenerateAndAdd(generated, () => generator.GenerateFileContent(dtoFullPath));
            }
            return generated;
        }

        public List<EventDTOGenerator> GetllEventDTOGenerators()
        {
            var dtoFullNamespace = GetFullNamespace(DTONamespace);
            var generators = new List<EventDTOGenerator>();
            foreach (var eventABI in ContractABI.Events)
            {
                var generator = new EventDTOGenerator(eventABI, dtoFullNamespace, CodeGenLanguage);
                generators.Add(generator);
            }
            return generators;
        }

        public List<GeneratedFile> GeneratCQSFunctionMessages()
        {
            var generators = GetAllCQSFunctionMessageGenerators();
            var cqsFullPath = GetFullPath(CQSNamespace);;
            var generated = new List<GeneratedFile>();
            foreach (var generator in generators)
            {
                   GenerateAndAdd(generated, () => generator.GenerateFileContent(cqsFullPath));
            }
            return generated;
        }

        public List<FunctionCQSMessageGenerator> GetAllCQSFunctionMessageGenerators()
        {
            var cqsFullNamespace = GetFullNamespace(CQSNamespace);

            var dtoFullNamespace = GetFullNamespace(DTONamespace);

            dtoFullNamespace = FullyQualifyNamespaceFromImport(dtoFullNamespace);

            var generators = new List<FunctionCQSMessageGenerator>();
            foreach (var functionAbi in ContractABI.Functions)
            {
                var cqsGenerator = new FunctionCQSMessageGenerator(functionAbi, cqsFullNamespace, dtoFullNamespace, CodeGenLanguage);
                generators.Add(cqsGenerator);
            }
            return generators;
        }


        public bool AddRootNamespaceOnVbProjectsToImportStatements { get; set; } = true;

        private string FullyQualifyNamespaceFromImport(string @namespace)
        {
            if (CodeGenLanguage == CodeGenLanguage.Vb && AddRootNamespaceOnVbProjectsToImportStatements)
                @namespace = $"{ProjectName}.{@namespace}";
            return @namespace;
        }

        public ContractDeploymentCQSMessageGenerator GetCQSMessageDeploymentGenerator()
        {
            var cqsFullNamespace = GetFullNamespace(CQSNamespace);

            return new ContractDeploymentCQSMessageGenerator(
                ContractABI.Constructor,
                cqsFullNamespace,
                ByteCode,
                ContractName,
                CodeGenLanguage);
        }

        public GeneratedFile GeneratCQSMessageDeployment()
        {
            var cqsGenerator = GetCQSMessageDeploymentGenerator();

           return cqsGenerator.GenerateFileContent(GetFullPath(CQSNamespace));
        }

        public string GetFullNamespace(string @namespace)
        {
            if (string.IsNullOrEmpty(BaseNamespace)) return @namespace;
            return BaseNamespace + "." + @namespace.TrimStart('.');
        }

        public string GetFullPath(string @namespace)
        {
            return BaseOutputPath 
                + PathDelimiter 
                + @namespace.Replace(".", PathDelimiter);
        }

        private void GenerateAndAdd(List<GeneratedFile> generated, Func<GeneratedFile> generator)
        {
            var generatedFile = generator();
            if (generatedFile != null)
            {
                generated.Add(generatedFile);
            }
        }
    }
}
