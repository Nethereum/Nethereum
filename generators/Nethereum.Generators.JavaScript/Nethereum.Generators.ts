///<reference path='mscorlib.ts'/>
class ContractProjectGenerator extends NObject
{
	get ContractABI(): ContractABI
	{
		return this._ContractABI_k__BackingField;
	}
	get ContractName(): string
	{
		return this._ContractName_k__BackingField;
	}
	get ByteCode(): string
	{
		return this._ByteCode_k__BackingField;
	}
	get BaseNamespace(): string
	{
		return this._BaseNamespace_k__BackingField;
	}
	get ServiceNamespace(): string
	{
		return this._ServiceNamespace_k__BackingField;
	}
	get CQSNamespace(): string
	{
		return this._CQSNamespace_k__BackingField;
	}
	get DTONamespace(): string
	{
		return this._DTONamespace_k__BackingField;
	}
	get BaseOutputPath(): string
	{
		return this._BaseOutputPath_k__BackingField;
	}
	get PathDelimiter(): string
	{
		return this._PathDelimiter_k__BackingField;
	}
	get CodeGenLanguage(): CodeGenLanguage
	{
		return this._CodeGenLanguage_k__BackingField;
	}
	private get ProjectName(): string
	{
		return this._ProjectName_k__BackingField;
	}
	AddRootNamespaceOnVbProjectsToImportStatements: boolean = false;
	constructor(contractABI: ContractABI, contractName: string, byteCode: string, baseNamespace: string, serviceNamespace: string, cqsNamespace: string, dtoNamespace: string, baseOutputPath: string, pathDelimiter: string, codeGenLanguage: CodeGenLanguage)
	{
		super();
		this._AddRootNamespaceOnVbProjectsToImportStatements_k__BackingField = true;
		super..ctor();
		this._ContractABI_k__BackingField = contractABI;
		this._ContractName_k__BackingField = contractName;
		this._ByteCode_k__BackingField = byteCode;
		this._BaseNamespace_k__BackingField = baseNamespace;
		this._ServiceNamespace_k__BackingField = serviceNamespace;
		this._CQSNamespace_k__BackingField = cqsNamespace;
		this._DTONamespace_k__BackingField = dtoNamespace;
		this._BaseOutputPath_k__BackingField = ((baseOutputPath !== null) ? NString.TrimEnd(baseOutputPath, pathDelimiter.ToCharArray()) : null);
		this._PathDelimiter_k__BackingField = pathDelimiter;
		this._CodeGenLanguage_k__BackingField = codeGenLanguage;
		if (this.BaseOutputPath !== null && this.BaseOutputPath.LastIndexOf(this.PathDelimiter) > 0)
		{
			this._ProjectName_k__BackingField = NString.Substring(this.BaseOutputPath, this.BaseOutputPath.LastIndexOf(this.PathDelimiter) + this.PathDelimiter.length);
		}
	}
	GenerateAllMessagesFileAndService(): GeneratedFile[]
	{
		var expr_05: List<GeneratedFile> = new List<GeneratedFile>();
		expr_05.Add(this.GenerateAllMessages());
		expr_05.Add(this.GenerateService(true));
		return expr_05.ToArray();
	}
	GenerateAllMessages(): GeneratedFile
	{
		var fullNamespace: string = this.GetFullNamespace(this.CQSNamespace);
		var fullPath: string = this.GetFullPath(this.CQSNamespace);
		var expr_1F: List<IClassGenerator> = new List<IClassGenerator>();
		expr_1F.Add(this.GetCQSMessageDeploymentGenerator());
		expr_1F.AddRange(this.GetAllCQSFunctionMessageGenerators());
		expr_1F.AddRange(this.GetllEventDTOGenerators());
		expr_1F.AddRange(this.GetAllFunctionDTOsGenerators());
		return new AllMessagesGenerator(expr_1F, this.ContractName, fullNamespace, this.CodeGenLanguage).GenerateFileContent(fullPath);
	}
	GenerateAll(): GeneratedFile[]
	{
		var expr_05: List<GeneratedFile> = new List<GeneratedFile>();
		expr_05.AddRange(this.GenerateAllCQSMessages());
		expr_05.AddRange(this.GenerateAllEventDTOs());
		expr_05.AddRange(this.GenerateAllFunctionDTOs());
		expr_05.Add(this.GenerateService(false));
		return expr_05.ToArray();
	}
	GenerateService(singleMessagesFile: boolean = false): GeneratedFile
	{
		var text: string = this.GetFullNamespace(this.DTONamespace);
		var text2: string = this.GetFullNamespace(this.CQSNamespace);
		text = (singleMessagesFile ? NString.Empty : this.FullyQualifyNamespaceFromImport(text));
		text2 = this.FullyQualifyNamespaceFromImport(text2);
		var fullNamespace: string = this.GetFullNamespace(this.ServiceNamespace);
		var fullPath: string = this.GetFullPath(this.ServiceNamespace);
		return new ServiceGenerator(this.ContractABI, this.ContractName, this.ByteCode, fullNamespace, text2, text, this.CodeGenLanguage).GenerateFileContent(fullPath);
	}
	GenerateAllCQSMessages(): List<GeneratedFile>
	{
		var expr_05: List<GeneratedFile> = new List<GeneratedFile>();
		expr_05.Add(this.GeneratCQSMessageDeployment());
		expr_05.AddRange(this.GeneratCQSFunctionMessages());
		return expr_05;
	}
	GenerateAllFunctionDTOs(): List<GeneratedFile>
	{
		var arg_24_0: List<FunctionOutputDTOGenerator> = this.GetAllFunctionDTOsGenerators();
		var dtoFullPath: string = this.GetFullPath(this.DTONamespace);
		var list: List<GeneratedFile> = new List<GeneratedFile>();
		var enumerator: List_Enumerator<FunctionOutputDTOGenerator> = arg_24_0.GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				var generator: FunctionOutputDTOGenerator = enumerator.Current;
				this.GenerateAndAdd(list, ()=>{return generator.GenerateFileContent(dtoFullPath);});
			}
		}
		finally
		{
			(<IDisposable>enumerator).Dispose();
		}
		return list;
	}
	GetAllFunctionDTOsGenerators(): List<FunctionOutputDTOGenerator>
	{
		var fullNamespace: string = this.GetFullNamespace(this.DTONamespace);
		var list: List<FunctionOutputDTOGenerator> = new List<FunctionOutputDTOGenerator>();
		var functions: FunctionABI[] = this.ContractABI.Functions;
		for (var i: number = 0; i < functions.length; i = i + 1)
		{
			var item: FunctionOutputDTOGenerator = new FunctionOutputDTOGenerator(functions[i], fullNamespace, this.CodeGenLanguage);
			list.Add(item);
		}
		return list;
	}
	GenerateAllEventDTOs(): List<GeneratedFile>
	{
		var arg_24_0: List<EventDTOGenerator> = this.GetllEventDTOGenerators();
		var dtoFullPath: string = this.GetFullPath(this.DTONamespace);
		var list: List<GeneratedFile> = new List<GeneratedFile>();
		var enumerator: List_Enumerator<EventDTOGenerator> = arg_24_0.GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				var generator: EventDTOGenerator = enumerator.Current;
				this.GenerateAndAdd(list, ()=>{return generator.GenerateFileContent(dtoFullPath);});
			}
		}
		finally
		{
			(<IDisposable>enumerator).Dispose();
		}
		return list;
	}
	GetllEventDTOGenerators(): List<EventDTOGenerator>
	{
		var fullNamespace: string = this.GetFullNamespace(this.DTONamespace);
		var list: List<EventDTOGenerator> = new List<EventDTOGenerator>();
		var events: EventABI[] = this.ContractABI.Events;
		for (var i: number = 0; i < events.length; i = i + 1)
		{
			var item: EventDTOGenerator = new EventDTOGenerator(events[i], fullNamespace, this.CodeGenLanguage);
			list.Add(item);
		}
		return list;
	}
	GeneratCQSFunctionMessages(): List<GeneratedFile>
	{
		var arg_24_0: List<FunctionCQSMessageGenerator> = this.GetAllCQSFunctionMessageGenerators();
		var cqsFullPath: string = this.GetFullPath(this.CQSNamespace);
		var list: List<GeneratedFile> = new List<GeneratedFile>();
		var enumerator: List_Enumerator<FunctionCQSMessageGenerator> = arg_24_0.GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				var generator: FunctionCQSMessageGenerator = enumerator.Current;
				this.GenerateAndAdd(list, ()=>{return generator.GenerateFileContent(cqsFullPath);});
			}
		}
		finally
		{
			(<IDisposable>enumerator).Dispose();
		}
		return list;
	}
	GetAllCQSFunctionMessageGenerators(): List<FunctionCQSMessageGenerator>
	{
		var fullNamespace: string = this.GetFullNamespace(this.CQSNamespace);
		var text: string = this.GetFullNamespace(this.DTONamespace);
		text = this.FullyQualifyNamespaceFromImport(text);
		var list: List<FunctionCQSMessageGenerator> = new List<FunctionCQSMessageGenerator>();
		var functions: FunctionABI[] = this.ContractABI.Functions;
		for (var i: number = 0; i < functions.length; i = i + 1)
		{
			var item: FunctionCQSMessageGenerator = new FunctionCQSMessageGenerator(functions[i], fullNamespace, text, this.CodeGenLanguage);
			list.Add(item);
		}
		return list;
	}
	private FullyQualifyNamespaceFromImport(namespace: string): string
	{
		if (this.CodeGenLanguage === CodeGenLanguage.Vb && this.AddRootNamespaceOnVbProjectsToImportStatements)
		{
			namespace = this.ProjectName + "." + namespace;
		}
		return namespace;
	}
	GetCQSMessageDeploymentGenerator(): ContractDeploymentCQSMessageGenerator
	{
		var fullNamespace: string = this.GetFullNamespace(this.CQSNamespace);
		return new ContractDeploymentCQSMessageGenerator(this.ContractABI.Constructor, fullNamespace, this.ByteCode, this.ContractName, this.CodeGenLanguage);
	}
	GeneratCQSMessageDeployment(): GeneratedFile
	{
		return this.GetCQSMessageDeploymentGenerator().GenerateFileContent(this.GetFullPath(this.CQSNamespace));
	}
	GetFullNamespace(namespace: string): string
	{
		if (NString.IsNullOrEmpty(this.BaseNamespace))
		{
			return namespace;
		}
		return this.BaseNamespace + "." + NString.TrimStart(namespace, [
			46
		]/*'.'*/);
	}
	GetFullPath(namespace: string): string
	{
		return this.BaseOutputPath + this.PathDelimiter + NString.Replace(namespace, ".", this.PathDelimiter);
	}
	private GenerateAndAdd(generated: List<GeneratedFile>, generator: () => GeneratedFile): void
	{
		var generatedFile: GeneratedFile = generator();
		if (generatedFile !== null)
		{
			generated.Add(generatedFile);
		}
	}
}
class NetStandardLibraryGenerator extends NObject
{
	private _languageBasedPropertyGroups: string = null;
	get ProjectFileName(): string
	{
		return this._ProjectFileName_k__BackingField;
	}
	get CodeGenLanguage(): CodeGenLanguage
	{
		return this._CodeGenLanguage_k__BackingField;
	}
	NethereumWeb3Version: string = null;
	constructor(projectFileName: string, codeGenLanguage: CodeGenLanguage)
	{
		super();
		this._NethereumWeb3Version_k__BackingField = "3.0.0";
		super..ctor();
		this._ProjectFileName_k__BackingField = CodeGenLanguageExt.AddProjectFileExtension(codeGenLanguage, projectFileName);
		this._CodeGenLanguage_k__BackingField = codeGenLanguage;
		this._languageBasedPropertyGroups = ((this.CodeGenLanguage === CodeGenLanguage.Vb) ? "<RootNamespace></RootNamespace>" : NString.Empty);
	}
	GenerateFileContent(outputPath: string): GeneratedFile
	{
		return new GeneratedFile(this.CreateTemplate(this._languageBasedPropertyGroups), this.ProjectFileName, outputPath);
	}
	private CreateTemplate(languageDependentProperty: string): string
	{
		return NString.Concat([
			SpaceUtils.NoTabs, "<Project Sdk=\"Microsoft.NET.Sdk\">\r\n", SpaceUtils.NoTabs, "\r\n", SpaceUtils.OneTab, "<PropertyGroup>\r\n", SpaceUtils.TwoTabs, "<TargetFramework>netstandard2.0</TargetFramework>\r\n", SpaceUtils.TwoTabs, languageDependentProperty, "\r\n", SpaceUtils.OneTab, "</PropertyGroup>\r\n", SpaceUtils.NoTabs, "\r\n", SpaceUtils.OneTab, "<ItemGroup>\r\n", SpaceUtils.TwoTabs, "<PackageReference Include = \"Nethereum.Web3\" Version=\"", 
			this.NethereumWeb3Version, "\" />\r\n", SpaceUtils.OneTab, "</ItemGroup>\r\n", SpaceUtils.OneTab, "\r\n", SpaceUtils.NoTabs, "</Project>"
		]);
	}
}
interface IClassTemplate
{
	GenerateClass(): string;
	GenerateFullClass(): string;
}
class ClassTemplateBase<TModel> extends NObject implements IClassTemplate
{
	ClassFileTemplate: ClassFileTemplate = null;
	get Model(): TModel
	{
		return this._Model_k__BackingField;
	}
	GenerateClass(): string
	{
		throw new NotSupportedException();
	}
	constructor(model: TModel)
	{
		super();
		this._Model_k__BackingField = model;
	}
	GenerateFullClass(): string
	{
		return this.ClassFileTemplate.GenerateFullClass();
	}
}
class SimpleTestCSharpTemplate extends ClassTemplateBase<SimpleTestModel>
{
	constructor(model: SimpleTestModel)
	{
		super(model);
		this.ClassFileTemplate = new CSharpClassFileTemplate(this.Model, this);
	}
	GenerateClass(): string
	{
		return NString.Concat([
			"\r\n", SpaceUtils.OneTab, "public class ", this.Model.GetTypeName(), ": NethereumIntegrationTest \r\n", SpaceUtils.OneTab, "{\r\n", SpaceUtils.OneTab, "\r\n", SpaceUtils.TwoTabs, "// ITestOutputHelper outputs information using XUnit\r\n", SpaceUtils.TwoTabs, "// The default account (private key) for Nethereum testing is used as the parameter \"DefaultTestAccountConstants.PrivateKey\", \r\n", SpaceUtils.TwoTabs, "// you can use any of the preconfigured TestChains with that account for testing https://github.com/Nethereum/TestChains\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "public ", 
			this.Model.GetTypeName(), "(ITestOutputHelper xunitTestOutputHelper) : base(\"http://localhost:8545\",\r\n", SpaceUtils.ThreeTabs, "DefaultTestAccountConstants.PrivateKey, new NethereumTestDebugLogger(new XunitOutputWriter(xunitTestOutputHelper)))\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.TwoTabs, "}\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "/// *** UNIT TEST SAMPLE USING ERC20 Standard Token *** //\r\n", 
			SpaceUtils.TwoTabs, "[Theory]\r\n", SpaceUtils.TwoTabs, "[InlineData(10000)]\r\n", SpaceUtils.TwoTabs, "[InlineData(5000)]\r\n", SpaceUtils.TwoTabs, "[InlineData(300)]\r\n", SpaceUtils.TwoTabs, "public async Task AfterDeployment_BalanceOwner_ShouldBeTheSameAsInitialSupply(int initialSupply)\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "// Constructing the default deployment message with token name, decimal units\r\n", SpaceUtils.ThreeTabs, "var contractDeploymentDefault = GetDeploymentMessage();\r\n", SpaceUtils.ThreeTabs, "// Setting the supply to the theory\r\n", SpaceUtils.ThreeTabs, "contractDeploymentDefault.InitialAmount = initialSupply;\r\n", 
			SpaceUtils.ThreeTabs, "// Given that we deploy the smart contract \r\n", SpaceUtils.ThreeTabs, "GivenADeployedContract(contractDeploymentDefault);\r\n", SpaceUtils.ThreeTabs, "// Set up the expectation to be the balance of the owner the same as the initial supply\r\n", SpaceUtils.ThreeTabs, "var balanceOfExpectedResult = new BalanceOfOutputDTO() { Balance = initialSupply };\r\n", SpaceUtils.ThreeTabs, "// when querying the smart contract to get the the balance of the owner we will expect the result to be same as the intial supply\r\n", SpaceUtils.ThreeTabs, "WhenQueryingThen(SimpleStandardContractTest.GetBalanceOfOwnerMessage(), balanceOfExpectedResult);\r\n", SpaceUtils.TwoTabs, "}\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "[Theory]\r\n", SpaceUtils.TwoTabs, "[InlineData(10000)]\r\n", 
			SpaceUtils.TwoTabs, "[InlineData(5000)]\r\n", SpaceUtils.TwoTabs, "[InlineData(300)]\r\n", SpaceUtils.TwoTabs, "public async Task Transfering_ShouldIncreaseTheBalanceOfReceiver(int valueToSend)\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "var contractDeploymentDefault = SimpleStandardContractTest.GetDeploymentMessage();\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, "Assert.False(valueToSend > contractDeploymentDefault.InitialAmount, \"value to send is bigger than the total supply, please adjust the test data\");\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, "GivenADeployedContract(contractDeploymentDefault);\r\n", SpaceUtils.ThreeTabs, "\r\n", 
			SpaceUtils.ThreeTabs, "var receiver = SimpleStandardContractTest.ReceiverAddress;\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, "var transferMessage = new TransferFunction()\r\n", SpaceUtils.ThreeTabs, "{\r\n", SpaceUtils.ThreeTabs, "Value = valueToSend,\r\n", SpaceUtils.ThreeTabs, "FromAddress = DefaultTestAccountConstants.Address,\r\n", SpaceUtils.ThreeTabs, "To = receiver,\r\n", SpaceUtils.ThreeTabs, "};\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, "var expectedEvent = new TransferEventDTO()\r\n", 
			SpaceUtils.ThreeTabs, "{\r\n", SpaceUtils.FourTabs, "From = DefaultTestAccountConstants.Address.ToLower(), \r\n", SpaceUtils.FourTabs, "To = SimpleStandardContractTest.ReceiverAddress.ToLower(),\r\n", SpaceUtils.FourTabs, "Value = valueToSend\r\n", SpaceUtils.ThreeTabs, "};\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, "GivenATransaction(transferMessage).\r\n", SpaceUtils.FourTabs, "ThenExpectAnEvent(expectedEvent);\r\n", SpaceUtils.FourTabs, "\r\n", SpaceUtils.ThreeTabs, "var queryBalanceReceiverMessage = new BalanceOfFunction() { Owner = ReceiverAddress };\r\n", 
			SpaceUtils.ThreeTabs, "var balanceOfExpectedResult = new BalanceOfOutputDTO() { Balance = valueToSend };\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, "WhenQuerying<BalanceOfFunction, BalanceOfOutputDTO>(queryBalanceReceiverMessage)\r\n", SpaceUtils.FourTabs, ".ThenExpectResult(balanceOfExpectedResult);\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.TwoTabs, "}\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "public static string ReceiverAddress = \"0x31230d2cce102216644c59daE5baed380d84830c\";\r\n", SpaceUtils.FiveTabs, "\r\n", SpaceUtils.TwoTabs, "// Simple scenario for deployment\r\n", 
			SpaceUtils.ThreeTabs, "public static StandardTokenDeployment GetDeploymentMessage()\r\n", SpaceUtils.ThreeTabs, "{\r\n", SpaceUtils.FourTabs, "return new StandardTokenDeployment()\r\n", SpaceUtils.FourTabs, "{\r\n", SpaceUtils.FiveTabs, "InitialAmount = 10000000,\r\n", SpaceUtils.FiveTabs, "TokenName = \"TST\",\r\n", SpaceUtils.FiveTabs, "TokenSymbol = \"TST\",\r\n", SpaceUtils.FiveTabs, "DecimalUnits = 18,\r\n", SpaceUtils.FiveTabs, "FromAddress = DefaultTestAccountConstants.Address\r\n", SpaceUtils.FourTabs, "};\r\n", 
			SpaceUtils.ThreeTabs, "}\r\n", SpaceUtils.FiveTabs, "\r\n", SpaceUtils.ThreeTabs, "public static BalanceOfFunction GetBalanceOfOwnerMessage()\r\n", SpaceUtils.ThreeTabs, "{\r\n", SpaceUtils.FourTabs, "return new BalanceOfFunction()\r\n", SpaceUtils.FourTabs, "{\r\n", SpaceUtils.FiveTabs, "Owner = DefaultTestAccountConstants.Address\r\n", SpaceUtils.FourTabs, "};\r\n", SpaceUtils.ThreeTabs, "}\r\n", SpaceUtils.ThreeTabs, "\r\n", 
			SpaceUtils.TwoTabs, "//*** Etherum / Nethereum CQS Messages. These classes will normally be included in your Ethereum contract integration project *** ///\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, "// Standard token deployment\r\n", SpaceUtils.TwoTabs, "public class StandardTokenDeployment : ContractDeploymentMessage\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, "public static string BYTECODE = \"6060604052341561000f57600080fd5b6040516107ae3803806107ae833981016040528080519190602001805182019190602001805191906020018051600160a060020a03331660009081526001602052604081208790558690559091019050600383805161007292916020019061009f565b506004805460ff191660ff8416179055600581805161009592916020019061009f565b505050505061013a565b828054600181600116156101000203166002900490600052602060002090601f016020900481019282601f106100e057805160ff191683800117855561010d565b8280016001018555821561010d579182015b8281111561010d5782518255916020019190600101906100f2565b5061011992915061011d565b5090565b61013791905b808211156101195760008155600101610123565b90565b610665806101496000396000f3006060604052600436106100ae5763ffffffff7c010000000000000000000000000000000000000000000000000000000060003504166306fdde0381146100b3578063095ea7b31461013d57806318160ddd1461017357806323b872dd1461019857806327e235e3146101c0578063313ce567146101df5780635c6581651461020857806370a082311461022d57806395d89b411461024c578063a9059cbb1461025f578063dd62ed3e14610281575b600080fd5b34156100be57600080fd5b6100c66102a6565b60405160208082528190810183818151815260200191508051906020019080838360005b838110156101025780820151838201526020016100ea565b50505050905090810190601f16801561012f5780820380516001836020036101000a031916815260200191505b509250505060405180910390f35b341561014857600080fd5b61015f600160a060020a0360043516602435610344565b604051901515815260200160405180910390f35b341561017e57600080fd5b6101866103b0565b60405190815260200160405180910390f35b34156101a357600080fd5b61015f600160a060020a03600435811690602435166044356103b6565b34156101cb57600080fd5b610186600160a060020a03600435166104bc565b34156101ea57600080fd5b6101f26104ce565b60405160ff909116815260200160405180910390f35b341561021357600080fd5b610186600160a060020a03600435811690602435166104d7565b341561023857600080fd5b610186600160a060020a03600435166104f4565b341561025757600080fd5b6100c661050f565b341561026a57600080fd5b61015f600160a060020a036004351660243561057a565b341561028c57600080fd5b610186600160a060020a036004358116906024351661060e565b60038054600181600116156101000203166002900480601f01602080910402602001604051908101604052809291908181526020018280546001816001161561010002031660029004801561033c5780601f106103115761010080835404028352916020019161033c565b820191906000526020600020905b81548152906001019060200180831161031f57829003601f168201915b505050505081565b600160a060020a03338116600081815260026020908152604080832094871680845294909152808220859055909291907f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b9259085905190815260200160405180910390a350600192915050565b60005481565b600160a060020a0380841660008181526002602090815260408083203390951683529381528382205492825260019052918220548390108015906103fa5750828110155b151561040557600080fd5b600160a060020a038085166000908152600160205260408082208054870190559187168152208054849003905560001981101561046a57600160a060020a03808616600090815260026020908152604080832033909416835292905220805484900390555b83600160a060020a031685600160a060020a03167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef8560405190815260200160405180910390a3506001949350505050565b60016020526000908152604090205481565b60045460ff1681565b600260209081526000928352604080842090915290825290205481565b600160a060020a031660009081526001602052604090205490565b60058054600181600116156101000203166002900480601f01602080910402602001604051908101604052809291908181526020018280546001816001161561010002031660029004801561033c5780601f106103115761010080835404028352916020019161033c565b600160a060020a033316600090815260016020526040812054829010156105a057600080fd5b600160a060020a033381166000818152600160205260408082208054879003905592861680825290839020805486019055917fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef9085905190815260200160405180910390a350600192915050565b600160a060020a039182166000908152600260209081526040808320939094168252919091522054905600a165627a7a723058201145b253e40a502d8bd264f98d66de641dec0c9e4a25e35eaba523821e0fb6ad0029\";\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, "public StandardTokenDeployment() : base(BYTECODE)\r\n", SpaceUtils.ThreeTabs, "{\r\n", 
			SpaceUtils.ThreeTabs, "}\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, "public StandardTokenDeployment(string byteCode) : base(byteCode)\r\n", SpaceUtils.ThreeTabs, "{\r\n", SpaceUtils.ThreeTabs, "}\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, "[Parameter(\"uint256\", \"_initialAmount\", 1)]\r\n", SpaceUtils.ThreeTabs, "public BigInteger InitialAmount { get; set; }\r\n", SpaceUtils.ThreeTabs, "[Parameter(\"string\", \"_tokenName\", 2)]\r\n", SpaceUtils.ThreeTabs, "public string TokenName { get; set; }\r\n", 
			SpaceUtils.ThreeTabs, "[Parameter(\"uint8\", \"_decimalUnits\", 3)]\r\n", SpaceUtils.ThreeTabs, "public byte DecimalUnits { get; set; }\r\n", SpaceUtils.ThreeTabs, "[Parameter(\"string\", \"_tokenSymbol\", 4)]\r\n", SpaceUtils.ThreeTabs, "public string TokenSymbol { get; set; }\r\n", SpaceUtils.TwoTabs, "}\r\n", SpaceUtils.FiveTabs, "\r\n", SpaceUtils.TwoTabs, "// Standard token transfer\r\n", SpaceUtils.TwoTabs, "[Function(\"transfer\", \"bool\")]\r\n", SpaceUtils.TwoTabs, "public class TransferFunction : ContractMessage\r\n", SpaceUtils.TwoTabs, "{\r\n", 
			SpaceUtils.ThreeTabs, "[Parameter(\"address\", \"_to\", 1)]\r\n", SpaceUtils.ThreeTabs, "public string To { get; set; }\r\n", SpaceUtils.ThreeTabs, "[Parameter(\"uint256\", \"_value\", 2)]\r\n", SpaceUtils.ThreeTabs, "public BigInteger Value { get; set; }\r\n", SpaceUtils.TwoTabs, "}\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "//Standard token balanceOf\r\n", SpaceUtils.TwoTabs, "[Function(\"balanceOf\", typeof(BalanceOfOutputDTO))]\r\n", SpaceUtils.TwoTabs, "public class BalanceOfFunction : ContractMessage\r\n", SpaceUtils.TwoTabs, "{\r\n", 
			SpaceUtils.ThreeTabs, "[Parameter(\"address\", \"_owner\", 1)]\r\n", SpaceUtils.ThreeTabs, "public string Owner { get; set; }\r\n", SpaceUtils.TwoTabs, "}\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "//Standard token balanceOf Return\r\n", SpaceUtils.TwoTabs, "[FunctionOutput]\r\n", SpaceUtils.TwoTabs, "public class BalanceOfOutputDTO\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "[Parameter(\"uint256\", \"balance\", 1)]\r\n", 
			SpaceUtils.ThreeTabs, "public BigInteger Balance { get; set; }\r\n", SpaceUtils.TwoTabs, "}\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "//Standard token Transfer Event \r\n", SpaceUtils.TwoTabs, "[Event(\"Transfer\")]\r\n", SpaceUtils.TwoTabs, "public class TransferEventDTO\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "[Parameter(\"address\", \"_from\", 1, true)]\r\n", SpaceUtils.ThreeTabs, "public string From { get; set; }\r\n", SpaceUtils.ThreeTabs, "[Parameter(\"address\", \"_to\", 2, true)]\r\n", 
			SpaceUtils.ThreeTabs, "public string To { get; set; }\r\n", SpaceUtils.ThreeTabs, "[Parameter(\"uint256\", \"_value\", 3, false)]\r\n", SpaceUtils.ThreeTabs, "public BigInteger Value { get; set; }\r\n", SpaceUtils.TwoTabs, "}\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.OneTab, "}"
		]);
	}
}
interface IClassGenerator
{
	GenerateClass(): string;
}
interface IGenerator
{
	GenerateClass(): string;
}
interface IFileGenerator extends IGenerator
{
	GenerateFileContent(outputPath: string): GeneratedFile;
	GenerateFileContent(): GeneratedFile;
	GenerateFileContent(outputPath?: string): GeneratedFile;
	GetFileName(): string;
}
class ClassGeneratorBase<TClassTemplate, TClassModel> extends NObject implements IFileGenerator, IGenerator, IClassGenerator
{
	ClassTemplate: TClassTemplate = null;
	ClassModel: TClassModel = null;
	GenerateFileContent(outputPath: string): GeneratedFile;
	GenerateFileContent(): GeneratedFile;
	GenerateFileContent(outputPath?: string): GeneratedFile
	{
		if (arguments.length === 1 && (outputPath === null || outputPath.constructor === String))
		{
			return this.GenerateFileContent_0(outputPath);
		}
		return this.GenerateFileContent_1();
	}
	private GenerateFileContent_0(outputPath: string): GeneratedFile
	{
		var text: string = this.GenerateFileContent();
		if (text !== null)
		{
			return new GeneratedFile(text, this.GetFileName(), outputPath);
		}
		return null;
	}
	private GenerateFileContent_1(): string
	{
		var classTemplate: TClassTemplate = this.ClassTemplate;
		return classTemplate.GenerateFullClass();
	}
	GetFileName(): string
	{
		return this.ClassModel.GetFileName();
	}
	GenerateClass(): string
	{
		var classTemplate: TClassTemplate = this.ClassTemplate;
		return classTemplate.GenerateClass();
	}
	constructor()
	{
		super();
	}
}
class SimpleTestGenerator extends ClassGeneratorBase<ClassTemplateBase<SimpleTestModel>, SimpleTestModel>
{
	get ContractABI(): ContractABI
	{
		return this._ContractABI_k__BackingField;
	}
	constructor(contractABI: ContractABI, contractName: string, namespace: string, cqsNamespace: string, functionOutputNamespace: string, codeGenLanguage: CodeGenLanguage)
	{
		super();
		this._ContractABI_k__BackingField = contractABI;
		this.ClassModel = new SimpleTestModel(contractABI, contractName, namespace, cqsNamespace, functionOutputNamespace);
		this.ClassModel.CodeGenLanguage = codeGenLanguage;
		this.InitialiseTemplate(codeGenLanguage);
	}
	InitialiseTemplate(codeGenLanguage: CodeGenLanguage): void
	{
		if (codeGenLanguage === CodeGenLanguage.CSharp)
		{
			this.ClassTemplate = new SimpleTestCSharpTemplate(this.ClassModel);
			return;
		}
		throw new ArgumentOutOfRangeException("codeGenLanguage", codeGenLanguage, "Code generation not implemented for this language");
	}
}
interface IFileModel
{
	Namespace: string;
	NamespaceDependencies: List<string>;
	GetFileName(): string;
}
interface IClassModel extends IFileModel
{
	GetTypeName(): string;
	GetVariableName(): string;
}
class TypeMessageModel extends NObject implements IClassModel, IFileModel
{
	CommonGenerators: CommonGenerators = null;
	get Namespace(): string
	{
		return this._Namespace_k__BackingField;
	}
	get Name(): string
	{
		return this._Name_k__BackingField;
	}
	ClassNameSuffix: string = null;
	get NamespaceDependencies(): List<string>
	{
		return this._NamespaceDependencies_k__BackingField;
	}
	CodeGenLanguage: CodeGenLanguage = 0;
	constructor(namespace: string, name: string, classNameSuffix: string)
	{
		super();
		this._Namespace_k__BackingField = namespace;
		this._Name_k__BackingField = name;
		this.ClassNameSuffix = classNameSuffix;
		this.CommonGenerators = new CommonGenerators();
		this._NamespaceDependencies_k__BackingField = new List<string>();
		this.CodeGenLanguage = CodeGenLanguage.CSharp;
	}
	GetTypeName(name: string): string;
	GetTypeName(): string;
	GetTypeName(name?: string): string
	{
		if (arguments.length === 1 && (name === null || name.constructor === String))
		{
			return this.GetTypeName_0(name);
		}
		return this.GetTypeName_1();
	}
	private GetTypeName_0(name: string): string
	{
		return this.CommonGenerators.GenerateClassName(name) + this.ClassNameSuffix;
	}
	GetFileName(name: string): string;
	GetFileName(): string;
	GetFileName(name?: string): string
	{
		if (arguments.length === 1 && (name === null || name.constructor === String))
		{
			return this.GetFileName_0(name);
		}
		return this.GetFileName_1();
	}
	private GetFileName_0(name: string): string
	{
		return this.GetTypeName(name) + "." + CodeGenLanguageExt.GetCodeOutputFileExtension(this.CodeGenLanguage);
	}
	GetVariableName(name: string): string;
	GetVariableName(): string;
	GetVariableName(name?: string): string
	{
		if (arguments.length === 1 && (name === null || name.constructor === String))
		{
			return this.GetVariableName_0(name);
		}
		return this.GetVariableName_1();
	}
	private GetVariableName_0(name: string): string
	{
		return this.CommonGenerators.GenerateVariableName(name) + this.ClassNameSuffix;
	}
	private GetTypeName_1(): string
	{
		return this.GetTypeName(this.Name);
	}
	private GetFileName_1(): string
	{
		return this.GetFileName(this.Name);
	}
	private GetVariableName_1(): string
	{
		return this.GetVariableName(this.Name);
	}
}
class SimpleTestModel extends TypeMessageModel
{
	get ContractABI(): ContractABI
	{
		return this._ContractABI_k__BackingField;
	}
	get CQSNamespace(): string
	{
		return this._CQSNamespace_k__BackingField;
	}
	get FunctionOutputNamespace(): string
	{
		return this._FunctionOutputNamespace_k__BackingField;
	}
	constructor(contractABI: ContractABI, contractName: string, namespace: string, cqsNamespace: string, functionOutputNamespace: string)
	{
		super(namespace, contractName, "Test");
		this._ContractABI_k__BackingField = contractABI;
		this._CQSNamespace_k__BackingField = cqsNamespace;
		this._FunctionOutputNamespace_k__BackingField = functionOutputNamespace;
		this.InitisialiseNamespaceDependencies();
		this.NamespaceDependencies.Add(cqsNamespace);
		this.NamespaceDependencies.Add(functionOutputNamespace);
	}
	private InitisialiseNamespaceDependencies(): void
	{
		this.NamespaceDependencies.AddRange(NArray.ToEnumerable([
			"System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "System.Threading", "Nethereum.Hex.HexTypes", "Nethereum.ABI.FunctionEncoding.Attributes", "Nethereum.Web3", "Nethereum.RPC.Eth.DTOs", "Nethereum.Contracts.CQS", "Nethereum.Contracts.IntegrationTester", "Xunit", "Xunit.Abstractions"
		]));
	}
}
class EventDTOCSharpTemplate extends ClassTemplateBase<EventDTOModel>
{
	private _parameterAbiEventDtocSharpTemplate: ParameterABIEventDTOCSharpTemplate = null;
	constructor(eventDTOModel: EventDTOModel)
	{
		super(eventDTOModel);
		this._parameterAbiEventDtocSharpTemplate = new ParameterABIEventDTOCSharpTemplate();
		this.ClassFileTemplate = new CSharpClassFileTemplate(this.Model, this);
	}
	GenerateClass(): string
	{
		if (this.Model.CanGenerateOutputDTO())
		{
			return NString.Concat([
				this.GetPartialMainClass(), "\r\n\r\n", SpaceUtils.OneTab, "[Event(\"", this.Model.EventABI.Name, "\")]\r\n", SpaceUtils.OneTab, "public class ", this.Model.GetTypeName(), "Base : IEventDTO\r\n", SpaceUtils.OneTab, "{\r\n", this._parameterAbiEventDtocSharpTemplate.GenerateAllProperties(this.Model.EventABI.InputParameters), "\r\n", SpaceUtils.OneTab, "}"
			]);
		}
		return null;
	}
	GetPartialMainClass(): string
	{
		return NString.Concat([
			SpaceUtils.OneTab, "public partial class ", this.Model.GetTypeName(), " : ", this.Model.GetTypeName(), "Base { }"
		]);
	}
}
class FunctionOutputDTOCSharpTemplate extends ClassTemplateBase<FunctionOutputDTOModel>
{
	private _parameterAbiFunctionDtocSharpTemplate: ParameterABIFunctionDTOCSharpTemplate = null;
	constructor(model: FunctionOutputDTOModel)
	{
		super(model);
		this._parameterAbiFunctionDtocSharpTemplate = new ParameterABIFunctionDTOCSharpTemplate();
		this.ClassFileTemplate = new CSharpClassFileTemplate(this.Model, this);
	}
	GenerateClass(): string
	{
		if (this.Model.CanGenerateOutputDTO())
		{
			return NString.Concat([
				this.GetPartialMainClass(), "\r\n\r\n", SpaceUtils.OneTab, "[FunctionOutput]\r\n", SpaceUtils.OneTab, "public class ", this.Model.GetTypeName(), "Base : IFunctionOutputDTO \r\n", SpaceUtils.OneTab, "{\r\n", this._parameterAbiFunctionDtocSharpTemplate.GenerateAllProperties(this.Model.FunctionABI.OutputParameters), "\r\n", SpaceUtils.OneTab, "}"
			]);
		}
		return null;
	}
	GetPartialMainClass(): string
	{
		return NString.Concat([
			SpaceUtils.OneTab, "public partial class ", this.Model.GetTypeName(), " : ", this.Model.GetTypeName(), "Base { }"
		]);
	}
}
class ParameterABIEventDTOCSharpTemplate extends NObject
{
	private parameterAbiModelTypeMap: ParameterABIModelTypeMap = null;
	private utils: Utils = null;
	constructor()
	{
		super();
		var typeConvertor: ABITypeToCSharpType = new ABITypeToCSharpType();
		this.parameterAbiModelTypeMap = new ParameterABIModelTypeMap(typeConvertor);
		this.utils = new Utils();
	}
	GenerateAllProperties(parameters: ParameterABI[]): string
	{
		return NString.Join(Environment.NewLine, Enumerable.Select<ParameterABI, string>(NArray.ToEnumerable(parameters), this.GenerateProperty));
	}
	GenerateProperty(parameter: ParameterABI): string
	{
		var parameterABIModel: ParameterABIModel = new ParameterABIModel(parameter);
		return NString.Format("{0}[Parameter(\"{1}\", \"{2}\", {3}, {4} )]\r\n{5}public virtual {6} {7} {{ get; set; }}", [
			SpaceUtils.TwoTabs, parameter.Type, parameter.Name, parameter.Order, this.utils.GetBooleanAsString(parameter.Indexed), SpaceUtils.TwoTabs, this.parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter), parameterABIModel.GetPropertyName()
		]);
	}
}
class ParameterABIFunctionDTOCSharpTemplate extends NObject
{
	private parameterModel: ParameterABIModel = null;
	private parameterAbiModelTypeMap: ParameterABIModelTypeMap = null;
	constructor()
	{
		super();
		var typeConvertor: ABITypeToCSharpType = new ABITypeToCSharpType();
		this.parameterAbiModelTypeMap = new ParameterABIModelTypeMap(typeConvertor);
	}
	GenerateAllProperties(parameters: ParameterABI[]): string
	{
		return NString.Join(Environment.NewLine, Enumerable.Select<ParameterABI, string>(NArray.ToEnumerable(parameters), this.GenerateProperty));
	}
	GenerateProperty(parameter: ParameterABI): string
	{
		var parameterABIModel: ParameterABIModel = new ParameterABIModel(parameter);
		return NString.Format("{0}[Parameter(\"{1}\", \"{2}\", {3})]\r\n{4}public virtual {5} {6} {{ get; set; }}", [
			SpaceUtils.TwoTabs, parameter.Type, parameter.Name, parameter.Order, SpaceUtils.TwoTabs, this.parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter), parameterABIModel.GetPropertyName()
		]);
	}
	GenerateAllFunctionParameters(parameters: ParameterABI[]): string
	{
		return NString.Join(", ", Enumerable.Select<ParameterABI, string>(NArray.ToEnumerable(parameters), this.GenerateFunctionParameter));
	}
	GenerateFunctionParameter(parameter: ParameterABI): string
	{
		var parameterABIModel: ParameterABIModel = new ParameterABIModel(parameter);
		return this.parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter) + " " + parameterABIModel.GetVariableName();
	}
	GenerateAssigmentFunctionParametersToProperties(parameters: ParameterABI[], objectName: string, spacing: string): string
	{
		return NString.Join(Environment.NewLine, Enumerable.Select<ParameterABI, string>(NArray.ToEnumerable(parameters), (x: ParameterABI)=>{return this.GenerateAssigmentFunctionParameterToProperty(x, objectName, spacing);}));
	}
	GenerateAssigmentFunctionParameterToProperty(parameter: ParameterABI, objectName: string, spacing: string): string
	{
		var parameterABIModel: ParameterABIModel = new ParameterABIModel(parameter);
		return NString.Concat([
			spacing, objectName, ".", parameterABIModel.GetPropertyName(), " = ", parameterABIModel.GetVariableName(), ";"
		]);
	}
}
class EventDTOGenerator extends ClassGeneratorBase<ClassTemplateBase<EventDTOModel>, EventDTOModel>
{
	constructor(abi: EventABI, namespace: string, codeGenLanguage: CodeGenLanguage)
	{
		super();
		this.ClassModel = new EventDTOModel(abi, namespace);
		this.ClassModel.CodeGenLanguage = codeGenLanguage;
		this.InitialiseTemplate(codeGenLanguage);
	}
	InitialiseTemplate(codeGenLanguage: CodeGenLanguage): void
	{
		switch (codeGenLanguage)
		{
		case CodeGenLanguage.CSharp:
			this.ClassTemplate = new EventDTOCSharpTemplate(this.ClassModel);
			return;
		case CodeGenLanguage.Vb:
			this.ClassTemplate = new EventDTOVbTemplate(this.ClassModel);
			return;
		case CodeGenLanguage.FSharp:
			this.ClassTemplate = new EventDTOFSharpTemplate(this.ClassModel);
			return;
		}
		throw new ArgumentOutOfRangeException("codeGenLanguage", codeGenLanguage, "Code generation not implemented for this language");
	}
}
class EventDTOModel extends TypeMessageModel
{
	get EventABI(): EventABI
	{
		return this._EventABI_k__BackingField;
	}
	constructor(eventABI: EventABI, namespace: string)
	{
		super(namespace, eventABI.Name, "EventDTO");
		this._EventABI_k__BackingField = eventABI;
		this.InitisialiseNamespaceDependencies();
	}
	private InitisialiseNamespaceDependencies(): void
	{
		this.NamespaceDependencies.AddRange(NArray.ToEnumerable([
			"System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.ABI.FunctionEncoding.Attributes"
		]));
	}
	CanGenerateOutputDTO(): boolean
	{
		return this.EventABI.InputParameters !== null && this.EventABI.InputParameters.length > 0;
	}
}
class EventDTOFSharpTemplate extends ClassTemplateBase<EventDTOModel>
{
	private _parameterAbiEventDtoFSharpTemplate: ParameterABIEventDTOFSharpTemplate = null;
	constructor(eventDTOModel: EventDTOModel)
	{
		super(eventDTOModel);
		this._parameterAbiEventDtoFSharpTemplate = new ParameterABIEventDTOFSharpTemplate();
		this.ClassFileTemplate = new FSharpClassFileTemplate(this.Model, this);
	}
	GenerateClass(): string
	{
		if (this.Model.CanGenerateOutputDTO())
		{
			return NString.Concat([
				SpaceUtils.OneTab, "[<Event(\"", this.Model.EventABI.Name, "\")>]\r\n", SpaceUtils.OneTab, "type ", this.Model.GetTypeName(), "() =\r\n", SpaceUtils.TwoTabs, "interface IEventDTO with\r\n", this._parameterAbiEventDtoFSharpTemplate.GenerateAllProperties(this.Model.EventABI.InputParameters), "\r\n", SpaceUtils.OneTab
			]);
		}
		return null;
	}
}
class FunctionOutputDTOFSharpTemplate extends ClassTemplateBase<FunctionOutputDTOModel>
{
	private _parameterAbiFunctionDtoFSharpTemplate: ParameterABIFunctionDTOFSharpTemplate = null;
	constructor(model: FunctionOutputDTOModel)
	{
		super(model);
		this._parameterAbiFunctionDtoFSharpTemplate = new ParameterABIFunctionDTOFSharpTemplate();
		this.ClassFileTemplate = new FSharpClassFileTemplate(this.Model, this);
	}
	GenerateClass(): string
	{
		if (this.Model.CanGenerateOutputDTO())
		{
			return NString.Concat([
				SpaceUtils.OneTab, "[<FunctionOutput>]\r\n", SpaceUtils.OneTab, "type ", this.Model.GetTypeName(), "() =\r\n", SpaceUtils.TwoTabs, "interface IFunctionOutputDTO with\r\n", this._parameterAbiFunctionDtoFSharpTemplate.GenerateAllProperties(this.Model.FunctionABI.OutputParameters), "\r\n", SpaceUtils.OneTab
			]);
		}
		return null;
	}
}
class ParameterABIEventDTOFSharpTemplate extends NObject
{
	private parameterAbiModelTypeMap: ParameterABIModelTypeMap = null;
	private utils: Utils = null;
	constructor()
	{
		super();
		var typeConvertor: ABITypeToFSharpType = new ABITypeToFSharpType();
		this.parameterAbiModelTypeMap = new ParameterABIModelTypeMap(typeConvertor);
		this.utils = new Utils();
	}
	GenerateAllProperties(parameters: ParameterABI[]): string
	{
		return NString.Join(Environment.NewLine, Enumerable.Select<ParameterABI, string>(NArray.ToEnumerable(parameters), this.GenerateProperty));
	}
	GenerateProperty(parameter: ParameterABI): string
	{
		var parameterABIModel: ParameterABIModel = new ParameterABIModel(parameter);
		return NString.Format("{0}[<Parameter(\"{1}\", \"{2}\", {3}, {4} )>]\r\n{5}member val {6} = Unchecked.defaultof<{7}> with get, set", [
			SpaceUtils.ThreeTabs, parameter.Type, parameter.Name, parameter.Order, this.utils.GetBooleanAsString(parameter.Indexed), SpaceUtils.ThreeTabs, parameterABIModel.GetPropertyName(), this.parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter)
		]);
	}
}
class ParameterABIFunctionDTOFSharpTemplate extends NObject
{
	private parameterModel: ParameterABIModel = null;
	private parameterAbiModelTypeMap: ParameterABIModelTypeMap = null;
	constructor()
	{
		super();
		var typeConvertor: ABITypeToFSharpType = new ABITypeToFSharpType();
		this.parameterAbiModelTypeMap = new ParameterABIModelTypeMap(typeConvertor);
	}
	GenerateAllProperties(parameters: ParameterABI[]): string
	{
		return NString.Join(Environment.NewLine, Enumerable.Select<ParameterABI, string>(NArray.ToEnumerable(parameters), this.GenerateProperty));
	}
	GenerateProperty(parameter: ParameterABI): string
	{
		var parameterABIModel: ParameterABIModel = new ParameterABIModel(parameter);
		return NString.Format("{0}[<Parameter(\"{1}\", \"{2}\", {3})>]\r\n{4}member val {5} = Unchecked.defaultof<{6}> with get, set", [
			SpaceUtils.ThreeTabs, parameter.Type, parameter.Name, parameter.Order, SpaceUtils.ThreeTabs, parameterABIModel.GetPropertyName(), this.parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter)
		]);
	}
	GenerateAllFunctionParameters(parameters: ParameterABI[]): string
	{
		return NString.Join(", ", Enumerable.Select<ParameterABI, string>(NArray.ToEnumerable(parameters), this.GenerateFunctionParameter));
	}
	GenerateFunctionParameter(parameter: ParameterABI): string
	{
		return new ParameterABIModel(parameter).GetVariableName() + ": " + this.parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter);
	}
	GenerateAssigmentFunctionParametersToProperties(parameters: ParameterABI[], objectName: string, spacing: string): string
	{
		return NString.Join(Environment.NewLine, Enumerable.Select<ParameterABI, string>(NArray.ToEnumerable(parameters), (x: ParameterABI)=>{return this.GenerateAssigmentFunctionParameterToProperty(x, objectName, spacing);}));
	}
	GenerateAssigmentFunctionParameterToProperty(parameter: ParameterABI, objectName: string, spacing: string): string
	{
		var parameterABIModel: ParameterABIModel = new ParameterABIModel(parameter);
		return NString.Concat([
			spacing, objectName, ".", parameterABIModel.GetPropertyName(), " <- ", parameterABIModel.GetVariableName()
		]);
	}
}
class FunctionOutputDTOGenerator extends ClassGeneratorBase<ClassTemplateBase<FunctionOutputDTOModel>, FunctionOutputDTOModel>
{
	constructor(functionABI: FunctionABI, namespace: string, codeGenLanguage: CodeGenLanguage)
	{
		super();
		var expr_0E: FunctionOutputDTOModel = new FunctionOutputDTOModel(functionABI, namespace);
		expr_0E.CodeGenLanguage = codeGenLanguage;
		this.ClassModel = expr_0E;
		this.InitialiseTemplate(codeGenLanguage);
	}
	InitialiseTemplate(codeGenLanguage: CodeGenLanguage): void
	{
		switch (codeGenLanguage)
		{
		case CodeGenLanguage.CSharp:
			this.ClassTemplate = new FunctionOutputDTOCSharpTemplate(this.ClassModel);
			return;
		case CodeGenLanguage.Vb:
			this.ClassTemplate = new FunctionOutputDTOVbTemplate(this.ClassModel);
			return;
		case CodeGenLanguage.FSharp:
			this.ClassTemplate = new FunctionOutputDTOFSharpTemplate(this.ClassModel);
			return;
		}
		throw new ArgumentOutOfRangeException("codeGenLanguage", codeGenLanguage, "Code generation not implemented for this language");
	}
	GenerateClass(): string
	{
		if (!this.ClassModel.CanGenerateOutputDTO())
		{
			return null;
		}
		return this.ClassTemplate.GenerateClass();
	}
	GenerateFileContent(): string
	{
		if (!this.ClassModel.CanGenerateOutputDTO())
		{
			return null;
		}
		return this.ClassTemplate.GenerateFullClass();
	}
}
class FunctionOutputDTOModel extends TypeMessageModel
{
	get FunctionABI(): FunctionABI
	{
		return this._FunctionABI_k__BackingField;
	}
	constructor(functionABI: FunctionABI, namespace: string)
	{
		super(namespace, functionABI.Name, "OutputDTO");
		this._FunctionABI_k__BackingField = functionABI;
		this.InitisialiseNamespaceDependencies();
	}
	private InitisialiseNamespaceDependencies(): void
	{
		this.NamespaceDependencies.AddRange(NArray.ToEnumerable([
			"System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.ABI.FunctionEncoding.Attributes"
		]));
	}
	CanGenerateOutputDTO(): boolean
	{
		return this.FunctionABI.OutputParameters !== null && this.FunctionABI.OutputParameters.length !== 0 && this.FunctionABI.Constant;
	}
}
class EventDTOVbTemplate extends ClassTemplateBase<EventDTOModel>
{
	private _parameterAbiEventDtoVbTemplate: ParameterABIEventDTOVbTemplate = null;
	constructor(eventDTOModel: EventDTOModel)
	{
		super(eventDTOModel);
		this._parameterAbiEventDtoVbTemplate = new ParameterABIEventDTOVbTemplate();
		this.ClassFileTemplate = new VbClassFileTemplate(this.Model, this);
	}
	GenerateClass(): string
	{
		if (this.Model.CanGenerateOutputDTO())
		{
			return NString.Concat([
				this.GetPartialMainClass(), "\r\n\r\n", SpaceUtils.OneTab, "<[Event](\"", this.Model.EventABI.Name, "\")>\r\n", SpaceUtils.OneTab, "Public Class ", this.Model.GetTypeName(), "Base\r\n", SpaceUtils.TwoTabs, "Implements IEventDTO\r\n", SpaceUtils.TwoTabs, "\r\n", this._parameterAbiEventDtoVbTemplate.GenerateAllProperties(this.Model.EventABI.InputParameters), "\r\n", SpaceUtils.OneTab, "\r\n", SpaceUtils.OneTab, 
				"End Class"
			]);
		}
		return null;
	}
	GetPartialMainClass(): string
	{
		return NString.Concat([
			SpaceUtils.OneTab, "Public Partial Class ", this.Model.GetTypeName(), "\r\n", SpaceUtils.TwoTabs, "Inherits ", this.Model.GetTypeName(), "Base\r\n", SpaceUtils.OneTab, "End Class"
		]);
	}
}
class FunctionOutputDTOVbTemplate extends ClassTemplateBase<FunctionOutputDTOModel>
{
	private _parameterAbiFunctionDtoVbTemplate: ParameterABIFunctionDTOVbTemplate = null;
	constructor(model: FunctionOutputDTOModel)
	{
		super(model);
		this._parameterAbiFunctionDtoVbTemplate = new ParameterABIFunctionDTOVbTemplate();
		this.ClassFileTemplate = new VbClassFileTemplate(this.Model, this);
	}
	GenerateClass(): string
	{
		if (this.Model.CanGenerateOutputDTO())
		{
			return NString.Concat([
				this.GetPartialMainClass(), "\r\n\r\n", SpaceUtils.OneTab, "<[FunctionOutput]>\r\n", SpaceUtils.OneTab, "Public Class ", this.Model.GetTypeName(), "Base\r\n", SpaceUtils.TwoTabs, "Implements IFunctionOutputDTO\r\n", SpaceUtils.TwoTabs, "\r\n", this._parameterAbiFunctionDtoVbTemplate.GenerateAllProperties(this.Model.FunctionABI.OutputParameters), "\r\n", SpaceUtils.OneTab, "\r\n", SpaceUtils.OneTab, "End Class"
			]);
		}
		return null;
	}
	GetPartialMainClass(): string
	{
		return NString.Concat([
			SpaceUtils.OneTab, "Public Partial Class ", this.Model.GetTypeName(), "\r\n", SpaceUtils.TwoTabs, "Inherits ", this.Model.GetTypeName(), "Base\r\n", SpaceUtils.OneTab, "End Class"
		]);
	}
}
class ParameterABIEventDTOVbTemplate extends NObject
{
	private parameterAbiModelTypeMap: ParameterABIModelTypeMap = null;
	private utils: Utils = null;
	constructor()
	{
		super();
		var typeConvertor: ABITypeToVBType = new ABITypeToVBType();
		this.parameterAbiModelTypeMap = new ParameterABIModelTypeMap(typeConvertor);
		this.utils = new Utils();
	}
	GenerateAllProperties(parameters: ParameterABI[]): string
	{
		return NString.Join(Environment.NewLine, Enumerable.Select<ParameterABI, string>(NArray.ToEnumerable(parameters), this.GenerateProperty));
	}
	GenerateProperty(parameter: ParameterABI): string
	{
		var parameterABIModel: ParameterABIModel = new ParameterABIModel(parameter);
		return NString.Format("{0}<[Parameter](\"{1}\", \"{2}\", {3}, {4})>\r\n{5}Public Overridable Property [{6}] As {7}", [
			SpaceUtils.TwoTabs, parameter.Type, parameter.Name, parameter.Order, this.utils.GetBooleanAsString(parameter.Indexed), SpaceUtils.TwoTabs, parameterABIModel.GetPropertyName(), this.parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter)
		]);
	}
}
class ParameterABIFunctionDTOVbTemplate extends NObject
{
	private parameterModel: ParameterABIModel = null;
	private parameterAbiModelTypeMap: ParameterABIModelTypeMap = null;
	constructor()
	{
		super();
		var typeConvertor: ABITypeToVBType = new ABITypeToVBType();
		this.parameterAbiModelTypeMap = new ParameterABIModelTypeMap(typeConvertor);
	}
	GenerateAllProperties(parameters: ParameterABI[]): string
	{
		return NString.Join(Environment.NewLine, Enumerable.Select<ParameterABI, string>(NArray.ToEnumerable(parameters), this.GenerateProperty));
	}
	GenerateProperty(parameter: ParameterABI): string
	{
		var parameterABIModel: ParameterABIModel = new ParameterABIModel(parameter);
		return NString.Format("{0}<[Parameter](\"{1}\", \"{2}\", {3})>\r\n{4}Public Overridable Property [{5}] As {6}", [
			SpaceUtils.TwoTabs, parameter.Type, parameter.Name, parameter.Order, SpaceUtils.TwoTabs, parameterABIModel.GetPropertyName(), this.parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter)
		]);
	}
	GenerateAllFunctionParameters(parameters: ParameterABI[]): string
	{
		return NString.Join(", ", Enumerable.Select<ParameterABI, string>(NArray.ToEnumerable(parameters), this.GenerateFunctionParameter));
	}
	GenerateFunctionParameter(parameter: ParameterABI): string
	{
		var parameterABIModel: ParameterABIModel = new ParameterABIModel(parameter);
		return "ByVal [" + parameterABIModel.GetVariableName() + "] As " + this.parameterAbiModelTypeMap.GetParameterDotNetOutputMapType(parameter);
	}
	GenerateAssigmentFunctionParametersToProperties(parameters: ParameterABI[], objectName: string, spacing: string): string
	{
		return NString.Join(Environment.NewLine, Enumerable.Select<ParameterABI, string>(NArray.ToEnumerable(parameters), (x: ParameterABI)=>{return this.GenerateAssigmentFunctionParameterToProperty(x, objectName, spacing);}));
	}
	GenerateAssigmentFunctionParameterToProperty(parameter: ParameterABI, objectName: string, spacing: string): string
	{
		var parameterABIModel: ParameterABIModel = new ParameterABIModel(parameter);
		return NString.Concat([
			spacing, objectName, ".", parameterABIModel.GetPropertyName(), " = [", parameterABIModel.GetVariableName(), "]"
		]);
	}
}
class FileTemplate extends NObject
{
	FileModel: IFileModel = null;
	constructor(fileModel: IFileModel)
	{
		super();
		this.FileModel = fileModel;
	}
	GenerateNamespaceDependencies(): string
	{
		var arg_4B_0: string = Environment.NewLine;
		var arg_2F_0: IEnumerable<string> = this.FileModel.NamespaceDependencies;
		var arg_2F_1: (arg: string) => boolean;
		if ((arg_2F_1 = FileTemplate___c.__9__5_0) === null)
		{
			arg_2F_1 = (FileTemplate___c.__9__5_0 = FileTemplate___c.__9._GenerateNamespaceDependencies_b__5_0);
		}
		return NString.Join(arg_4B_0, Enumerable.Select<string, string>(Enumerable.Distinct<string>(Enumerable.Where<string>(arg_2F_0, arg_2F_1)), this.GenerateNamespaceDependency));
	}
	GenerateNamespaceDependency(namespaceName: string): string
	{
		throw new NotSupportedException();
	}
}
class ClassFileTemplate extends FileTemplate
{
	get ClassModel(): IClassModel
	{
		return this._ClassModel_k__BackingField;
	}
	get ClassTemplate(): IClassTemplate
	{
		return this._ClassTemplate_k__BackingField;
	}
	constructor(classModel: IClassModel, classTemplate: IClassTemplate)
	{
		super(classModel);
		this._ClassModel_k__BackingField = classModel;
		this._ClassTemplate_k__BackingField = classTemplate;
	}
	GenerateFullClass(): string
	{
		throw new NotSupportedException();
	}
}
class CSharpClassFileTemplate extends ClassFileTemplate
{
	constructor(classModel: IClassModel, classTemplate: IClassTemplate)
	{
		super(classModel, classTemplate);
	}
	GenerateNamespaceDependency(namespaceName: string): string
	{
		return SpaceUtils.NoTabs + "using " + namespaceName + ";";
	}
	GenerateFullClass(): string
	{
		return NString.Concat([
			super.GenerateNamespaceDependencies(), "\r\n", SpaceUtils.NoTabs, "\r\n", SpaceUtils.NoTabs, "namespace ", this.ClassModel.Namespace, "\r\n", SpaceUtils.NoTabs, "{\r\n", SpaceUtils.NoTabs, this.ClassTemplate.GenerateClass(), "\r\n", SpaceUtils.NoTabs, "}\r\n"
		]);
	}
}
class MultipleClassFileTemplate extends FileTemplate
{
	get ClassGenerators(): IEnumerable<IClassGenerator>
	{
		return this._ClassGenerators_k__BackingField;
	}
	constructor(classGenerators: IEnumerable<IClassGenerator>, fileModel: IFileModel)
	{
		super(fileModel);
		this._ClassGenerators_k__BackingField = classGenerators;
	}
	GenerateFile(): string
	{
		throw new NotSupportedException();
	}
}
class CSharpMultipleClassFileTemplate extends MultipleClassFileTemplate
{
	constructor(classGenerators: IEnumerable<IClassGenerator>, fileModel: IFileModel)
	{
		super(classGenerators, fileModel);
	}
	GenerateNamespaceDependency(namespaceName: string): string
	{
		return SpaceUtils.NoTabs + "using " + namespaceName + ";";
	}
	GenerateFile(): string
	{
		return NString.Concat([
			super.GenerateNamespaceDependencies(), "\r\n", SpaceUtils.NoTabs, "\r\n", SpaceUtils.NoTabs, "namespace ", this.FileModel.Namespace, "\r\n", SpaceUtils.NoTabs, "{\r\n", this.GenerateAll(), "\r\n", SpaceUtils.NoTabs, "}\r\n"
		]);
	}
	GenerateAll(): string
	{
		var text: string = "";
		var enumerator: IEnumerator<IClassGenerator> = this.ClassGenerators.GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				var current: IClassGenerator = enumerator.Current;
				text = NString.Concat([
					text, SpaceUtils.NoTabs, "\r\n", SpaceUtils.NoTabs, "\r\n", current.GenerateClass()
				]);
			}
		}
		finally
		{
			if (enumerator !== null)
			{
				enumerator.Dispose();
			}
		}
		return text;
	}
}
class FileTemplate___c extends NObject
{
	static __9: FileTemplate___c = new FileTemplate___c();
	static __9__5_0: (arg: string) => boolean = null;
	_GenerateNamespaceDependencies_b__5_0(ns: string): boolean
	{
		return !NString.IsNullOrEmpty(ns);
	}
	constructor()
	{
		super();
	}
}
class FSharpClassFileTemplate extends ClassFileTemplate
{
	constructor(classModel: IClassModel, classTemplate: IClassTemplate)
	{
		super(classModel, classTemplate);
	}
	GenerateNamespaceDependency(namespaceName: string): string
	{
		return SpaceUtils.NoTabs + "open " + namespaceName;
	}
	GenerateFullClass(): string
	{
		return NString.Concat([
			SpaceUtils.NoTabs, "namespace ", this.ClassModel.Namespace, "\r\n", SpaceUtils.NoTabs, "\r\n", super.GenerateNamespaceDependencies(), "\r\n", SpaceUtils.NoTabs, "\r\n", SpaceUtils.NoTabs, this.ClassTemplate.GenerateClass(), "\r\n", SpaceUtils.NoTabs, "\r\n"
		]);
	}
}
class FSharpMultipleClassFileTemplate extends MultipleClassFileTemplate
{
	constructor(classGenerators: IEnumerable<IClassGenerator>, fileModel: IFileModel)
	{
		super(classGenerators, fileModel);
	}
	GenerateNamespaceDependency(namespaceName: string): string
	{
		return SpaceUtils.NoTabs + "open " + namespaceName;
	}
	GenerateFile(): string
	{
		return NString.Concat([
			SpaceUtils.NoTabs, "namespace ", this.FileModel.Namespace, "\r\n", SpaceUtils.NoTabs, "\r\n", super.GenerateNamespaceDependencies(), "\r\n", SpaceUtils.NoTabs, "\r\n", SpaceUtils.NoTabs, this.GenerateAll(), "\r\n", SpaceUtils.NoTabs, "\r\n"
		]);
	}
	GenerateAll(): string
	{
		var text: string = "";
		var enumerator: IEnumerator<IClassGenerator> = this.ClassGenerators.GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				var current: IClassGenerator = enumerator.Current;
				text = NString.Concat([
					text, SpaceUtils.OneTab, "\r\n", SpaceUtils.OneTab, "\r\n", current.GenerateClass()
				]);
			}
		}
		finally
		{
			if (enumerator !== null)
			{
				enumerator.Dispose();
			}
		}
		return text;
	}
}
class VbClassFileTemplate extends ClassFileTemplate
{
	constructor(classModel: IClassModel, classTemplate: IClassTemplate)
	{
		super(classModel, classTemplate);
	}
	GenerateNamespaceDependency(namespaceName: string): string
	{
		return SpaceUtils.NoTabs + "Imports " + namespaceName;
	}
	GenerateFullClass(): string
	{
		return NString.Concat([
			super.GenerateNamespaceDependencies(), "\r\n", SpaceUtils.NoTabs, "Namespace ", this.ClassModel.Namespace, "\r\n", SpaceUtils.NoTabs, "\r\n", SpaceUtils.NoTabs, this.ClassTemplate.GenerateClass(), "\r\n", SpaceUtils.NoTabs, "\r\n", SpaceUtils.NoTabs, "End Namespace\r\n"
		]);
	}
}
class VbMultipleClassFileTemplate extends MultipleClassFileTemplate
{
	constructor(classGenerators: IEnumerable<IClassGenerator>, fileModel: IFileModel)
	{
		super(classGenerators, fileModel);
	}
	GenerateNamespaceDependency(namespaceName: string): string
	{
		return SpaceUtils.NoTabs + "Imports " + namespaceName;
	}
	GenerateFile(): string
	{
		return NString.Concat([
			super.GenerateNamespaceDependencies(), "\r\n", SpaceUtils.NoTabs, "Namespace ", this.FileModel.Namespace, "\r\n", SpaceUtils.NoTabs, "\r\n", this.GenerateAll(), "\r\n", SpaceUtils.NoTabs, "End Namespace\r\n"
		]);
	}
	GenerateAll(): string
	{
		var text: string = "";
		var enumerator: IEnumerator<IClassGenerator> = this.ClassGenerators.GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				var current: IClassGenerator = enumerator.Current;
				text = NString.Concat([
					text, SpaceUtils.OneTab, "\r\n", SpaceUtils.OneTab, "\r\n", current.GenerateClass()
				]);
			}
		}
		finally
		{
			if (enumerator !== null)
			{
				enumerator.Dispose();
			}
		}
		return text;
	}
}
class ContractDeploymentCQSMessageGenerator extends ClassGeneratorBase<ClassTemplateBase<ContractDeploymentCQSMessageModel>, ContractDeploymentCQSMessageModel>
{
	constructor(abi: ConstructorABI, namespaceName: string, byteCode: string, contractName: string, codeGenLanguage: CodeGenLanguage)
	{
		super();
		this.ClassModel = new ContractDeploymentCQSMessageModel(abi, namespaceName, byteCode, contractName);
		this.ClassModel.CodeGenLanguage = codeGenLanguage;
		this.InitialiseTemplate(codeGenLanguage);
	}
	InitialiseTemplate(codeGenLanguage: CodeGenLanguage): void
	{
		switch (codeGenLanguage)
		{
		case CodeGenLanguage.CSharp:
			this.ClassTemplate = new ContractDeploymentCQSMessageCSharpTemplate(this.ClassModel);
			return;
		case CodeGenLanguage.Vb:
			this.ClassTemplate = new ContractDeploymentCQSMessageVbTemplate(this.ClassModel);
			return;
		case CodeGenLanguage.FSharp:
			this.ClassTemplate = new ContractDeploymentCQSMessageFSharpTemplate(this.ClassModel);
			return;
		}
		throw new ArgumentOutOfRangeException("codeGenLanguage", codeGenLanguage, "Code generation not implemented for this language");
	}
}
class ContractDeploymentCQSMessageModel extends TypeMessageModel
{
	get ConstructorABI(): ConstructorABI
	{
		return this._ConstructorABI_k__BackingField;
	}
	get ByteCode(): string
	{
		return this._ByteCode_k__BackingField;
	}
	constructor(constructorABI: ConstructorABI, namespace: string, byteCode: string, contractName: string)
	{
		super(namespace, contractName, "Deployment");
		this._ConstructorABI_k__BackingField = constructorABI;
		this._ByteCode_k__BackingField = byteCode;
		this.InitisialiseNamespaceDependencies();
	}
	private InitisialiseNamespaceDependencies(): void
	{
		this.NamespaceDependencies.AddRange(NArray.ToEnumerable([
			"System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.Contracts", "Nethereum.ABI.FunctionEncoding.Attributes"
		]));
	}
}
class ContractDeploymentCQSMessageCSharpTemplate extends ClassTemplateBase<ContractDeploymentCQSMessageModel>
{
	private _parameterAbiFunctionDtocSharpTemplate: ParameterABIFunctionDTOCSharpTemplate = null;
	constructor(model: ContractDeploymentCQSMessageModel)
	{
		super(model);
		this._parameterAbiFunctionDtocSharpTemplate = new ParameterABIFunctionDTOCSharpTemplate();
		this.ClassFileTemplate = new CSharpClassFileTemplate(model, this);
	}
	GenerateClass(): string
	{
		var typeName: string = this.Model.GetTypeName();
		return NString.Concat([
			this.GetPartialMainClass(), "\r\n\r\n", SpaceUtils.OneTab, "public class ", typeName, "Base : ContractDeploymentMessage\r\n", SpaceUtils.OneTab, "{\r\n", SpaceUtils.TwoTabs, "public static string BYTECODE = \"", this.Model.ByteCode, "\";\r\n", SpaceUtils.TwoTabs, "public ", typeName, "Base() : base(BYTECODE) { }\r\n", SpaceUtils.TwoTabs, "public ", typeName, 
			"Base(string byteCode) : base(byteCode) { }\r\n", this._parameterAbiFunctionDtocSharpTemplate.GenerateAllProperties(this.Model.ConstructorABI.InputParameters), "\r\n", SpaceUtils.OneTab, "}"
		]);
	}
	GetPartialMainClass(): string
	{
		var typeName: string = this.Model.GetTypeName();
		return NString.Concat([
			SpaceUtils.OneTab, "public partial class ", typeName, " : ", typeName, "Base\r\n", SpaceUtils.OneTab, "{\r\n", SpaceUtils.TwoTabs, "public ", typeName, "() : base(BYTECODE) { }\r\n", SpaceUtils.TwoTabs, "public ", typeName, "(string byteCode) : base(byteCode) { }\r\n", SpaceUtils.OneTab, "}"
		]);
	}
}
class FunctionCQSMessageCSharpTemplate extends ClassTemplateBase<FunctionCQSMessageModel>
{
	private _parameterAbiFunctionDtocSharpTemplate: ParameterABIFunctionDTOCSharpTemplate = null;
	private _functionOutputDTOModel: FunctionOutputDTOModel = null;
	private _functionABIModel: FunctionABIModel = null;
	constructor(model: FunctionCQSMessageModel, functionOutputDTOModel: FunctionOutputDTOModel, functionABIModel: FunctionABIModel)
	{
		super(model);
		this._parameterAbiFunctionDtocSharpTemplate = new ParameterABIFunctionDTOCSharpTemplate();
		this._functionOutputDTOModel = functionOutputDTOModel;
		this._functionABIModel = functionABIModel;
		this.ClassFileTemplate = new CSharpClassFileTemplate(model, this);
	}
	GenerateClass(): string
	{
		var functionABI: FunctionABI = this.Model.FunctionABI;
		var text: string = "";
		if (this._functionABIModel.IsMultipleOutput())
		{
			text = NString.Concat([
				SpaceUtils.OneTab, "[Function(\"", functionABI.Name, "\", typeof(", this._functionOutputDTOModel.GetTypeName(), "))]"
			]);
		}
		if (this._functionABIModel.IsSingleOutput())
		{
			text = NString.Concat([
				SpaceUtils.OneTab, "[Function(\"", functionABI.Name, "\", \"", this._functionABIModel.GetSingleAbiReturnType(), "\")]"
			]);
		}
		if (this._functionABIModel.HasNoReturn())
		{
			text = SpaceUtils.OneTab + "[Function(\"" + functionABI.Name + "\")]";
		}
		return NString.Concat([
			this.GetPartialMainClass(), "\r\n\r\n", text, "\r\n", SpaceUtils.OneTab, "public class ", this.Model.GetTypeName(), "Base : FunctionMessage\r\n", SpaceUtils.OneTab, "{\r\n", this._parameterAbiFunctionDtocSharpTemplate.GenerateAllProperties(functionABI.InputParameters), "\r\n", SpaceUtils.OneTab, "}"
		]);
	}
	GetPartialMainClass(): string
	{
		return NString.Concat([
			SpaceUtils.OneTab, "public partial class ", this.Model.GetTypeName(), " : ", this.Model.GetTypeName(), "Base { }"
		]);
	}
}
class ContractDeploymentCQSMessageFSharpTemplate extends ClassTemplateBase<ContractDeploymentCQSMessageModel>
{
	private _parameterAbiFunctionDtoFSharpTemplate: ParameterABIFunctionDTOFSharpTemplate = null;
	constructor(model: ContractDeploymentCQSMessageModel)
	{
		super(model);
		this._parameterAbiFunctionDtoFSharpTemplate = new ParameterABIFunctionDTOFSharpTemplate();
		this.ClassFileTemplate = new FSharpClassFileTemplate(model, this);
	}
	GenerateClass(): string
	{
		var typeName: string = this.Model.GetTypeName();
		return NString.Concat([
			SpaceUtils.OneTab, "type ", typeName, "(byteCode: string) =\r\n", SpaceUtils.TwoTabs, "inherit ContractDeploymentMessage(byteCode)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "static let BYTECODE = \"", this.Model.ByteCode, "\"\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "new() = ", typeName, "(BYTECODE)\r\n", SpaceUtils.TwoTabs, 
			"\r\n", this._parameterAbiFunctionDtoFSharpTemplate.GenerateAllProperties(this.Model.ConstructorABI.InputParameters), "\r\n", SpaceUtils.OneTab
		]);
	}
}
class FunctionCQSMessageFSharpTemplate extends ClassTemplateBase<FunctionCQSMessageModel>
{
	private _parameterAbiFunctionDtoFSharpTemplate: ParameterABIFunctionDTOFSharpTemplate = null;
	private _functionOutputDTOModel: FunctionOutputDTOModel = null;
	private _functionABIModel: FunctionABIModel = null;
	constructor(model: FunctionCQSMessageModel, functionOutputDTOModel: FunctionOutputDTOModel, functionABIModel: FunctionABIModel)
	{
		super(model);
		this._parameterAbiFunctionDtoFSharpTemplate = new ParameterABIFunctionDTOFSharpTemplate();
		this._functionOutputDTOModel = functionOutputDTOModel;
		this._functionABIModel = functionABIModel;
		this.ClassFileTemplate = new FSharpClassFileTemplate(model, this);
	}
	GenerateClass(): string
	{
		var functionABI: FunctionABI = this.Model.FunctionABI;
		var text: string = "";
		if (this._functionABIModel.IsMultipleOutput())
		{
			text = NString.Concat([
				SpaceUtils.OneTab, "[<Function(\"", functionABI.Name, "\", typeof<", this._functionOutputDTOModel.GetTypeName(), ">)>]"
			]);
		}
		if (this._functionABIModel.IsSingleOutput())
		{
			text = NString.Concat([
				SpaceUtils.OneTab, "[<Function(\"", functionABI.Name, "\", \"", this._functionABIModel.GetSingleAbiReturnType(), "\")>]"
			]);
		}
		if (this._functionABIModel.HasNoReturn())
		{
			text = SpaceUtils.OneTab + "[<Function(\"" + functionABI.Name + "\">]";
		}
		return NString.Concat([
			text, "\r\n", SpaceUtils.OneTab, "type ", this.Model.GetTypeName(), "() = \r\n", SpaceUtils.TwoTabs, "inherit FunctionMessage()\r\n", SpaceUtils.OneTab, "\r\n", this._parameterAbiFunctionDtoFSharpTemplate.GenerateAllProperties(functionABI.InputParameters), "\r\n", SpaceUtils.OneTab
		]);
	}
}
class FunctionCQSMessageGenerator extends ClassGeneratorBase<ClassTemplateBase<FunctionCQSMessageModel>, FunctionCQSMessageModel>
{
	get FunctionABI(): FunctionABI
	{
		return this._FunctionABI_k__BackingField;
	}
	constructor(functionABI: FunctionABI, namespace: string, namespaceFunctionOutput: string, codeGenLanguage: CodeGenLanguage)
	{
		super();
		this._FunctionABI_k__BackingField = functionABI;
		this.ClassModel = new FunctionCQSMessageModel(this.FunctionABI, namespace);
		this.ClassModel.NamespaceDependencies.Add(namespaceFunctionOutput);
		this.ClassModel.CodeGenLanguage = codeGenLanguage;
		var functionOutputDTOModel: FunctionOutputDTOModel = new FunctionOutputDTOModel(functionABI, namespaceFunctionOutput);
		this.InitialiseTemplate(codeGenLanguage, functionOutputDTOModel);
	}
	private InitialiseTemplate(codeGenLanguage: CodeGenLanguage, functionOutputDTOModel: FunctionOutputDTOModel): void
	{
		switch (codeGenLanguage)
		{
		case CodeGenLanguage.CSharp:
		{
			var abiTypeToDotnetTypeConvertor: ABITypeToCSharpType = new ABITypeToCSharpType();
			var functionABIModel: FunctionABIModel = new FunctionABIModel(this.ClassModel.FunctionABI, abiTypeToDotnetTypeConvertor);
			this.ClassTemplate = new FunctionCQSMessageCSharpTemplate(this.ClassModel, functionOutputDTOModel, functionABIModel);
			return;
		}
		case CodeGenLanguage.Vb:
		{
			var abiTypeToDotnetTypeConvertor2: ABITypeToVBType = new ABITypeToVBType();
			var functionABIModel2: FunctionABIModel = new FunctionABIModel(this.ClassModel.FunctionABI, abiTypeToDotnetTypeConvertor2);
			this.ClassTemplate = new FunctionCQSMessageVbTemplate(this.ClassModel, functionOutputDTOModel, functionABIModel2);
			return;
		}
		case CodeGenLanguage.FSharp:
		{
			var abiTypeToDotnetTypeConvertor3: ABITypeToFSharpType = new ABITypeToFSharpType();
			var functionABIModel3: FunctionABIModel = new FunctionABIModel(this.ClassModel.FunctionABI, abiTypeToDotnetTypeConvertor3);
			this.ClassTemplate = new FunctionCQSMessageFSharpTemplate(this.ClassModel, functionOutputDTOModel, functionABIModel3);
			return;
		}
		}
		throw new ArgumentOutOfRangeException("codeGenLanguage", codeGenLanguage, "Code generation not implemented for this language");
	}
}
class FunctionCQSMessageModel extends TypeMessageModel
{
	get FunctionABI(): FunctionABI
	{
		return this._FunctionABI_k__BackingField;
	}
	constructor(functionABI: FunctionABI, namespace: string)
	{
		super(namespace, functionABI.Name, "Function");
		this._FunctionABI_k__BackingField = functionABI;
		this.InitisialiseNamespaceDependencies();
	}
	private InitisialiseNamespaceDependencies(): void
	{
		this.NamespaceDependencies.AddRange(NArray.ToEnumerable([
			"System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.Contracts", "Nethereum.ABI.FunctionEncoding.Attributes"
		]));
	}
}
class ContractDeploymentCQSMessageVbTemplate extends ClassTemplateBase<ContractDeploymentCQSMessageModel>
{
	private _parameterAbiFunctionDtovbTemplate: ParameterABIFunctionDTOVbTemplate = null;
	constructor(model: ContractDeploymentCQSMessageModel)
	{
		super(model);
		this._parameterAbiFunctionDtovbTemplate = new ParameterABIFunctionDTOVbTemplate();
		this.ClassFileTemplate = new VbClassFileTemplate(model, this);
	}
	GenerateClass(): string
	{
		var typeName: string = this.Model.GetTypeName();
		return NString.Concat([
			this.GetPartialMainClass(), "\r\n\r\n", SpaceUtils.OneTab, "Public Class ", typeName, "Base \r\n", SpaceUtils.ThreeTabs, "Inherits ContractDeploymentMessage\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "Public Shared DEFAULT_BYTECODE As String = \"", this.Model.ByteCode, "\"\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "Public Sub New()\r\n", SpaceUtils.ThreeTabs, 
			"MyBase.New(DEFAULT_BYTECODE)\r\n", SpaceUtils.TwoTabs, "End Sub\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "Public Sub New(ByVal byteCode As String)\r\n", SpaceUtils.ThreeTabs, "MyBase.New(byteCode)\r\n", SpaceUtils.TwoTabs, "End Sub\r\n", SpaceUtils.TwoTabs, "\r\n", this._parameterAbiFunctionDtovbTemplate.GenerateAllProperties(this.Model.ConstructorABI.InputParameters), "\r\n", SpaceUtils.OneTab, "\r\n", SpaceUtils.OneTab, "End Class"
		]);
	}
	GetPartialMainClass(): string
	{
		var typeName: string = this.Model.GetTypeName();
		return NString.Concat([
			SpaceUtils.OneTab, "Public Partial Class ", typeName, "\r\n", SpaceUtils.OneTab, " Inherits ", typeName, "Base\r\n", SpaceUtils.OneTab, "\r\n", SpaceUtils.TwoTabs, "Public Sub New()\r\n", SpaceUtils.ThreeTabs, "MyBase.New(DEFAULT_BYTECODE)\r\n", SpaceUtils.TwoTabs, "End Sub\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, 
			"Public Sub New(ByVal byteCode As String)\r\n", SpaceUtils.ThreeTabs, "MyBase.New(byteCode)\r\n", SpaceUtils.TwoTabs, "End Sub\r\n", SpaceUtils.OneTab, "\r\n", SpaceUtils.OneTab, "End Class"
		]);
	}
}
class FunctionCQSMessageVbTemplate extends ClassTemplateBase<FunctionCQSMessageModel>
{
	private _parameterAbiFunctionDtovbTemplate: ParameterABIFunctionDTOVbTemplate = null;
	private _functionOutputDTOModel: FunctionOutputDTOModel = null;
	private _functionABIModel: FunctionABIModel = null;
	constructor(model: FunctionCQSMessageModel, functionOutputDTOModel: FunctionOutputDTOModel, functionABIModel: FunctionABIModel)
	{
		super(model);
		this._parameterAbiFunctionDtovbTemplate = new ParameterABIFunctionDTOVbTemplate();
		this._functionOutputDTOModel = functionOutputDTOModel;
		this._functionABIModel = functionABIModel;
		this.ClassFileTemplate = new VbClassFileTemplate(model, this);
	}
	GenerateClass(): string
	{
		var functionABI: FunctionABI = this.Model.FunctionABI;
		var text: string = "";
		if (this._functionABIModel.IsMultipleOutput())
		{
			text = NString.Concat([
				SpaceUtils.OneTab, "<[Function](\"", functionABI.Name, "\", GetType(", this._functionOutputDTOModel.GetTypeName(), "))>"
			]);
		}
		if (this._functionABIModel.IsSingleOutput())
		{
			text = NString.Concat([
				SpaceUtils.OneTab, "<[Function](\"", functionABI.Name, "\", \"", this._functionABIModel.GetSingleAbiReturnType(), "\")>"
			]);
		}
		if (this._functionABIModel.HasNoReturn())
		{
			text = SpaceUtils.OneTab + "<[Function](\"" + functionABI.Name + "\")>";
		}
		return NString.Concat([
			this.GetPartialMainClass(), "\r\n\r\n", SpaceUtils.OneTab, text, "\r\n", SpaceUtils.OneTab, "Public Class ", this.Model.GetTypeName(), "Base\r\n", SpaceUtils.TwoTabs, "Inherits FunctionMessage\r\n", SpaceUtils.OneTab, "\r\n", this._parameterAbiFunctionDtovbTemplate.GenerateAllProperties(functionABI.InputParameters), "\r\n", SpaceUtils.OneTab, "\r\n", SpaceUtils.OneTab, "End Class\r\n"
		]);
	}
	GetPartialMainClass(): string
	{
		return NString.Concat([
			SpaceUtils.OneTab, "Public Partial Class ", this.Model.GetTypeName(), "\r\n", SpaceUtils.TwoTabs, "Inherits ", this.Model.GetTypeName(), "Base\r\n", SpaceUtils.OneTab, "End Class"
		]);
	}
}
class MultipleClassGeneratorBase<TMultipleClassFileTemplate, TMultipleClassFileModel> extends NObject implements IFileGenerator, IGenerator
{
	Template: TMultipleClassFileTemplate = null;
	Model: TMultipleClassFileModel = null;
	GenerateFileContent(outputPath: string): GeneratedFile;
	GenerateFileContent(): GeneratedFile;
	GenerateFileContent(outputPath?: string): GeneratedFile
	{
		if (arguments.length === 1 && (outputPath === null || outputPath.constructor === String))
		{
			return this.GenerateFileContent_0(outputPath);
		}
		return this.GenerateFileContent_1();
	}
	private GenerateFileContent_0(outputPath: string): GeneratedFile
	{
		var text: string = this.GenerateFileContent();
		if (text !== null)
		{
			return new GeneratedFile(text, this.GetFileName(), outputPath);
		}
		return null;
	}
	private GenerateFileContent_1(): string
	{
		return this.Template.GenerateFile();
	}
	GetFileName(): string
	{
		return this.Model.GetFileName();
	}
	GenerateClass(): string
	{
		throw new Exception("Not supported");
	}
	constructor()
	{
		super();
	}
}
class AllMessagesGenerator extends MultipleClassGeneratorBase<MultipleClassFileTemplate, AllMessagesModel>
{
	private _classGenerators: IEnumerable<IClassGenerator> = null;
	constructor(classGenerators: IEnumerable<IClassGenerator>, contractName: string, namespace: string, codeGenLanguage: CodeGenLanguage)
	{
		super();
		this._classGenerators = classGenerators;
		this.Model = new AllMessagesModel(contractName, namespace);
		this.Model.CodeGenLanguage = codeGenLanguage;
		this.InitialiseTemplate(codeGenLanguage);
	}
	InitialiseTemplate(codeGenLanguage: CodeGenLanguage): void
	{
		switch (codeGenLanguage)
		{
		case CodeGenLanguage.CSharp:
			this.Template = new CSharpMultipleClassFileTemplate(this._classGenerators, this.Model);
			return;
		case CodeGenLanguage.Vb:
			this.Template = new VbMultipleClassFileTemplate(this._classGenerators, this.Model);
			return;
		case CodeGenLanguage.FSharp:
			this.Template = new FSharpMultipleClassFileTemplate(this._classGenerators, this.Model);
			return;
		}
		throw new ArgumentOutOfRangeException("codeGenLanguage", codeGenLanguage, "Code generation not implemented for this language");
	}
}
class FileModel extends NObject implements IFileModel
{
	get Name(): string
	{
		return this._Name_k__BackingField;
	}
	CommonGenerators: CommonGenerators = null;
	CodeGenLanguage: CodeGenLanguage = 0;
	get Namespace(): string
	{
		return this._Namespace_k__BackingField;
	}
	get NamespaceDependencies(): List<string>
	{
		return this._NamespaceDependencies_k__BackingField;
	}
	GetFileName(): string
	{
		return this.CommonGenerators.GenerateClassName(this.Name) + "." + CodeGenLanguageExt.GetCodeOutputFileExtension(this.CodeGenLanguage);
	}
	constructor(namespace: string, name: string)
	{
		super();
		this._NamespaceDependencies_k__BackingField = new List<string>();
		super..ctor();
		this._Namespace_k__BackingField = namespace;
		this._Name_k__BackingField = name;
		this.CommonGenerators = new CommonGenerators();
		this.CodeGenLanguage = CodeGenLanguage.CSharp;
	}
}
class AllMessagesModel extends FileModel
{
	get ContractDeploymentCQSMessageModel(): ContractDeploymentCQSMessageModel
	{
		return this._ContractDeploymentCQSMessageModel_k__BackingField;
	}
	constructor(contractName: string, namespace: string)
	{
		super(namespace, contractName + "Definition");
		this.InitialiseNamespaceDependencies();
	}
	private InitialiseNamespaceDependencies(): void
	{
		this.NamespaceDependencies.AddRange(NArray.ToEnumerable([
			"System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.ABI.FunctionEncoding.Attributes", "Nethereum.Web3", "Nethereum.RPC.Eth.DTOs", "Nethereum.Contracts.CQS", "Nethereum.Contracts", "System.Threading"
		]));
	}
}
class ContractDeploymentServiceMethodsCSharpTemplate extends NObject
{
	private _contractDeploymentCQSMessageModel: ContractDeploymentCQSMessageModel = null;
	private _serviceModel: ServiceModel = null;
	private static SpaceFollowingFunction: string = Environment.NewLine + Environment.NewLine;
	constructor(model: ServiceModel)
	{
		super();
		this._contractDeploymentCQSMessageModel = model.ContractDeploymentCQSMessageModel;
		this._serviceModel = model;
	}
	GenerateMethods(): string
	{
		var typeName: string = this._contractDeploymentCQSMessageModel.GetTypeName();
		var variableName: string = this._contractDeploymentCQSMessageModel.GetVariableName();
		var text: string = NString.Concat([
			SpaceUtils.TwoTabs, "public static Task<TransactionReceipt> DeployContractAndWaitForReceiptAsync(Nethereum.Web3.IWeb3 web3, ", typeName, " ", variableName, ", CancellationTokenSource cancellationTokenSource = null)\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "return web3.Eth.GetContractDeploymentHandler<", typeName, ">().SendRequestAndWaitForReceiptAsync(", variableName, ", cancellationTokenSource);\r\n", SpaceUtils.TwoTabs, "}"
		]);
		var text2: string = NString.Concat([
			SpaceUtils.TwoTabs, "public static Task<string> DeployContractAsync(Nethereum.Web3.IWeb3 web3, ", typeName, " ", variableName, ")\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "return web3.Eth.GetContractDeploymentHandler<", typeName, ">().SendRequestAsync(", variableName, ");\r\n", SpaceUtils.TwoTabs, "}"
		]);
		var text3: string = NString.Concat([
			SpaceUtils.TwoTabs, "public static async Task<", this._serviceModel.GetTypeName(), "> DeployContractAndGetServiceAsync(Nethereum.Web3.IWeb3 web3, ", typeName, " ", variableName, ", CancellationTokenSource cancellationTokenSource = null)\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "var receipt = await DeployContractAndWaitForReceiptAsync(web3, ", variableName, ", cancellationTokenSource);\r\n", SpaceUtils.ThreeTabs, "return new ", this._serviceModel.GetTypeName(), "(web3, receipt.ContractAddress);\r\n", SpaceUtils.TwoTabs, 
			"}"
		]);
		return NString.Join(ContractDeploymentServiceMethodsCSharpTemplate.SpaceFollowingFunction, [
			text, text2, text3
		]);
	}
}
class FunctionServiceMethodCSharpTemplate extends NObject
{
	private _model: ServiceModel = null;
	private _commonGenerators: CommonGenerators = null;
	private _typeConvertor: ITypeConvertor = null;
	private _parameterAbiFunctionDtocSharpTemplate: ParameterABIFunctionDTOCSharpTemplate = null;
	constructor(model: ServiceModel)
	{
		super();
		this._model = model;
		this._typeConvertor = new ABITypeToCSharpType();
		this._commonGenerators = new CommonGenerators();
		this._parameterAbiFunctionDtocSharpTemplate = new ParameterABIFunctionDTOCSharpTemplate();
	}
	GenerateMethods(): string
	{
		var functions: FunctionABI[] = this._model.ContractABI.Functions;
		return NString.Join(this.GenerateLineBreak(), Enumerable.Select<FunctionABI, string>(NArray.ToEnumerable(functions), this.GenerateMethod));
	}
	GenerateMethod(functionABI: FunctionABI): string
	{
		var arg_30_0: FunctionCQSMessageModel = new FunctionCQSMessageModel(functionABI, this._model.CQSNamespace);
		var functionOutputDTOModel: FunctionOutputDTOModel = new FunctionOutputDTOModel(functionABI, this._model.FunctionOutputNamespace);
		var functionABIModel: FunctionABIModel = new FunctionABIModel(functionABI, this._typeConvertor);
		var typeName: string = arg_30_0.GetTypeName();
		var variableName: string = arg_30_0.GetVariableName();
		var text: string = this._commonGenerators.GenerateClassName(functionABI.Name);
		if (functionABIModel.IsMultipleOutput() && !functionABIModel.IsTransaction())
		{
			var typeName2: string = functionOutputDTOModel.GetTypeName();
			var str: string = NString.Concat([
				SpaceUtils.TwoTabs, "public Task<", typeName2, "> ", text, "QueryAsync(", typeName, " ", variableName, ", BlockParameter blockParameter = null)\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "return ContractHandler.QueryDeserializingToObjectAsync<", typeName, ", ", typeName2, ">(", variableName, 
				", blockParameter);\r\n", SpaceUtils.TwoTabs, "}"
			]);
			var str2: string = NString.Concat([
				SpaceUtils.TwoTabs, "public Task<", typeName2, "> ", text, "QueryAsync(BlockParameter blockParameter = null)\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "return ContractHandler.QueryDeserializingToObjectAsync<", typeName, ", ", typeName2, ">(null, blockParameter);\r\n", SpaceUtils.TwoTabs, "}"
			]);
			var str3: string = NString.Concat([
				SpaceUtils.TwoTabs, "public Task<", typeName2, "> ", text, "QueryAsync(", this._parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters), ", BlockParameter blockParameter = null)\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "var ", variableName, " = new ", typeName, "();\r\n", this._parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, variableName, SpaceUtils.FourTabs), "\r\n", SpaceUtils.ThreeTabs, 
				"\r\n", SpaceUtils.ThreeTabs, "return ContractHandler.QueryDeserializingToObjectAsync<", typeName, ", ", typeName2, ">(", variableName, ", blockParameter);\r\n", SpaceUtils.TwoTabs, "}"
			]);
			if (functionABIModel.HasNoInputParameters())
			{
				return str + this.GenerateLineBreak() + str2;
			}
			return str + this.GenerateLineBreak() + str3;
		}
		else
		{
			if (functionABIModel.IsSingleOutput() && !functionABIModel.IsTransaction() && functionABI.OutputParameters !== null && functionABI.OutputParameters.length === 1 && functionABI.Constant)
			{
				var singleOutputReturnType: string = functionABIModel.GetSingleOutputReturnType();
				var str4: string = NString.Concat([
					SpaceUtils.TwoTabs, "public Task<", singleOutputReturnType, "> ", text, "QueryAsync(", typeName, " ", variableName, ", BlockParameter blockParameter = null)\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "return ContractHandler.QueryAsync<", typeName, ", ", singleOutputReturnType, ">(", variableName, 
					", blockParameter);\r\n", SpaceUtils.TwoTabs, "}"
				]);
				var str5: string = NString.Concat([
					SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "public Task<", singleOutputReturnType, "> ", text, "QueryAsync(BlockParameter blockParameter = null)\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "return ContractHandler.QueryAsync<", typeName, ", ", singleOutputReturnType, ">(null, blockParameter);\r\n", SpaceUtils.TwoTabs, "}"
				]);
				var str6: string = NString.Concat([
					SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "public Task<", singleOutputReturnType, "> ", text, "QueryAsync(", this._parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters), ", BlockParameter blockParameter = null)\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "var ", variableName, " = new ", typeName, "();\r\n", this._parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, variableName, SpaceUtils.FourTabs), 
					"\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, "return ContractHandler.QueryAsync<", typeName, ", ", singleOutputReturnType, ">(", variableName, ", blockParameter);\r\n", SpaceUtils.TwoTabs, "}"
				]);
				if (functionABIModel.HasNoInputParameters())
				{
					return str4 + this.GenerateLineBreak() + str5;
				}
				return str4 + this.GenerateLineBreak() + str6;
			}
			else
			{
				if (!functionABIModel.IsTransaction())
				{
					return null;
				}
				var text2: string = NString.Concat([
					SpaceUtils.TwoTabs, "public Task<string> ", text, "RequestAsync(", typeName, " ", variableName, ")\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, " return ContractHandler.SendRequestAsync(", variableName, ");\r\n", SpaceUtils.TwoTabs, "}"
				]);
				var text3: string = NString.Concat([
					SpaceUtils.TwoTabs, "public Task<string> ", text, "RequestAsync()\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, " return ContractHandler.SendRequestAsync<", typeName, ">();\r\n", SpaceUtils.TwoTabs, "}"
				]);
				var text4: string = NString.Concat([
					SpaceUtils.TwoTabs, "public Task<string> ", text, "RequestAsync(", this._parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters), ")\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "var ", variableName, " = new ", typeName, "();\r\n", this._parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, variableName, SpaceUtils.FourTabs), "\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, 
					" return ContractHandler.SendRequestAsync(", variableName, ");\r\n", SpaceUtils.TwoTabs, "}"
				]);
				var text5: string = NString.Concat([
					SpaceUtils.TwoTabs, "public Task<TransactionReceipt> ", text, "RequestAndWaitForReceiptAsync(", typeName, " ", variableName, ", CancellationTokenSource cancellationToken = null)\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, " return ContractHandler.SendRequestAndWaitForReceiptAsync(", variableName, ", cancellationToken);\r\n", SpaceUtils.TwoTabs, "}"
				]);
				var text6: string = NString.Concat([
					SpaceUtils.TwoTabs, "public Task<TransactionReceipt> ", text, "RequestAndWaitForReceiptAsync(CancellationTokenSource cancellationToken = null)\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, " return ContractHandler.SendRequestAndWaitForReceiptAsync<", typeName, ">(null, cancellationToken);\r\n", SpaceUtils.TwoTabs, "}"
				]);
				var text7: string = NString.Concat([
					SpaceUtils.TwoTabs, "public Task<TransactionReceipt> ", text, "RequestAndWaitForReceiptAsync(", this._parameterAbiFunctionDtocSharpTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters), ", CancellationTokenSource cancellationToken = null)\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "var ", variableName, " = new ", typeName, "();\r\n", this._parameterAbiFunctionDtocSharpTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, variableName, SpaceUtils.FourTabs), "\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, 
					" return ContractHandler.SendRequestAndWaitForReceiptAsync(", variableName, ", cancellationToken);\r\n", SpaceUtils.TwoTabs, "}"
				]);
				if (functionABIModel.HasNoInputParameters())
				{
					return NString.Concat([
						text2, this.GenerateLineBreak(), text3, this.GenerateLineBreak(), text5, this.GenerateLineBreak(), text6
					]);
				}
				return NString.Concat([
					text2, this.GenerateLineBreak(), text5, this.GenerateLineBreak(), text4, this.GenerateLineBreak(), text7
				]);
			}
		}
	}
	private GenerateLineBreak(): string
	{
		return Environment.NewLine + Environment.NewLine;
	}
}
class ServiceCSharpTemplate extends ClassTemplateBase<ServiceModel>
{
	private _functionServiceMethodCSharpTemplate: FunctionServiceMethodCSharpTemplate = null;
	private _deploymentServiceMethodsCSharpTemplate: ContractDeploymentServiceMethodsCSharpTemplate = null;
	constructor(model: ServiceModel)
	{
		super(model);
		this._functionServiceMethodCSharpTemplate = new FunctionServiceMethodCSharpTemplate(model);
		this._deploymentServiceMethodsCSharpTemplate = new ContractDeploymentServiceMethodsCSharpTemplate(model);
		this.ClassFileTemplate = new CSharpClassFileTemplate(this.Model, this);
	}
	GenerateClass(): string
	{
		return NString.Concat([
			SpaceUtils.OneTab, "public partial class ", this.Model.GetTypeName(), "\r\n", SpaceUtils.OneTab, "{\r\n", this._deploymentServiceMethodsCSharpTemplate.GenerateMethods(), "\r\n", SpaceUtils.NoTabs, "\r\n", SpaceUtils.TwoTabs, "protected Nethereum.Web3.IWeb3 Web3 { get; }\r\n", SpaceUtils.NoTabs, "\r\n", SpaceUtils.TwoTabs, "public ContractHandler ContractHandler { get; }\r\n", SpaceUtils.NoTabs, "\r\n", SpaceUtils.TwoTabs, 
			"public ", this.Model.GetTypeName(), "(Nethereum.Web3.IWeb3 web3, string contractAddress)\r\n", SpaceUtils.TwoTabs, "{\r\n", SpaceUtils.ThreeTabs, "Web3 = web3;\r\n", SpaceUtils.ThreeTabs, "ContractHandler = web3.Eth.GetContractHandler(contractAddress);\r\n", SpaceUtils.TwoTabs, "}\r\n", SpaceUtils.NoTabs, "\r\n", this._functionServiceMethodCSharpTemplate.GenerateMethods(), "\r\n", SpaceUtils.OneTab, "}"
		]);
	}
}
class ContractDeploymentServiceMethodsFSharpTemplate extends NObject
{
	private _contractDeploymentCQSMessageModel: ContractDeploymentCQSMessageModel = null;
	private _serviceModel: ServiceModel = null;
	constructor(model: ServiceModel)
	{
		super();
		this._contractDeploymentCQSMessageModel = model.ContractDeploymentCQSMessageModel;
		this._serviceModel = model;
	}
	GenerateMethods(): string
	{
		var typeName: string = this._contractDeploymentCQSMessageModel.GetTypeName();
		var variableName: string = this._contractDeploymentCQSMessageModel.GetVariableName();
		var text: string = NString.Concat([
			SpaceUtils.TwoTabs, "static member DeployContractAndWaitForReceiptAsync(web3: Web3, ", variableName, ": ", typeName, ", ?cancellationTokenSource : CancellationTokenSource): Task<TransactionReceipt> = \r\n", SpaceUtils.ThreeTabs, "let cancellationTokenSourceVal = defaultArg cancellationTokenSource null\r\n", SpaceUtils.ThreeTabs, "web3.Eth.GetContractDeploymentHandler<", typeName, ">().SendRequestAndWaitForReceiptAsync(", variableName, ", cancellationTokenSourceVal)\r\n", SpaceUtils.TwoTabs
		]);
		var text2: string = NString.Concat([
			SpaceUtils.TwoTabs, "static member DeployContractAsync(web3: Web3, ", variableName, ": ", typeName, "): Task<string> =\r\n", SpaceUtils.ThreeTabs, "web3.Eth.GetContractDeploymentHandler<", typeName, ">().SendRequestAsync(", variableName, ")\r\n", SpaceUtils.TwoTabs
		]);
		var text3: string = NString.Concat([
			SpaceUtils.TwoTabs, "static member DeployContractAndGetServiceAsync(web3: Web3, ", variableName, ": ", typeName, ", ?cancellationTokenSource : CancellationTokenSource) = async {\r\n", SpaceUtils.ThreeTabs, "let cancellationTokenSourceVal = defaultArg cancellationTokenSource null\r\n", SpaceUtils.ThreeTabs, "let! receipt = ", this._serviceModel.GetTypeName(), ".DeployContractAndWaitForReceiptAsync(web3, ", variableName, ", cancellationTokenSourceVal) |> Async.AwaitTask\r\n", SpaceUtils.ThreeTabs, "return new ", this._serviceModel.GetTypeName(), "(web3, receipt.ContractAddress);\r\n", SpaceUtils.ThreeTabs, 
			"}"
		]);
		return NString.Join(Environment.NewLine, [
			text, text2, text3
		]);
	}
}
class FunctionServiceMethodFSharpTemplate extends NObject
{
	private _model: ServiceModel = null;
	private _commonGenerators: CommonGenerators = null;
	private _typeConvertor: ITypeConvertor = null;
	constructor(model: ServiceModel)
	{
		super();
		this._model = model;
		this._typeConvertor = new ABITypeToFSharpType();
		this._commonGenerators = new CommonGenerators();
	}
	GenerateMethods(): string
	{
		var functions: FunctionABI[] = this._model.ContractABI.Functions;
		return NString.Join(Environment.NewLine, Enumerable.Select<FunctionABI, string>(NArray.ToEnumerable(functions), this.GenerateMethod));
	}
	GenerateMethod(functionABI: FunctionABI): string
	{
		var arg_30_0: FunctionCQSMessageModel = new FunctionCQSMessageModel(functionABI, this._model.CQSNamespace);
		var functionOutputDTOModel: FunctionOutputDTOModel = new FunctionOutputDTOModel(functionABI, this._model.FunctionOutputNamespace);
		var functionABIModel: FunctionABIModel = new FunctionABIModel(functionABI, this._typeConvertor);
		var typeName: string = arg_30_0.GetTypeName();
		var variableName: string = arg_30_0.GetVariableName();
		var text: string = this._commonGenerators.GenerateClassName(functionABI.Name);
		if (functionABIModel.IsMultipleOutput() && !functionABIModel.IsTransaction())
		{
			var typeName2: string = functionOutputDTOModel.GetTypeName();
			return NString.Concat([
				SpaceUtils.TwoTabs, "member this.", text, "QueryAsync(", variableName, ": ", typeName, ", ?blockParameter: BlockParameter): Task<", typeName2, "> =\r\n", SpaceUtils.ThreeTabs, "let blockParameterVal = defaultArg blockParameter null\r\n", SpaceUtils.ThreeTabs, "this.ContractHandler.QueryDeserializingToObjectAsync<", typeName, ", ", typeName2, ">(", variableName, 
				", blockParameterVal)\r\n", SpaceUtils.ThreeTabs
			]);
		}
		if (functionABIModel.IsSingleOutput() && !functionABIModel.IsTransaction() && functionABI.OutputParameters !== null && functionABI.OutputParameters.length === 1 && functionABI.Constant)
		{
			var singleOutputReturnType: string = functionABIModel.GetSingleOutputReturnType();
			return NString.Concat([
				SpaceUtils.TwoTabs, "member this.", text, "QueryAsync(", variableName, ": ", typeName, ", ?blockParameter: BlockParameter): Task<", singleOutputReturnType, "> =\r\n", SpaceUtils.ThreeTabs, "let blockParameterVal = defaultArg blockParameter null\r\n", SpaceUtils.ThreeTabs, "this.ContractHandler.QueryAsync<", typeName, ", ", singleOutputReturnType, ">(", variableName, 
				", blockParameterVal)\r\n", SpaceUtils.ThreeTabs
			]);
		}
		if (functionABIModel.IsTransaction())
		{
			var str: string = NString.Concat([
				SpaceUtils.TwoTabs, "member this.", text, "RequestAsync(", variableName, ": ", typeName, "): Task<string> =\r\n", SpaceUtils.ThreeTabs, "this.ContractHandler.SendRequestAsync(", variableName, ");\r\n", SpaceUtils.TwoTabs
			]);
			var str2: string = NString.Concat([
				SpaceUtils.TwoTabs, "member this.", text, "RequestAndWaitForReceiptAsync(", variableName, ": ", typeName, ", ?cancellationTokenSource : CancellationTokenSource): Task<TransactionReceipt> =\r\n", SpaceUtils.ThreeTabs, "let cancellationTokenSourceVal = defaultArg cancellationTokenSource null\r\n", SpaceUtils.ThreeTabs, "this.ContractHandler.SendRequestAndWaitForReceiptAsync(", variableName, ", cancellationTokenSourceVal);\r\n", SpaceUtils.TwoTabs
			]);
			return str + Environment.NewLine + str2;
		}
		return null;
	}
}
class ServiceFSharpTemplate extends ClassTemplateBase<ServiceModel>
{
	private _functionServiceMethodFSharpTemplate: FunctionServiceMethodFSharpTemplate = null;
	private _deploymentServiceMethodsFSharpTemplate: ContractDeploymentServiceMethodsFSharpTemplate = null;
	constructor(model: ServiceModel)
	{
		super(model);
		this._functionServiceMethodFSharpTemplate = new FunctionServiceMethodFSharpTemplate(model);
		this._deploymentServiceMethodsFSharpTemplate = new ContractDeploymentServiceMethodsFSharpTemplate(model);
		this.ClassFileTemplate = new FSharpClassFileTemplate(this.Model, this);
	}
	GenerateClass(): string
	{
		return NString.Concat([
			"\r\n", SpaceUtils.OneTab, "type ", this.Model.GetTypeName(), " (web3: Web3, contractAddress: string) =\r\n", SpaceUtils.OneTab, "\r\n", SpaceUtils.TwoTabs, "member val Web3 = web3 with get\r\n", SpaceUtils.TwoTabs, "member val ContractHandler = web3.Eth.GetContractHandler(contractAddress) with get\r\n", SpaceUtils.OneTab, "\r\n", this._deploymentServiceMethodsFSharpTemplate.GenerateMethods(), "\r\n", SpaceUtils.OneTab, "\r\n", this._functionServiceMethodFSharpTemplate.GenerateMethods(), "\r\n", 
			SpaceUtils.OneTab
		]);
	}
}
class ServiceGenerator extends ClassGeneratorBase<ClassTemplateBase<ServiceModel>, ServiceModel>
{
	get ContractABI(): ContractABI
	{
		return this._ContractABI_k__BackingField;
	}
	constructor(contractABI: ContractABI, contractName: string, byteCode: string, namespace: string, cqsNamespace: string, functionOutputNamespace: string, codeGenLanguage: CodeGenLanguage)
	{
		super();
		this._ContractABI_k__BackingField = contractABI;
		this.ClassModel = new ServiceModel(contractABI, contractName, byteCode, namespace, cqsNamespace, functionOutputNamespace);
		this.ClassModel.CodeGenLanguage = codeGenLanguage;
		this.InitialiseTemplate(codeGenLanguage);
	}
	InitialiseTemplate(codeGenLanguage: CodeGenLanguage): void
	{
		switch (codeGenLanguage)
		{
		case CodeGenLanguage.CSharp:
			this.ClassTemplate = new ServiceCSharpTemplate(this.ClassModel);
			return;
		case CodeGenLanguage.Vb:
			this.ClassTemplate = new ServiceVbTemplate(this.ClassModel);
			return;
		case CodeGenLanguage.FSharp:
			this.ClassTemplate = new ServiceFSharpTemplate(this.ClassModel);
			return;
		}
		throw new ArgumentOutOfRangeException("codeGenLanguage", codeGenLanguage, "Code generation not implemented for this language");
	}
}
class ServiceModel extends TypeMessageModel
{
	get ContractABI(): ContractABI
	{
		return this._ContractABI_k__BackingField;
	}
	get CQSNamespace(): string
	{
		return this._CQSNamespace_k__BackingField;
	}
	get FunctionOutputNamespace(): string
	{
		return this._FunctionOutputNamespace_k__BackingField;
	}
	get ContractDeploymentCQSMessageModel(): ContractDeploymentCQSMessageModel
	{
		return this._ContractDeploymentCQSMessageModel_k__BackingField;
	}
	constructor(contractABI: ContractABI, contractName: string, byteCode: string, namespace: string, cqsNamespace: string, functionOutputNamespace: string)
	{
		super(namespace, contractName, "Service");
		this._ContractABI_k__BackingField = contractABI;
		this._CQSNamespace_k__BackingField = cqsNamespace;
		this._FunctionOutputNamespace_k__BackingField = functionOutputNamespace;
		this._ContractDeploymentCQSMessageModel_k__BackingField = new ContractDeploymentCQSMessageModel(contractABI.Constructor, cqsNamespace, byteCode, contractName);
		this.InitialiseNamespaceDependencies();
		if (!NString.IsNullOrEmpty(cqsNamespace))
		{
			this.NamespaceDependencies.Add(cqsNamespace);
		}
		if (!NString.IsNullOrEmpty(functionOutputNamespace))
		{
			this.NamespaceDependencies.Add(functionOutputNamespace);
		}
	}
	private InitialiseNamespaceDependencies(): void
	{
		this.NamespaceDependencies.AddRange(NArray.ToEnumerable([
			"System", "System.Threading.Tasks", "System.Collections.Generic", "System.Numerics", "Nethereum.Hex.HexTypes", "Nethereum.ABI.FunctionEncoding.Attributes", "Nethereum.Web3", "Nethereum.RPC.Eth.DTOs", "Nethereum.Contracts.CQS", "Nethereum.Contracts.ContractHandlers", "Nethereum.Contracts", "System.Threading"
		]));
	}
}
class ContractDeploymentServiceMethodsVbTemplate extends NObject
{
	private _contractDeploymentCQSMessageModel: ContractDeploymentCQSMessageModel = null;
	private _serviceModel: ServiceModel = null;
	constructor(model: ServiceModel)
	{
		super();
		this._contractDeploymentCQSMessageModel = model.ContractDeploymentCQSMessageModel;
		this._serviceModel = model;
	}
	GenerateMethods(): string
	{
		var typeName: string = this._contractDeploymentCQSMessageModel.GetTypeName();
		var variableName: string = this._contractDeploymentCQSMessageModel.GetVariableName();
		var text: string = NString.Concat([
			SpaceUtils.TwoTabs, "Public Shared Function DeployContractAndWaitForReceiptAsync(ByVal web3 As Nethereum.Web3.Web3, ByVal ", variableName, " As ", typeName, ", ByVal Optional cancellationTokenSource As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.ThreeTabs, "Return web3.Eth.GetContractDeploymentHandler(Of ", typeName, ")().SendRequestAndWaitForReceiptAsync(", variableName, ", cancellationTokenSource)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "End Function"
		]);
		var text2: string = NString.Concat([
			SpaceUtils.TwoTabs, " Public Shared Function DeployContractAsync(ByVal web3 As Nethereum.Web3.Web3, ByVal ", variableName, " As ", typeName, ") As Task(Of String)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.ThreeTabs, "Return web3.Eth.GetContractDeploymentHandler(Of ", typeName, ")().SendRequestAsync(", variableName, ")\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "End Function"
		]);
		var text3: string = NString.Concat([
			SpaceUtils.TwoTabs, "Public Shared Async Function DeployContractAndGetServiceAsync(ByVal web3 As Nethereum.Web3.Web3, ByVal ", variableName, " As ", typeName, ", ByVal Optional cancellationTokenSource As CancellationTokenSource = Nothing) As Task(Of ", this._serviceModel.GetTypeName(), ")\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.ThreeTabs, "Dim receipt = Await DeployContractAndWaitForReceiptAsync(web3, ", variableName, ", cancellationTokenSource)\r\n", SpaceUtils.ThreeTabs, "Return New ", this._serviceModel.GetTypeName(), "(web3, receipt.ContractAddress)\r\n", SpaceUtils.TwoTabs, 
			"\r\n", SpaceUtils.TwoTabs, "End Function"
		]);
		return NString.Join(Environment.NewLine, [
			text, text2, text3
		]);
	}
}
class FunctionServiceMethodVbTemplate extends NObject
{
	private _model: ServiceModel = null;
	private _commonGenerators: CommonGenerators = null;
	private _typeConvertor: ITypeConvertor = null;
	private _parameterAbiFunctionDtoVbTemplate: ParameterABIFunctionDTOVbTemplate = null;
	constructor(model: ServiceModel)
	{
		super();
		this._model = model;
		this._typeConvertor = new ABITypeToVBType();
		this._commonGenerators = new CommonGenerators();
		this._parameterAbiFunctionDtoVbTemplate = new ParameterABIFunctionDTOVbTemplate();
	}
	GenerateMethods(): string
	{
		var functions: FunctionABI[] = this._model.ContractABI.Functions;
		return NString.Join(Environment.NewLine, Enumerable.Select<FunctionABI, string>(NArray.ToEnumerable(functions), this.GenerateMethod));
	}
	GenerateMethod(functionABI: FunctionABI): string
	{
		var arg_30_0: FunctionCQSMessageModel = new FunctionCQSMessageModel(functionABI, this._model.CQSNamespace);
		var functionOutputDTOModel: FunctionOutputDTOModel = new FunctionOutputDTOModel(functionABI, this._model.FunctionOutputNamespace);
		var functionABIModel: FunctionABIModel = new FunctionABIModel(functionABI, this._typeConvertor);
		var typeName: string = arg_30_0.GetTypeName();
		var variableName: string = arg_30_0.GetVariableName();
		var text: string = this._commonGenerators.GenerateClassName(functionABI.Name);
		if (functionABIModel.IsMultipleOutput() && !functionABIModel.IsTransaction())
		{
			var typeName2: string = functionOutputDTOModel.GetTypeName();
			var str: string = NString.Concat([
				SpaceUtils.TwoTabs, "Public Function ", text, "QueryAsync(ByVal ", variableName, " As ", typeName, ", ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of ", typeName2, ")\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.ThreeTabs, "Return ContractHandler.QueryDeserializingToObjectAsync(Of ", typeName, ", ", typeName2, ")(", variableName, 
				", blockParameter)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "End Function"
			]);
			var str2: string = NString.Concat([
				SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "Public Function ", text, "QueryAsync(ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of ", typeName2, ")\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.ThreeTabs, "return ContractHandler.QueryDeserializingToObjectAsync(Of ", typeName, ", ", typeName2, ")(Nothing, blockParameter)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, 
				"End Function\r\n"
			]);
			var str3: string = NString.Concat([
				SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "Public Function ", text, "QueryAsync(", this._parameterAbiFunctionDtoVbTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters), ", ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of ", typeName2, ")\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.ThreeTabs, "Dim ", variableName, " = New ", typeName, "()\r\n", this._parameterAbiFunctionDtoVbTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, variableName, SpaceUtils.FourTabs), 
				"\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, "Return ContractHandler.QueryDeserializingToObjectAsync(Of ", typeName, ", ", typeName2, ")(", variableName, ", blockParameter)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "End Function"
			]);
			if (functionABIModel.HasNoInputParameters())
			{
				return str + this.GenerateLineBreak() + str2 + this.GenerateLineBreak();
			}
			return str + this.GenerateLineBreak() + str3 + this.GenerateLineBreak();
		}
		else
		{
			if (functionABIModel.IsSingleOutput() && !functionABIModel.IsTransaction() && functionABI.OutputParameters !== null && functionABI.OutputParameters.length === 1 && functionABI.Constant)
			{
				var singleOutputReturnType: string = functionABIModel.GetSingleOutputReturnType();
				var str4: string = NString.Concat([
					SpaceUtils.TwoTabs, "Public Function ", text, "QueryAsync(ByVal ", variableName, " As ", typeName, ", ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of ", singleOutputReturnType, ")\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.ThreeTabs, "Return ContractHandler.QueryAsync(Of ", typeName, ", ", singleOutputReturnType, ")(", variableName, 
					", blockParameter)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "End Function"
				]);
				var str5: string = NString.Concat([
					SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "Public Function ", text, "QueryAsync(ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of ", singleOutputReturnType, ")\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.ThreeTabs, "return ContractHandler.QueryAsync(Of ", typeName, ", ", singleOutputReturnType, ")(Nothing, blockParameter)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, 
					"End Function\r\n"
				]);
				var str6: string = NString.Concat([
					SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "Public Function ", text, "QueryAsync(", this._parameterAbiFunctionDtoVbTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters), ", ByVal Optional blockParameter As BlockParameter = Nothing) As Task(Of ", singleOutputReturnType, ")\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.ThreeTabs, "Dim ", variableName, " = New ", typeName, "()\r\n", this._parameterAbiFunctionDtoVbTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, variableName, SpaceUtils.FourTabs), 
					"\r\n", SpaceUtils.ThreeTabs, "\r\n", SpaceUtils.ThreeTabs, "Return ContractHandler.QueryAsync(Of ", typeName, ", ", singleOutputReturnType, ")(", variableName, ", blockParameter)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "End Function"
				]);
				if (functionABIModel.HasNoInputParameters())
				{
					return str4 + this.GenerateLineBreak() + str5 + this.GenerateLineBreak();
				}
				return str4 + this.GenerateLineBreak() + str6 + this.GenerateLineBreak();
			}
			else
			{
				if (!functionABIModel.IsTransaction())
				{
					return null;
				}
				var text2: string = NString.Concat([
					SpaceUtils.TwoTabs, "Public Function ", text, "RequestAsync(ByVal ", variableName, " As ", typeName, ") As Task(Of String)\r\n", SpaceUtils.TwoTabs, "            \r\n", SpaceUtils.ThreeTabs, "Return ContractHandler.SendRequestAsync(Of ", typeName, ")(", variableName, ")\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, 
					"End Function"
				]);
				var text3: string = NString.Concat([
					SpaceUtils.TwoTabs, "Public Function ", text, "RequestAsync() As Task(Of String)\r\n", SpaceUtils.TwoTabs, "            \r\n", SpaceUtils.ThreeTabs, "Return ContractHandler.SendRequestAsync(Of ", typeName, ")\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "End Function"
				]);
				var text4: string = NString.Concat([
					SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "Public Function ", text, "RequestAsync(", this._parameterAbiFunctionDtoVbTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters), ") As Task(Of String)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.ThreeTabs, "Dim ", variableName, " = New ", typeName, "()\r\n", this._parameterAbiFunctionDtoVbTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, variableName, SpaceUtils.FourTabs), "\r\n", SpaceUtils.ThreeTabs, 
					"\r\n", SpaceUtils.ThreeTabs, "Return ContractHandler.SendRequestAsync(Of ", typeName, ")(", variableName, ")\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "End Function"
				]);
				var text5: string = NString.Concat([
					SpaceUtils.TwoTabs, "Public Function ", text, "RequestAndWaitForReceiptAsync(ByVal ", variableName, " As ", typeName, ", ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.ThreeTabs, "Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of ", typeName, ")(", variableName, ", cancellationToken)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, 
					"End Function"
				]);
				var text6: string = NString.Concat([
					SpaceUtils.TwoTabs, "Public Function ", text, "RequestAndWaitForReceiptAsync(ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.ThreeTabs, "Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of ", typeName, ")(Nothing, cancellationToken)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "End Function"
				]);
				var text7: string = NString.Concat([
					SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "Public Function ", text, "RequestAndWaitForReceiptAsync(", this._parameterAbiFunctionDtoVbTemplate.GenerateAllFunctionParameters(functionABIModel.FunctionABI.InputParameters), ", ByVal Optional cancellationToken As CancellationTokenSource = Nothing) As Task(Of TransactionReceipt)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.ThreeTabs, "Dim ", variableName, " = New ", typeName, "()\r\n", this._parameterAbiFunctionDtoVbTemplate.GenerateAssigmentFunctionParametersToProperties(functionABIModel.FunctionABI.InputParameters, variableName, SpaceUtils.FourTabs), "\r\n", SpaceUtils.ThreeTabs, 
					"\r\n", SpaceUtils.ThreeTabs, "Return ContractHandler.SendRequestAndWaitForReceiptAsync(Of ", typeName, ")(", variableName, ", cancellationToken)\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "End Function"
				]);
				if (functionABIModel.HasNoInputParameters())
				{
					return NString.Concat([
						text2, this.GenerateLineBreak(), text3, this.GenerateLineBreak(), text5, this.GenerateLineBreak(), text6
					]);
				}
				return NString.Concat([
					text2, this.GenerateLineBreak(), text5, this.GenerateLineBreak(), text4, this.GenerateLineBreak(), text7
				]);
			}
		}
	}
	private GenerateLineBreak(): string
	{
		return Environment.NewLine + Environment.NewLine;
	}
}
class ServiceVbTemplate extends ClassTemplateBase<ServiceModel>
{
	private _functionServiceMethodVbTemplate: FunctionServiceMethodVbTemplate = null;
	private _deploymentServiceMethodsVbTemplate: ContractDeploymentServiceMethodsVbTemplate = null;
	constructor(model: ServiceModel)
	{
		super(model);
		this._functionServiceMethodVbTemplate = new FunctionServiceMethodVbTemplate(model);
		this._deploymentServiceMethodsVbTemplate = new ContractDeploymentServiceMethodsVbTemplate(model);
		this.ClassFileTemplate = new VbClassFileTemplate(this.Model, this);
	}
	GenerateClass(): string
	{
		return NString.Concat([
			"\r\n", SpaceUtils.OneTab, "Public Partial Class ", this.Model.GetTypeName(), "\r\n", SpaceUtils.OneTab, "\r\n", SpaceUtils.OneTab, "\r\n", this._deploymentServiceMethodsVbTemplate.GenerateMethods(), "\r\n", SpaceUtils.OneTab, "\r\n", SpaceUtils.TwoTabs, "Protected Property Web3 As Nethereum.Web3.Web3\r\n", SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "Public Property ContractHandler As ContractHandler\r\n", 
			SpaceUtils.TwoTabs, "\r\n", SpaceUtils.TwoTabs, "Public Sub New(ByVal web3 As Nethereum.Web3.Web3, ByVal contractAddress As String)\r\n", SpaceUtils.ThreeTabs, "Web3 = web3\r\n", SpaceUtils.ThreeTabs, "ContractHandler = web3.Eth.GetContractHandler(contractAddress)\r\n", SpaceUtils.TwoTabs, "End Sub\r\n", SpaceUtils.OneTab, "\r\n", this._functionServiceMethodVbTemplate.GenerateMethods(), "\r\n", SpaceUtils.OneTab, "\r\n", SpaceUtils.OneTab, "End Class"
		]);
	}
}
interface IMessage<TParameter>
{
	InputParameters: TParameter[];
	Name: string;
}
class ConstructorABI extends NObject implements IMessage<ParameterABI>
{
	Name: string = null;
	InputParameters: ParameterABI[] = null;
	constructor()
	{
		super();
		this.InputParameters = new Array<ParameterABI>(0);
	}
}
class ContractABI extends NObject
{
	Functions: FunctionABI[] = null;
	Constructor: ConstructorABI = null;
	Events: EventABI[] = null;
	constructor()
	{
		super();
	}
}
class EventABI extends NObject
{
	get Name(): string
	{
		return this._Name_k__BackingField;
	}
	InputParameters: ParameterABI[] = null;
	constructor(name: string)
	{
		super();
		this._Name_k__BackingField = name;
	}
}
class FunctionABI extends NObject implements IMessage<ParameterABI>
{
	Serpent: boolean = false;
	Constant: boolean = false;
	Name: string = null;
	InputParameters: ParameterABI[] = null;
	OutputParameters: ParameterABI[] = null;
	constructor(name: string, constant: boolean, serpent: boolean = false)
	{
		super();
		this.Name = name;
		this.Serpent = serpent;
		this.Constant = constant;
	}
}
class Parameter extends NObject
{
	Name: string = null;
	Type: string = null;
	Order: number = 0;
	constructor(name: string, type: string, order: number)
	{
		super();
		this.Name = name;
		this.Type = type;
		this.Order = order;
	}
}
class ParameterABI extends Parameter
{
	Indexed: boolean = false;
	constructor(type: string, name: string, order: number);
	constructor(type: string, order: number);
	constructor(type: string, nameOrOrder: any = null, order: number = 1)
	{
		super(nameOrOrder, type, order);
		if (arguments.length === 3 && (type === null || type.constructor === String) && (nameOrOrder === null || nameOrOrder.constructor === String) && (order === null || order.constructor === Number))
		{
			this.constructor_0(type, nameOrOrder, order);
			return;
		}
		this.constructor_1(type, nameOrOrder);
	}
	private constructor_0(type: string, name: string, order: number): void
	{
	}
	private constructor_1(type: string, order: number): void
	{
		this.constructor_0(type, null, order);
	}
}
class FunctionABIModel extends NObject
{
	private _abiTypeToDotnetTypeConvertor: ITypeConvertor = null;
	get FunctionABI(): FunctionABI
	{
		return this._FunctionABI_k__BackingField;
	}
	constructor(functionABI: FunctionABI, abiTypeToDotnetTypeConvertor: ITypeConvertor)
	{
		super();
		this._FunctionABI_k__BackingField = functionABI;
		this._abiTypeToDotnetTypeConvertor = abiTypeToDotnetTypeConvertor;
	}
	GetSingleOutputReturnType(): string
	{
		if (this.FunctionABI.OutputParameters !== null && this.FunctionABI.OutputParameters.length === 1)
		{
			return this._abiTypeToDotnetTypeConvertor.Convert(this.FunctionABI.OutputParameters[0].Type, true);
		}
		return null;
	}
	GetSingleAbiReturnType(): string
	{
		if (this.FunctionABI.OutputParameters !== null && this.FunctionABI.OutputParameters.length === 1)
		{
			return this.FunctionABI.OutputParameters[0].Type;
		}
		return null;
	}
	IsMultipleOutput(): boolean
	{
		return this.FunctionABI.OutputParameters !== null && this.FunctionABI.OutputParameters.length > 1;
	}
	IsSingleOutput(): boolean
	{
		return this.FunctionABI.OutputParameters !== null && this.FunctionABI.OutputParameters.length === 1;
	}
	HasNoInputParameters(): boolean
	{
		return this.FunctionABI.InputParameters === null || this.FunctionABI.InputParameters.length === 0;
	}
	HasNoReturn(): boolean
	{
		return this.FunctionABI.OutputParameters === null || this.FunctionABI.OutputParameters.length === 0;
	}
	IsTransaction(): boolean
	{
		return !this.FunctionABI.Constant;
	}
}
class ParameterModel<TParameter> extends NObject
{
	Parameter: TParameter = null;
	get CommonGenerators(): CommonGenerators
	{
		return this._CommonGenerators_k__BackingField;
	}
	constructor();
	constructor(parameter: TParameter);
	constructor(parameter?: TParameter)
	{
		super();
		if (arguments.length === 0)
		{
			this.constructor_0();
			return;
		}
		this.constructor_1(parameter);
	}
	private constructor_0(): void
	{
		this._CommonGenerators_k__BackingField = new CommonGenerators();
	}
	private constructor_1(parameter: TParameter): void
	{
		this.constructor_0();
		this.Parameter = parameter;
	}
	GetVariableName(): string
	{
		return this.CommonGenerators.GenerateVariableName(this.Parameter.Name);
	}
	GetPropertyName(): string
	{
		return this.CommonGenerators.GeneratePropertyName(this.Parameter.Name);
	}
}
class ParameterABIModel extends ParameterModel<ParameterABI>
{
	static AnonymousInputParameterPrefix: string = "ParamValue";
	static AnonymousOutputParameterPrefix: string = "ReturnValue";
	constructor(parameter: ParameterABI);
	constructor();
	constructor(parameter?: ParameterABI)
	{
		super(parameter);
		if (arguments.length === 1 && (parameter === null || parameter instanceof ParameterABI))
		{
			this.constructor_0(parameter);
			return;
		}
		this.constructor_1();
	}
	private constructor_0(parameter: ParameterABI): void
	{
	}
	private constructor_1(): void
	{
	}
	GetVariableName(): string;
	GetVariableName(name: string, order: number): string;
	GetVariableName(name?: string, order?: number): string
	{
		if (arguments.length === 0)
		{
			return this.GetVariableName_0();
		}
		return this.GetVariableName_1(name, order);
	}
	private GetVariableName_0(): string
	{
		return this.GetVariableName(this.Parameter.Name, this.Parameter.Order);
	}
	GetPropertyName(): string;
	GetPropertyName(parameterDirection: ParameterDirection): string;
	GetPropertyName(name: string, order: number, parameterDirection: ParameterDirection): string;
	GetPropertyName(parameterDirectionOrName?: any, order?: number, parameterDirection: ParameterDirection = ParameterDirection.Output): string
	{
		if (arguments.length === 0)
		{
			return this.GetPropertyName_0();
		}
		if (arguments.length === 1 && (parameterDirectionOrName === null || parameterDirectionOrName.constructor === Number))
		{
			return this.GetPropertyName_1(parameterDirectionOrName);
		}
		return this.GetPropertyName_2(parameterDirectionOrName, order, parameterDirection);
	}
	private GetPropertyName_0(): string
	{
		return this.GetPropertyName(this.Parameter.Name, this.Parameter.Order, ParameterDirection.Output);
	}
	private GetPropertyName_1(parameterDirection: ParameterDirection): string
	{
		return this.GetPropertyName(this.Parameter.Name, this.Parameter.Order, parameterDirection);
	}
	private GetVariableName_1(name: string, order: number): string
	{
		return this.CommonGenerators.GenerateVariableName(this.NameOrDefault(name, order, ParameterDirection.Output));
	}
	private GetPropertyName_2(name: string, order: number, parameterDirection: ParameterDirection): string
	{
		if (NString.IsNullOrEmpty(name))
		{
			name = this.NameOrDefault(name, order, parameterDirection);
		}
		return this.CommonGenerators.GeneratePropertyName(name);
	}
	private NameOrDefault(name: string, order: number, parameterDirection: ParameterDirection = ParameterDirection.Output): string
	{
		if (!NString.IsNullOrEmpty(name))
		{
			return name;
		}
		var arg: string = (parameterDirection === ParameterDirection.Input) ? "ParamValue" : "ReturnValue";
		return NString.Format("{0}{1}", arg, order);
	}
}
class ParameterABIModelTypeMap extends NObject
{
	private _typeConvertor: ITypeConvertor = null;
	constructor(typeConvertor: ITypeConvertor)
	{
		super();
		this._typeConvertor = typeConvertor;
	}
	GetParameterDotNetOutputMapType(parameter: ParameterABI): string
	{
		return this._typeConvertor.Convert(parameter.Type, true);
	}
	GetParameterDotNetInputMapType(parameter: ParameterABI): string
	{
		return this._typeConvertor.Convert(parameter.Type, false);
	}
}
interface ITypeConvertor
{
	Convert(typeName: string, outputArrayAsList: boolean = false): string;
}
class ABITypeToDotNetTypeBase extends NObject implements ITypeConvertor
{
	Convert(typeName: string, outputArrayAsList: boolean = false): string
	{
		var num: number = NString.IndexOf(typeName, "[");
		var arg_2C_1: (arg: number) => boolean;
		if ((arg_2C_1 = ABITypeToDotNetTypeBase___c.__9__0_0) === null)
		{
			arg_2C_1 = (ABITypeToDotNetTypeBase___c.__9__0_0 = ABITypeToDotNetTypeBase___c.__9._Convert_b__0_0);
		}
		var numberOfArrays: number = Enumerable.Count<number>(typeName, arg_2C_1);
		if (num > -1)
		{
			var typeName2: string = NString.Substring(typeName, 0, num);
			if (outputArrayAsList)
			{
				return this.GetListType(this.Convert(typeName2, true), numberOfArrays);
			}
			return this.GetArrayType(this.Convert(typeName2, false));
		}
		else
		{
			if ("bool" === typeName)
			{
				return this.GetBooleanType();
			}
			if (NString.StartsWith(typeName, "int"))
			{
				if (typeName.length === 3)
				{
					return this.GetBigIntegerType();
				}
				var num2: number = NNumber.Parse(NString.Substring(typeName, 3));
				if (num2 > 64)
				{
					return this.GetBigIntegerType();
				}
				if (num2 <= 64 && num2 > 32)
				{
					return this.GetLongType();
				}
				if (num2 === 32)
				{
					return this.GetIntType();
				}
				if (num2 === 16)
				{
					return this.GetShortType();
				}
				if (num2 === 8)
				{
					return this.GetSByteType();
				}
			}
			if (NString.StartsWith(typeName, "uint"))
			{
				if (typeName.length === 4)
				{
					return this.GetBigIntegerType();
				}
				var num3: number = NNumber.Parse(NString.Substring(typeName, 4));
				if (num3 > 64)
				{
					return this.GetBigIntegerType();
				}
				if (num3 <= 64 && num3 > 32)
				{
					return this.GetULongType();
				}
				if (num3 === 32)
				{
					return this.GetUIntType();
				}
				if (num3 === 16)
				{
					return this.GetUShortType();
				}
				if (num3 === 8)
				{
					return this.GetByteType();
				}
			}
			if (typeName === "address")
			{
				return this.GetStringType();
			}
			if (typeName === "string")
			{
				return this.GetStringType();
			}
			if (typeName === "bytes")
			{
				return this.GetByteArrayType();
			}
			if (NString.StartsWith(typeName, "bytes"))
			{
				return this.GetByteArrayType();
			}
			return null;
		}
	}
	GetLongType(): string
	{
		throw new NotSupportedException();
	}
	GetULongType(): string
	{
		throw new NotSupportedException();
	}
	GetIntType(): string
	{
		throw new NotSupportedException();
	}
	GetUIntType(): string
	{
		throw new NotSupportedException();
	}
	GetShortType(): string
	{
		throw new NotSupportedException();
	}
	GetUShortType(): string
	{
		throw new NotSupportedException();
	}
	GetByteType(): string
	{
		throw new NotSupportedException();
	}
	GetSByteType(): string
	{
		throw new NotSupportedException();
	}
	GetByteArrayType(): string
	{
		throw new NotSupportedException();
	}
	GetStringType(): string
	{
		throw new NotSupportedException();
	}
	GetBooleanType(): string
	{
		throw new NotSupportedException();
	}
	GetBigIntegerType(): string
	{
		throw new NotSupportedException();
	}
	GetArrayType(type: string): string
	{
		throw new NotSupportedException();
	}
	GetListType(type: string, numberOfArrays: number = 1): string
	{
		throw new NotSupportedException();
	}
	constructor()
	{
		super();
	}
}
class ABITypeToCSharpType extends ABITypeToDotNetTypeBase
{
	GetLongType(): string
	{
		return "long";
	}
	GetULongType(): string
	{
		return "ulong";
	}
	GetIntType(): string
	{
		return "int";
	}
	GetUIntType(): string
	{
		return "uint";
	}
	GetShortType(): string
	{
		return "short";
	}
	GetUShortType(): string
	{
		return "ushort";
	}
	GetByteType(): string
	{
		return "byte";
	}
	GetSByteType(): string
	{
		return "sbyte";
	}
	GetByteArrayType(): string
	{
		return "byte[]";
	}
	GetStringType(): string
	{
		return "string";
	}
	GetBooleanType(): string
	{
		return "bool";
	}
	GetBigIntegerType(): string
	{
		return "BigInteger";
	}
	GetArrayType(type: string): string
	{
		return type + "[]";
	}
	GetListType(type: string, numberOfArrays: number = 1): string
	{
		var text: string = type;
		for (var i: number = 0; i < numberOfArrays; i = i + 1)
		{
			text = "List<" + text + ">";
		}
		return text;
	}
	constructor()
	{
		super();
	}
}
class ABITypeToDotNetTypeBase___c extends NObject
{
	static __9: ABITypeToDotNetTypeBase___c = new ABITypeToDotNetTypeBase___c();
	static __9__0_0: (arg: number) => boolean = null;
	_Convert_b__0_0(x: number): boolean
	{
		return x === 91/*'['*/;
	}
	constructor()
	{
		super();
	}
}
class ABITypeToFSharpType extends ABITypeToDotNetTypeBase
{
	GetLongType(): string
	{
		return "long";
	}
	GetULongType(): string
	{
		return "ulong";
	}
	GetIntType(): string
	{
		return "int";
	}
	GetUIntType(): string
	{
		return "uint";
	}
	GetShortType(): string
	{
		return "short";
	}
	GetUShortType(): string
	{
		return "ushort";
	}
	GetByteType(): string
	{
		return "byte";
	}
	GetSByteType(): string
	{
		return "sbyte";
	}
	GetByteArrayType(): string
	{
		return "byte[]";
	}
	GetStringType(): string
	{
		return "string";
	}
	GetBooleanType(): string
	{
		return "bool";
	}
	GetBigIntegerType(): string
	{
		return "BigInteger";
	}
	GetArrayType(type: string): string
	{
		return type + "[]";
	}
	GetListType(type: string, numberOfArrays: number = 1): string
	{
		var text: string = type;
		for (var i: number = 0; i < numberOfArrays; i = i + 1)
		{
			text = "List<" + text + ">";
		}
		return text;
	}
	constructor()
	{
		super();
	}
}
class ABITypeToVBType extends ABITypeToDotNetTypeBase
{
	GetLongType(): string
	{
		return "Long";
	}
	GetULongType(): string
	{
		return "ULong";
	}
	GetIntType(): string
	{
		return "Integer";
	}
	GetUIntType(): string
	{
		return "UInteger";
	}
	GetShortType(): string
	{
		return "Short";
	}
	GetUShortType(): string
	{
		return "UShort";
	}
	GetByteType(): string
	{
		return "Byte";
	}
	GetSByteType(): string
	{
		return "SByte";
	}
	GetByteArrayType(): string
	{
		return "Byte()";
	}
	GetStringType(): string
	{
		return "String";
	}
	GetBooleanType(): string
	{
		return "Boolean";
	}
	GetBigIntegerType(): string
	{
		return "BigInteger";
	}
	GetArrayType(type: string): string
	{
		return type + "()";
	}
	GetListType(type: string, numberOfArrays: number = 1): string
	{
		var text: string = type;
		for (var i: number = 0; i < numberOfArrays; i = i + 1)
		{
			text = "List(Of " + text + ")";
		}
		return text;
	}
	constructor()
	{
		super();
	}
}
enum CodeGenLanguage
{
	CSharp,
	Vb,
	Proto,
	FSharp
}
class CodeGenLanguageExt extends NObject
{
	static ProjectFileExtensions: Dictionary<CodeGenLanguage, string> = null;
	static LanguageMappings: Dictionary<string, CodeGenLanguage> = null;
	static DotNetCliLanguage: Dictionary<CodeGenLanguage, string> = null;
	static GetValidProjectFileExtensions(): IEnumerable<string>
	{
		return CodeGenLanguageExt.ProjectFileExtensions.Values;
	}
	static ParseLanguage(languageTag: string): CodeGenLanguage
	{
		if (CodeGenLanguageExt.LanguageMappings.ContainsKey(languageTag))
		{
			return CodeGenLanguageExt.LanguageMappings.get_Item(languageTag);
		}
		throw new ArgumentException("Unknown or unsupported language '" + languageTag + "'");
	}
	static ToDotNetCli(language: CodeGenLanguage): string
	{
		if (CodeGenLanguageExt.DotNetCliLanguage.ContainsKey(language))
		{
			return CodeGenLanguageExt.DotNetCliLanguage.get_Item(language);
		}
		throw new ArgumentException(NString.Format("Language isn't supported by dot net cli '{0}'", language));
	}
	static AddProjectFileExtension(language: CodeGenLanguage, projectFileName: string): string
	{
		if (NString.IsNullOrEmpty(projectFileName))
		{
			throw new ArgumentNullException("projectFileName");
		}
		if (!CodeGenLanguageExt.ProjectFileExtensions.ContainsKey(language))
		{
			return null;
		}
		var text: string = CodeGenLanguageExt.ProjectFileExtensions.get_Item(language);
		if (NString.EndsWith(projectFileName, text, StringComparison.InvariantCultureIgnoreCase))
		{
			return projectFileName;
		}
		return projectFileName + text;
	}
	static GetCodeGenLanguageFromProjectFile(projectFilePath: string): CodeGenLanguage
	{
		var extension: string = Path.GetExtension(projectFilePath);
		var enumerator: Dictionary_KeyCollection_Enumerator<CodeGenLanguage, string> = CodeGenLanguageExt.ProjectFileExtensions.Keys.GetEnumerator();
		try
		{
			while (enumerator.MoveNext())
			{
				var current: CodeGenLanguage = enumerator.Current;
				if (CodeGenLanguageExt.ProjectFileExtensions.get_Item(current).Equals(extension, StringComparison.InvariantCultureIgnoreCase))
				{
					return current;
				}
			}
		}
		finally
		{
			(<IDisposable>enumerator).Dispose();
		}
		throw new ArgumentException("Unsupported or unrecognised file extension for project file path '" + projectFilePath + "'");
	}
	static GetCodeOutputFileExtension(codeGenLanguage: CodeGenLanguage): string
	{
		if (codeGenLanguage === CodeGenLanguage.CSharp)
		{
			return "cs";
		}
		if (codeGenLanguage === CodeGenLanguage.Vb)
		{
			return "vb";
		}
		if (codeGenLanguage === CodeGenLanguage.Proto)
		{
			return "proto";
		}
		if (codeGenLanguage === CodeGenLanguage.FSharp)
		{
			return "fs";
		}
		throw new ArgumentOutOfRangeException("codeGenLanguage", codeGenLanguage, null);
	}
	static CodeGenLanguageExt_cctor(): void
	{
		// Note: this type is marked as 'beforefieldinit'.
		var expr_05: Dictionary<CodeGenLanguage, string> = new Dictionary<CodeGenLanguage, string>();
		expr_05.Add(CodeGenLanguage.CSharp, ".csproj");
		expr_05.Add(CodeGenLanguage.FSharp, ".fsproj");
		expr_05.Add(CodeGenLanguage.Vb, ".vbproj");
		CodeGenLanguageExt.ProjectFileExtensions = expr_05;
		var expr_38: Dictionary<string, CodeGenLanguage> = new Dictionary<string, CodeGenLanguage>(new CodeGenLanguageExt_StringComparerIgnoreCase());
		expr_38.Add("C#", CodeGenLanguage.CSharp);
		expr_38.Add("CSharp", CodeGenLanguage.CSharp);
		expr_38.Add("F#", CodeGenLanguage.FSharp);
		expr_38.Add("FSharp", CodeGenLanguage.FSharp);
		expr_38.Add("VB", CodeGenLanguage.Vb);
		CodeGenLanguageExt.LanguageMappings = expr_38;
		var expr_7E: Dictionary<CodeGenLanguage, string> = new Dictionary<CodeGenLanguage, string>();
		expr_7E.Add(CodeGenLanguage.CSharp, "C#");
		expr_7E.Add(CodeGenLanguage.FSharp, "F#");
		expr_7E.Add(CodeGenLanguage.Vb, "VB");
		CodeGenLanguageExt.DotNetCliLanguage = expr_7E;
	}
}
class CodeGenLanguageExt_StringComparerIgnoreCase extends NObject implements IEqualityComparer<string>
{
	Equals(x: string, y: string): boolean
	{
		return this.GetHashCode(x) === this.GetHashCode(y);
	}
	GetHashCode(obj: string): number
	{
		if (obj === null)
		{
			throw new ArgumentNullException("obj");
		}
		return NString.GetHashCode(NString.ToLowerInvariant(obj));
	}
	constructor()
	{
		super();
	}
}
class CommonGenerators extends NObject
{
	private utils: Utils = null;
	constructor()
	{
		super();
		this.utils = new Utils();
	}
	GenerateVariableName(value: string): string
	{
		return this.utils.LowerCaseFirstCharAndRemoveUnderscorePrefix(value);
	}
	GeneratePropertyName(value: string): string
	{
		return this.utils.CapitaliseFirstCharAndRemoveUnderscorePrefix(value);
	}
	GenerateClassName(value: string): string
	{
		return this.utils.CapitaliseFirstCharAndRemoveUnderscorePrefix(value);
	}
}
class GeneratedFile extends NObject
{
	get GeneratedCode(): string
	{
		return this._GeneratedCode_k__BackingField;
	}
	get FileName(): string
	{
		return this._FileName_k__BackingField;
	}
	get OutputFolder(): string
	{
		return this._OutputFolder_k__BackingField;
	}
	constructor(generatedCode: string, fileName: string, outputFolder: string)
	{
		super();
		this._GeneratedCode_k__BackingField = generatedCode;
		this._FileName_k__BackingField = fileName;
		this._OutputFolder_k__BackingField = outputFolder;
	}
}
class MessageMap<MFrom, MTo, PFrom, PTo> extends NObject
{
	From: MFrom = null;
	To: MTo = null;
	ParameterMaps: List<ParameterMap<PFrom, PTo>> = null;
	constructor(from: MFrom, to: MTo, parameterMaps: List<ParameterMap<PFrom, PTo>>)
	{
		super();
		this.From = from;
		this.To = to;
		this.ParameterMaps = parameterMaps;
	}
}
enum ParameterDirection
{
	Input,
	Output
}
class ParameterMap<T1, T2> extends NObject
{
	From: T1 = null;
	To: T2 = null;
	constructor(from: T1, to: T2)
	{
		super();
		this.From = from;
		this.To = to;
	}
}
class ParameterMapperAssignerTemplate<TParameterModelFrom, TParameterModelTo, TParameterFrom, TParameterTo> extends NObject
{
	ConversionFormatStrings: Dictionary<string, Dictionary<string, string>> = new Dictionary<string, Dictionary<string, string>>();
	GenerateMappingAssigment(map: ParameterMap<TParameterFrom, TParameterTo>, variableSourceName: string, destinationVariableName: string): string;
	GenerateMappingAssigment(map: ParameterMap<TParameterFrom, TParameterTo>, variableSourceName: string): string;
	GenerateMappingAssigment(map: ParameterMap<TParameterFrom, TParameterTo>, variableSourceName: string, destinationVariableName?: string): string
	{
		if (arguments.length === 3 && (map === null || map instanceof ParameterMap) && (variableSourceName === null || variableSourceName.constructor === String) && (destinationVariableName === null || destinationVariableName.constructor === String))
		{
			return this.GenerateMappingAssigment_0(map, variableSourceName, destinationVariableName);
		}
		return this.GenerateMappingAssigment_1(map, variableSourceName);
	}
	private GenerateMappingAssigment_0(map: ParameterMap<TParameterFrom, TParameterTo>, variableSourceName: string, destinationVariableName: string): string
	{
		return destinationVariableName + "." + this.GenerateMappingAssigment(map, variableSourceName);
	}
	private GenerateMappingAssigment_1(map: ParameterMap<TParameterFrom, TParameterTo>, variableSourceName: string): string
	{
		var tParameterModelFrom: TParameterModelFrom = Activator.CreateInstance<TParameterModelFrom>();
		tParameterModelFrom.Parameter = map.From;
		var tParameterModelTo: TParameterModelTo = Activator.CreateInstance<TParameterModelTo>();
		tParameterModelTo.Parameter = map.To;
		var conversionFormatString: string = this.GetConversionFormatString(map.From.Type, map.To.Type);
		if (conversionFormatString !== null)
		{
			var arg: string = variableSourceName + "." + tParameterModelFrom.GetPropertyName();
			var str: string = NString.Format(conversionFormatString, arg);
			return tParameterModelTo.GetPropertyName() + " = " + str;
		}
		return NString.Concat([
			tParameterModelTo.GetPropertyName(), " = ", variableSourceName, ".", tParameterModelFrom.GetPropertyName()
		]);
	}
	GenerateMappingsReturn(map: ParameterMap<TParameterFrom, TParameterTo>, variableSourceName: string, destinationVariableName: string): string;
	GenerateMappingsReturn(map: ParameterMap<TParameterFrom, TParameterTo>, variableSourceName: string): string;
	GenerateMappingsReturn(map: ParameterMap<TParameterFrom, TParameterTo>, variableSourceName: string, destinationVariableName?: string): string
	{
		if (arguments.length === 3 && (map === null || map instanceof ParameterMap) && (variableSourceName === null || variableSourceName.constructor === String) && (destinationVariableName === null || destinationVariableName.constructor === String))
		{
			return this.GenerateMappingsReturn_0(map, variableSourceName, destinationVariableName);
		}
		return this.GenerateMappingsReturn_1(map, variableSourceName);
	}
	private GenerateMappingsReturn_0(map: ParameterMap<TParameterFrom, TParameterTo>, variableSourceName: string, destinationVariableName: string): string
	{
		return destinationVariableName + "." + this.GenerateMappingsReturn(map, variableSourceName);
	}
	private GenerateMappingsReturn_1(map: ParameterMap<TParameterFrom, TParameterTo>, variableSourceName: string): string
	{
		var tParameterModelFrom: TParameterModelFrom = Activator.CreateInstance<TParameterModelFrom>();
		tParameterModelFrom.Parameter = map.From;
		var tParameterModelTo: TParameterModelTo = Activator.CreateInstance<TParameterModelTo>();
		tParameterModelTo.Parameter = map.To;
		var conversionFormatString: string = this.GetConversionFormatString(map.To.Type, map.From.Type);
		if (conversionFormatString !== null)
		{
			var arg: string = variableSourceName + "." + tParameterModelTo.GetPropertyName();
			var str: string = NString.Format(conversionFormatString, arg);
			return tParameterModelFrom.GetPropertyName() + " = " + str;
		}
		return NString.Concat([
			tParameterModelFrom.GetPropertyName(), " = ", variableSourceName, ".", tParameterModelTo.GetPropertyName()
		]);
	}
	GetConversionFormatString(typeFrom: string, typeTo: string): string
	{
		if (this.ConversionFormatStrings.ContainsKey(typeFrom) && this.ConversionFormatStrings.get_Item(typeFrom).ContainsKey(typeTo))
		{
			return this.ConversionFormatStrings.get_Item(typeFrom).get_Item(typeTo);
		}
		return null;
	}
	AddConversionFormatString(fromType: string, toType: string, conversionTemplate: string): void
	{
		var arg_14_0: Dictionary<string, Dictionary<string, string>> = this.ConversionFormatStrings;
		var expr_0C: Dictionary<string, string> = new Dictionary<string, string>();
		expr_0C.Add(fromType, conversionTemplate);
		arg_14_0.Add(toType, expr_0C);
	}
	constructor()
	{
		super();
	}
}
class ParameterMapperAssignerCSharpTemplate<TParameterModelFrom, TParameterModelTo, TParameterFrom, TParameterTo> extends ParameterMapperAssignerTemplate<TParameterModelFrom, TParameterModelTo, TParameterFrom, TParameterTo>
{
	constructor()
	{
		super();
	}
}
class ParameterMapperAssignerVbTemplate<TParameterModelFrom, TParameterModelTo, TParameterFrom, TParameterTo> extends ParameterMapperAssignerTemplate<TParameterModelFrom, TParameterModelTo, TParameterFrom, TParameterTo>
{
	constructor()
	{
		super();
	}
}
class SpaceUtils extends NObject
{
	static NoTabs: string = "";
	static OneTab: string = "    ";
	static TwoTabs: string = SpaceUtils.OneTab + SpaceUtils.OneTab;
	static ThreeTabs: string = SpaceUtils.TwoTabs + SpaceUtils.OneTab;
	static FourTabs: string = SpaceUtils.ThreeTabs + SpaceUtils.OneTab;
	static FiveTabs: string = SpaceUtils.FourTabs + SpaceUtils.OneTab;
	constructor()
	{
		super();
	}
}
class Utils extends NObject
{
	RemoveUnderscorePrefix(value: string): string
	{
		return ((value !== null) ? NString.TrimStart(value, [
			95
		]/*'_'*/) : null) || NString.Empty;
	}
	LowerCaseFirstCharAndRemoveUnderscorePrefix(value: string): string
	{
		value = this.RemoveUnderscorePrefix(value);
		return this.LowerCaseFirstChar(value);
	}
	CapitaliseFirstCharAndRemoveUnderscorePrefix(value: string): string
	{
		value = this.RemoveUnderscorePrefix(value);
		return this.CapitaliseFirstChar(value);
	}
	LowerCaseFirstChar(value: string): string
	{
		return NString.Substring(value, 0, 1).ToLower() + NString.Substring(value, 1);
	}
	CapitaliseFirstChar(value: string): string
	{
		return NString.Substring(value, 0, 1).ToUpper() + NString.Substring(value, 1);
	}
	GetBooleanAsString(value: boolean): string
	{
		if (value)
		{
			return "true";
		}
		return "false";
	}
	constructor()
	{
		super();
	}
}

{
	CodeGenLanguageExt.CodeGenLanguageExt_cctor();
}
