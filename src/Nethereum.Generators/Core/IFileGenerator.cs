namespace Nethereum.Generators.Core
{
    public interface IFileGenerator: IGenerator
    {
        GeneratedClass GenerateFileContent(string outputPath);
        string GenerateFileContent();
        string GetFileName();
    }
}