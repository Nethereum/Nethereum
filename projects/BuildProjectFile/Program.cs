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
            stringBuilder.Append(CreateOutputFolder("..\\src\\Nethereum.StandardTokenEIP20", "EIP20"));
            stringBuilder.Append(CreateOutputFolder ("..\\src\\Nethereum.JsonRpc.Client", "NethereumJsonRpc"));
            stringBuilder.Append(CreateOutputFolder("..\\src\\Nethereum.JsonRpc.RpcClient", "NethereumJsonRpcClient"));
            stringBuilder.Append(CreateOutputFolder ("..\\src\\Nethereum.KeyStore", "KeyStore"));
            stringBuilder.Append(CreateOutputFolder("..\\src\\Nethereum.Quorum", "Quorum"));
            stringBuilder.Append(CreateOutputFolder("..\\src\\Nethereum.ENS", "ENS"));
            stringBuilder.Append(CreateOutputFolder("..\\src\\Nethereum.Geth", "Geth"));
            stringBuilder.Append(CreateOutputFolder("..\\src\\Nethereum.Contracts", "Contracts"));
            stringBuilder.Append(CreateOutputFolder("..\\src\\Nethereum.Util", "Util"));
            stringBuilder.Append(CreateOutputFolder("..\\src\\Nethereum.Signer", "Signer"));
            stringBuilder.Append(CreateOutputFolder("..\\src\\Nethereum.RLP", "RLP"));
            stringBuilder.Append(CreateOutputFolder("..\\src\\Nethereum.Uport", "Uport"));

            //stringBuilder.Append(CreateOutputFolder ("JsonRpc.Router\\src\\JsonRpc.Core", "JsonRpc.Core"));
            //stringBuilder.Append(CreateOutputFolder ("JsonRpc.Router\\src\\JsonRpc.Client", "JsonRpc.Client"));
            //GenerateFile("Nethereum-XS\\Nethereum - XS.csproj", fileTemplate1, fileTemplate2, stringBuilder.ToString());
            GenerateFile("Nethereum.Portable\\Nethereum.Portable.csproj", fileTemplatePortable1, fileTemplatePortable2, stringBuilder.ToString());
        }

        public static void GenerateFile(string path, string template1, string template2, string content)
        {
            var fileOutputPath = buildPath + path;
            if (File.Exists(fileOutputPath))
                File.Delete(fileOutputPath);

            var outputFile = File.CreateText(fileOutputPath);
            outputFile.Write(template1 + content + template2);
            outputFile.Flush();
            outputFile.Close();
        }
        
		static string[] excludeFiles  = new []{"*AssemblyInfo.cs"};

		//hack to match our path.. make it empty and you are at the root of the solution.
		static string buildPath = "..\\..\\..\\";
        static string relativePathProject = "..\\"; //our projects are in a subdirectory

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

                    //Build project files execution is at bin, we get a root of solution and navigate a relativePath down to subfolder.
				    var filePathProject = relativePathProject + file.Substring (buildPath.Length);
					var linkPathProject = prefix + file.Substring (buildPath.Length + folderPath.Length);
					stringBuilder.AppendFormat (itemTemplate, filePathProject, linkPathProject);

			}

			return stringBuilder.ToString ();
		}

        static string fileTemplatePortable1 =
        @"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""14.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
  <PropertyGroup>
    <MinimumVisualStudioVersion>10.0</MinimumVisualStudioVersion>
    <Configuration Condition="" '$(Configuration)' == '' "">Debug</Configuration>
    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
    <ProjectGuid>{0F8DA38C-5C65-4545-BFF0-C40EC0CE40B4}</ProjectGuid>
    <OutputType>Library</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>Nethereum.Portable</RootNamespace>
    <AssemblyName>Nethereum.Portable</AssemblyName>
    <DefaultLanguage>en-US</DefaultLanguage>
    <FileAlignment>512</FileAlignment>
    <ProjectTypeGuids>{786C830F-07A1-408B-BD7F-6EE04809D6DB};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids>
    <TargetFrameworkProfile>Profile111</TargetFrameworkProfile>
    <TargetFrameworkVersion>v4.5</TargetFrameworkVersion>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' "">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE;PCL</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE;PCL</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <!-- A reference to the entire .NET Framework is automatically included -->
    <None Include=""project.json"" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include=""Properties\AssemblyInfo.cs"" />";
        static string fileTemplatePortable2 = @"
        </ItemGroup>
  <Import Project=""$(MSBuildExtensionsPath32)\Microsoft\Portable\$(TargetFrameworkVersion)\Microsoft.Portable.CSharp.targets"" />
  <!-- To modify your build process, add your task inside one of the targets below and uncomment it. 
       Other similar extension points exist, see Microsoft.Common.targets.
  <Target Name=""BeforeBuild"">
  </Target>
  <Target Name=""AfterBuild"">
  </Target>
  -->
</Project>";


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
