using Nethereum.Generators.Core;

namespace Nethereum.Generators
{
    public class NetStandardLibraryGenerator
    {
        public string ProjectFileName { get; }
        public CodeGenLanguage CodeGenLanguage { get; }

        public NetStandardLibraryGenerator(string projectFileName, CodeGenLanguage codeGenLanguage)
        {
            // NOTE: This breaks javascript don't use the extension
            ProjectFileName = CodeGenLanguageExt.AddProjectFileExtension(codeGenLanguage, projectFileName);
            CodeGenLanguage = codeGenLanguage;
        }

        public GeneratedFile GenerateFileContent(string outputPath)
        {
            return new GeneratedFile(template, ProjectFileName, outputPath);
        }

        private string template =
 $@"{SpaceUtils.NoTabs}<Project Sdk=""Microsoft.NET.Sdk"">
{SpaceUtils.NoTabs}
{SpaceUtils.OneTab}<PropertyGroup>
{SpaceUtils.TwoTabs}<TargetFramework>netstandard2.0</TargetFramework>
{SpaceUtils.OneTab}</PropertyGroup>
{SpaceUtils.NoTabs}
{SpaceUtils.OneTab}<ItemGroup>
{SpaceUtils.TwoTabs}<PackageReference Include = ""Nethereum.Web3"" Version=""2.4.0"" />
{SpaceUtils.OneTab}</ItemGroup>
{SpaceUtils.OneTab}
{SpaceUtils.NoTabs}</Project>";
    }
}
