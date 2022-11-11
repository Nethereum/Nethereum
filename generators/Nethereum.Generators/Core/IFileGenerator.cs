namespace Nethereum.Generators.Core
{
    public interface IFileGenerator: IGenerator
    {
        GeneratedFile GenerateFileContent(string outputPath);
        string GenerateFileContent();
        string GetFileName();
    }
}