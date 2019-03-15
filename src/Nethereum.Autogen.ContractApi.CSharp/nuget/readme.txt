
The nuget package supports both ABI driven code generation and config file driven code generation.
Where a contract name exists in both a config file and an abi file - the config file will be chosen as the target for code generation.

ABI file driven code generation (default)
===============================

During pre build, all abi files in the target project will be found and dot net code will be generated based on them.
Namespaces and output folders will be based on the assembly name of the target project.
The contract name is derived from the file name of the abi. 
The code generation language is based on the target project (csproj = csharp, vbproj = vb, fsproj = f#)
If .bin files (Solidity byte code) are present - then by convention they should be in the same folder as the abi and have the same name as the abi (e.g. Contract1.abi, Contract1.bin)

Config file driven code generation (optional)
==================================
For more control over code generation - or where the abi files are not in the target project.
To enable: add an json file called Nethereum.Generator.json to the root of the target project.
Example content is shown below (with only the mandatory values)
Multiple ABIConfiguration elements can be added

{
	"ABIConfigurations": [
	{
		"ContractName":"StandardContractA",
		"ABI":null,
		"ABIFile":"Ballot.abi",
		"ByteCode":null,
		"BinFile":"Ballot.bin",
		"BaseNamespace":null,
		"CQSNamespace":null,
		"DTONamespace":null,
		"ServiceNamespace":null,
		"CodeGenLanguage":"CSharp",
		"BaseOutputPath":null}
	]
}
Preventing Code Generation
==========================
If the abi contracts are not changing often, it may make sense to prevent auto code generation during each build as it will only generate identical files and take time.
To Prevent all Auto Generation of code during pre build, add the following property to the target project file.

  <PropertyGroup>
    <NethereumGenerateCode>false</NethereumGenerateCode>
  </PropertyGroup>