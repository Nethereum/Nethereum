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
            ProjectName = BaseOutputPath?.Split(Path.DirectorySeparatorChar).Last();
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

        public GeneratedFile GenerateService()
        {
            var dtoFullNamespace = GetFullNamespace(DTONamespace);
            var cqsFullNamespace = GetFullNamespace(CQSNamespace);

            dtoFullNamespace = FullyQualifyNamespaceFromImport(dtoFullNamespace);
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
            var dtoFullNamespace = GetFullNamespace(DTONamespace);
            var dtoFullPath = GetFullPath(DTONamespace);
            var generated = new List<GeneratedFile>();
            foreach (var functionABI in ContractABI.Functions)
            {
                var functionOutputDTOGenerator = new FunctionOutputDTOGenerator(functionABI, dtoFullNamespace, CodeGenLanguage);
                GenerateAndAdd(generated, () => functionOutputDTOGenerator.GenerateFileContent(dtoFullPath));
            }
            return generated;
        }

        public List<GeneratedFile> GenerateAllEventDTOs()
        {
            var dtoFullNamespace = GetFullNamespace(DTONamespace);
            var dtoFullPath = GetFullPath(DTONamespace);
            var generated = new List<GeneratedFile>();
            foreach (var eventABI in ContractABI.Events)
            {
                var cqsGenerator = new EventDTOGenerator(eventABI, dtoFullNamespace, CodeGenLanguage);
                GenerateAndAdd(generated, () => cqsGenerator.GenerateFileContent(dtoFullPath));
            }
            return generated;
        }

        public List<GeneratedFile> GeneratCQSFunctionMessages()
        {
            var cqsFullNamespace = GetFullNamespace(CQSNamespace);
            var cqsFullPath = GetFullPath(CQSNamespace);
            var dtoFullNamespace = GetFullNamespace(DTONamespace);

            dtoFullNamespace = FullyQualifyNamespaceFromImport(dtoFullNamespace);

            var generated = new List<GeneratedFile>();
            foreach (var functionAbi in ContractABI.Functions)
            {
                var cqsGenerator = new FunctionCQSMessageGenerator(functionAbi, cqsFullNamespace, dtoFullNamespace, CodeGenLanguage);
                GenerateAndAdd(generated, () => cqsGenerator.GenerateFileContent(cqsFullPath));
            }
            return generated;
        }

        public bool AddRootNamespaceOnVbProjectsToImportStatements { get; set; } = true;

        private string FullyQualifyNamespaceFromImport(string @namespace)
        {
            if (CodeGenLanguage == CodeGenLanguage.Vb && AddRootNamespaceOnVbProjectsToImportStatements)
                @namespace = $"{ProjectName}.{@namespace}";
            return @namespace;
        }

        public GeneratedFile GeneratCQSMessageDeployment()
        {
            var cqsFullNamespace = GetFullNamespace(CQSNamespace);

            var cqsGenerator = new ContractDeploymentCQSMessageGenerator(
                ContractABI.Constructor, 
                cqsFullNamespace,
                ByteCode,
                ContractName, 
                CodeGenLanguage);

           return cqsGenerator.GenerateFileContent(GetFullPath(CQSNamespace));
        }

        public string GetFullNamespace(string @namespace)
        {
            if (string.IsNullOrEmpty(BaseNamespace)) return @namespace;
            return BaseNamespace + "." + @namespace;
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
