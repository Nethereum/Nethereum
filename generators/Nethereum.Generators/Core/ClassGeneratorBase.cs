namespace Nethereum.Generators.Core
{
    public class ClassGeneratorBase<TMessageModel>: IFileGenerator, IClassGenerator
        where TMessageModel : TypeMessageModel
    {
        protected IClassTemplate ClassTemplate { get; set; }
        public TMessageModel ClassModel { get; set; }

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