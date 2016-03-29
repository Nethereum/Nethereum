using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BuildProjectFile
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			var stringBuilder = new System.Text.StringBuilder ();
			stringBuilder.Append(CreateOutputFolder ("..\\src\\Nethereum.Hex", "Hex"));
			stringBuilder.Append(CreateOutputFolder ("..\\src\\Nethereum.ABI", "ABI"));
			stringBuilder.Append(CreateOutputFolder ("..\\src\\Nethereum.RPC", "RPC"));
			stringBuilder.Append(CreateOutputFolder ("..\\src\\Nethereum.Web3", "Web3"));
			stringBuilder.Append(CreateOutputFolder ("..\\src\\Nethereum.JsonRpc.Client", "NethereumJsonRpc"));
			stringBuilder.Append(CreateOutputFolder ("JsonRpc.Router\\src\\JsonRpc.Core", "JsonRpc.Core"));
			stringBuilder.Append(CreateOutputFolder ("JsonRpc.Router\\src\\JsonRpc.Client", "JsonRpc.Client"));
            stringBuilder.Append(CreateOutputFolder("Netherum.Maker\\Nethereum.Maker", "Maker"));

            var fileOutputPath = buildPath + "Nethereum-XS.csproj";
			if (File.Exists (fileOutputPath))
				File.Delete (fileOutputPath);
			
			var outputFile = File.CreateText(fileOutputPath);
			outputFile.Write (fileTemplate1 + stringBuilder.ToString () + fileTemplate2);
			outputFile.Flush ();
			outputFile.Close ();

		}

		static string[] excludeFiles  = new []{"*AssemblyInfo.cs"};

		//hack to match our path.. make it empty and you are at the root of the solution.
		static string buildPath = "..\\..\\..\\";

		static string itemTemplate = @"
        <Compile Include=""{0}"">
				<Link>{1}</Link>
		</Compile>
		";

		public static string CreateOutputFolder(string folderPath, string prefix){
			
			var stringBuilder = new System.Text.StringBuilder ();
			var files = Directory.GetFiles (buildPath + folderPath, "*.cs", SearchOption.AllDirectories);

			foreach (var excludeFileSet in excludeFiles) {
				var excludeFileSetNames = Directory.GetFiles(buildPath + folderPath, excludeFileSet, System.IO.SearchOption.AllDirectories);
				files = files.Except(excludeFileSetNames).ToArray();
			}

			files = files.Where(x => !x.Contains("\\bin\\") && !x.Contains("\\obj\\")).ToArray();

			foreach (var file in files) {

				    var filePathProject = file.Substring (buildPath.Length);
					var linkPathProject = prefix + file.Substring (buildPath.Length + folderPath.Length);
					stringBuilder.AppendFormat (itemTemplate, filePathProject, linkPathProject);

			}

			return stringBuilder.ToString ();
		}


static string fileTemplate1 =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project DefaultTargets=""Build"" ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
    <ProjectGuid>{590E4431-4EA0-42A3-87FF-4F748C9FD514}</ProjectGuid>
    <OutputType>Library</OutputType>
    <RootNamespace>Nethereum</RootNamespace>
    <AssemblyName>Nethereum</AssemblyName>
    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug</OutputPath>
    <DefineConstants>DEBUG;</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
    <ConsolePause>false</ConsolePause>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
    <DebugType>full</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release</OutputPath>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
    <ConsolePause>false</ConsolePause>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""System"" />
    <Reference Include=""crypto"">
      <HintPath>packages\Portable.BouncyCastle.1.8.1\lib\net45\crypto.dll</HintPath>
    </Reference>
    <Reference Include=""Newtonsoft.Json"">
      <HintPath>packages\Newtonsoft.Json.8.0.2\lib\net45\Newtonsoft.Json.dll</HintPath>
    </Reference>
    <Reference Include=""System.Net.Http"" />
    <Reference Include=""System.Numerics"" />
    <Reference Include=""Microsoft.CSharp"" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include=""Properties\AssemblyInfo.cs"" />";
		static string fileTemplate2 = @"
  </ItemGroup>
  <Import Project=""$(MSBuildBinPath)\Microsoft.CSharp.targets"" />
  <Import Project=""packages\Microsoft.Net.Compilers.1.2.0-rc\build\Microsoft.Net.Compilers.props"" Condition=""Exists('packages\Microsoft.Net.Compilers.1.2.0-rc\build\Microsoft.Net.Compilers.props')"" />
  <ItemGroup>
    <None Include=""packages.config"" />
  </ItemGroup>
</Project>		
";

	}
}
