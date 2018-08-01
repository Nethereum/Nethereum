using System.IO;
using System.Linq;
using System.Text;

namespace Nethereum.Generators.BuildDuoProjectFile
{
    internal class MainClass
    {
        private static readonly string[] excludeFiles = {"*AssemblyInfo.cs"};

        //hack to match our path.. make it empty and you are at the root of the solution.
        private static readonly string buildPath = "..\\..\\..\\";

        private static readonly string relativePathProject = "..\\"; //our projects are in a subdirectory

        private static readonly string itemTemplate = @"
        <Compile Include=""{0}"">
				<Link>{1}</Link>
		</Compile>
		";

        private static readonly string FileTemplateStart =
                @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""14.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
    <ProjectGuid>{EB8AD30C-1914-42C9-9E10-A810B2CA296B}</ProjectGuid>
    <OutputType>Library</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>Nethereum.Generators.DuoCode</RootNamespace>
    <AssemblyName>Nethereum.Generators.DuoCode</AssemblyName>
    <TargetFrameworkVersion>v4.6.1</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
    <DisableHandlePackageFileConflicts>true</DisableHandlePackageFileConflicts>
    <NuGetPackageImportStamp>
    </NuGetPackageImportStamp>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>scripts\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>scripts\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition=""'$(Configuration)' == 'Debug'"">
   <DuoCodeDtsMode>es6</DuoCodeDtsMode>
   <DuoCodeModuleKind>commonjs</DuoCodeModuleKind>
   </PropertyGroup>
  <ItemGroup>"
            ;

        private static readonly string FileTemplateEnd = @"
  </ItemGroup>
  <ItemGroup>
    <None Include=""packages.config"" />
  </ItemGroup>
  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
  <Import Project=""..\..\packages\DuoCode.3.0.1654.0\build\DuoCode.targets"" Condition=""Exists('..\..\packages\DuoCode.3.0.1654.0\build\DuoCode.targets')"" />
  <Target Name=""EnsureNuGetPackageBuildImports"" BeforeTargets=""PrepareForBuild"">
    <PropertyGroup>
      <ErrorText>This project references NuGet package(s) that are missing on this computer. Use NuGet Package Restore to download them.  For more information, see http://go.microsoft.com/fwlink/?LinkID=322105. The missing file is {0}.</ErrorText>
    </PropertyGroup>
    <Error Condition=""!Exists('..\..\packages\DuoCode.3.0.1654.0\build\DuoCode.targets')"" Text=""$([System.String]::Format('$(ErrorText)', '..\..\packages\DuoCode.3.0.1654.0\build\DuoCode.targets'))"" />
  </Target>
 <PropertyGroup>
    <PostBuildEvent>xcopy ""$(ProjectDir) scripts\Nethereum.Generators.DuoCode.d.ts"" ""$(SolutionDir) src\Nethereum.Generators.JavaScript\"" /Y /I 
        xcopy ""$(ProjectDir)scripts\Nethereum.Generators.DuoCode.js"" ""$(SolutionDir)src\Nethereum.Generators.JavaScript\"" /Y /I 
        xcopy ""$(ProjectDir)scripts\Nethereum.Generators.DuoCode.js.map"" ""$(SolutionDir)src\Nethereum.Generators.JavaScript\"" /Y /I</PostBuildEvent>
  </PropertyGroup>
  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
       Other similar extension points exist, see Microsoft.Common.targets.
  <Target Name=""BeforeBuild"">
  </Target>
  <Target Name=""AfterBuild"">
  </Target>
  -->
</Project>";

        public static void Main(string[] args)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(CreateOutputFolder("Nethereum.Generators", ""));
            GenerateFile("Nethereum.Generators.DuoCode\\Nethereum.Generators.DuoCode.csproj", FileTemplateStart,
                FileTemplateEnd, stringBuilder.ToString());
        }

        public static void GenerateFile(string path, string fileTemplateStart, string fileTemplateEnd
            , string content)
        {
            var fileOutputPath = buildPath + path;
            if (File.Exists(fileOutputPath))
                File.Delete(fileOutputPath);

            var outputFile = File.CreateText(fileOutputPath);
            outputFile.Write(fileTemplateStart + content + fileTemplateEnd);
            outputFile.Flush();
            outputFile.Close();
        }

        public static string CreateOutputFolder(string folderPath, string prefix)
        {
            var stringBuilder = new StringBuilder();
            var files = Directory.GetFiles(buildPath + folderPath, "*.cs", SearchOption.AllDirectories);

            foreach (var excludeFileSet in excludeFiles)
            {
                var excludeFileSetNames =
                    Directory.GetFiles(buildPath + folderPath, excludeFileSet, SearchOption.AllDirectories);
                files = files.Except(excludeFileSetNames).ToArray();
            }

            files = files.Where(x => !x.Contains("\\bin\\") && !x.Contains("\\obj\\")).ToArray();

            foreach (var file in files)
            {
                //Build project files execution is at bin, we get a root of solution and navigate a relativePath down to subfolder.
                var filePathProject = relativePathProject + file.Substring(buildPath.Length);
                var linkPathProject = prefix + file.Substring(buildPath.Length + folderPath.Length);
                stringBuilder.AppendFormat(itemTemplate, filePathProject, linkPathProject);
            }

            return stringBuilder.ToString();
        }
    }
}