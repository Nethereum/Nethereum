using Nethereum.Generators.Core;

namespace Nethereum.Generators
{
    public class NetStandardLibraryGenerator
    {
        public string ProjectFileName { get; }
        public CodeGenLanguage CodeGenLanguage { get; }

        private string _languageBasedPropertyGroups;

        public NetStandardLibraryGenerator(string projectFileName, CodeGenLanguage codeGenLanguage)
        {
            // NOTE: This breaks javascript don't use the extension
            ProjectFileName = CodeGenLanguageExt.AddProjectFileExtension(codeGenLanguage, projectFileName);
            CodeGenLanguage = codeGenLanguage;
            _languageBasedPropertyGroups = CodeGenLanguage == CodeGenLanguage.Vb
                ? "<RootNamespace></RootNamespace>"
                : string.Empty;
        }

        public GeneratedFile GenerateFileContent(string outputPath)
        {
            var template = CreateTemplate(_languageBasedPropertyGroups);
            return new GeneratedFile(template, ProjectFileName, outputPath);
        }

        public string NethereumWeb3Version { get; set; } = "4.*";

        private string CreateTemplate(string languageDependentProperty)
        {
            return
                $@"{SpaceUtils.NoTabs}<Project Sdk=""Microsoft.NET.Sdk"">
{SpaceUtils.NoTabs}
{SpaceUtils.One__Tab}<PropertyGroup>
{SpaceUtils.Two___Tabs}<TargetFramework>netstandard2.0</TargetFramework>
{SpaceUtils.Two___Tabs}{languageDependentProperty}
{SpaceUtils.One__Tab}</PropertyGroup>
{SpaceUtils.NoTabs}
{SpaceUtils.One__Tab}<ItemGroup>
{SpaceUtils.Two___Tabs}<PackageReference Include = ""Nethereum.Web3"" Version=""{NethereumWeb3Version}"" />
{SpaceUtils.One__Tab}</ItemGroup>
{SpaceUtils.One__Tab}
{SpaceUtils.NoTabs}</Project>";
        }
    }



    public class MudCodeGenerator
    {

    }
}
