namespace Nethereum.Generators.Core
{
    public class ClassGeneratorBase<TClassTemplate, TClassModel> : IFileGenerator
        where TClassModel : TypeMessageModel
        where TClassTemplate : IClassTemplate
        
    {
        protected TClassTemplate ClassTemplate { get; set; }
        protected TClassModel ClassModel { get; set; }

        public GeneratedClass GenerateFileContent(string outputPath)
        {
            return new GeneratedClass(GenerateFileContent(), GetFileName(), outputPath);
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