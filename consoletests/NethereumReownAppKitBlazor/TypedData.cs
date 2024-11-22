using Nethereum.ABI.FunctionEncoding.Attributes;

namespace NethereumReownAppKitBlazor;
[Struct("Mail")]
public class Mail {
	[Parameter("tuple", "from", 1, "Person")]
	public required Person From { get; set; }

	[Parameter("tuple[]", "to", 2, "Person[]")]
	public required List<Person> To { get; set; }

	[Parameter("string", "contents", 3)]
	public required string Contents { get; set; }
}

[Struct("Person")]
public class Person {
	[Parameter("string", "name", 1)]
	public required string Name { get; set; }

	[Parameter("address[]", "wallets", 2)]
	public required List<string> Wallets { get; set; }
}

[Struct("Group")]
public class Group {
	[Parameter("string", "name", 1)]
	public required string Name { get; set; }

	[Parameter("tuple[]", "members", 2, "Person[]")]
	public required List<Person> Members { get; set; }
}
