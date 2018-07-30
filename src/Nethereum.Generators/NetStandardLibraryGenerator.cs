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

        public string NethereumWeb3Version { get; set; } = "3.*";

        private string CreateTemplate(string languageDependentProperty)
        {
            return
                $@"{SpaceUtils.NoTabs}<Project Sdk=""Microsoft.NET.Sdk"">
{SpaceUtils.NoTabs}
{SpaceUtils.OneTab}<PropertyGroup>
{SpaceUtils.TwoTabs}<TargetFramework>netstandard2.0</TargetFramework>
{SpaceUtils.TwoTabs}{languageDependentProperty}
{SpaceUtils.OneTab}</PropertyGroup>
{SpaceUtils.NoTabs}
{SpaceUtils.OneTab}<ItemGroup>
{SpaceUtils.TwoTabs}<PackageReference Include = ""Nethereum.Web3"" Version=""{NethereumWeb3Version}"" />
{SpaceUtils.OneTab}</ItemGroup>
{SpaceUtils.OneTab}
{SpaceUtils.NoTabs}</Project>";
        }
    }
}
