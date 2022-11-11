Nethereum.ABI.Autogen

This project contains a dotnet cli for generating dot net code from abi files.
It also contains the configuration to create a nuget package (Nethereum.ABI.Autogen)
This nuget package links to the pre build event of the target project and invokes the cli.

When changing the nuget version number:
	1 - nuget/Nethereum.Autogen.ContractApi.nuspec: Change the version
	2 - nuget/Nethereum.Autogen.ContractApi.targets: Change the version in the PreBuild Target Execute Command paths (there are 2 to change).
