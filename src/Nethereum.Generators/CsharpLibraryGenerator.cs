using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Generators.Core;

namespace Nethereum.Generators
{
    public class CsharpLibraryGenerator
    {
        public string ProjectFileName { get; }

        public CsharpLibraryGenerator(string projectFileName)
        {
            ProjectFileName = projectFileName;
        }

        public GeneratedFile GenerateFileContent(string outputPath)
        {
            return  new GeneratedFile(template, ProjectFileName, outputPath);
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
