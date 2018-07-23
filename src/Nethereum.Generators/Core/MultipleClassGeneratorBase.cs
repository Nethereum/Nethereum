using System;
using Nethereum.Generators.CQS;

namespace Nethereum.Generators.Core
{
    public class MultipleClassGeneratorBase<TMultipleClassFileTemplate, TMultipleClassFileModel> : IFileGenerator
        where TMultipleClassFileTemplate : MultipleClassFileTemplate
        where TMultipleClassFileModel : FileModel

    {
        protected TMultipleClassFileTemplate Template { get; set; }
        protected TMultipleClassFileModel Model { get; set; }

        public GeneratedFile GenerateFileContent(string outputPath)
        {
            var fileContent = GenerateFileContent();
            return fileContent == null ? null : new GeneratedFile(fileContent, GetFileName(), outputPath);
        }

        public virtual string GenerateFileContent()
        {
            return Template.GenerateFile();
        }

        public string GetFileName()
        {
            return Model.GetFileName();
        }

        public virtual string GenerateClass()
        {
            throw new Exception("Not supported");
        }
    }
}