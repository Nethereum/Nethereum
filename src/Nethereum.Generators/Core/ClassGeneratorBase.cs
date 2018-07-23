namespace Nethereum.Generators.Core
{
    public class ClassGeneratorBase<TClassTemplate, TClassModel> : IFileGenerator, IClassGenerator
        where TClassModel : TypeMessageModel
        where TClassTemplate : IClassTemplate
        
    {
        protected TClassTemplate ClassTemplate { get; set; }
        protected TClassModel ClassModel { get; set; }

        public GeneratedFile GenerateFileContent(string outputPath)
        {
            var fileContent = GenerateFileContent();
            return fileContent == null ? null : new GeneratedFile(fileContent, GetFileName(), outputPath);
        }

        public virtual string GenerateFileContent()
        {
            return ClassTemplate.GenerateFullClass();
        }

        public string GetFileName()
        {
            return ClassModel.GetFileName();
        }

        public virtual string GenerateClass()
        {
            return ClassTemplate.GenerateClass();
        }
    }
}