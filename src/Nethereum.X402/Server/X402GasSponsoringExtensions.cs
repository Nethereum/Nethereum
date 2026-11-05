using Nethereum.X402.Models;

namespace Nethereum.X402.Server;

public static class X402GasSponsoringExtensions
{
    private const string AddressPattern = "^0x[a-fA-F0-9]{40}$";
    private const string NumberPattern = "^[0-9]+$";

    public static Dictionary<string, object> DeclareEip2612() => new()
    {
        ["info"] = new Dictionary<string, object>
        {
            ["description"] = "The facilitator accepts EIP-2612 gasless Permit to `Permit2` canonical contract.",
            ["version"] = "1"
        },
        ["schema"] = new Dictionary<string, object>
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["from"] = Property(AddressPattern, "The address of the sender."),
                ["asset"] = Property(AddressPattern, "The address of the ERC-20 token contract."),
                ["spender"] = Property(AddressPattern, "The address of the spender (Canonical Permit2)."),
                ["amount"] = Property(NumberPattern, "The amount to approve (uint256). Typically MaxUint."),
                ["nonce"] = Property(NumberPattern, "The current nonce of the sender."),
                ["deadline"] = Property(NumberPattern, "The timestamp at which the signature expires."),
                ["signature"] = Property("^0x[a-fA-F0-9]+$", "The 65-byte concatenated signature (r, s, v) as a hex string."),
                ["version"] = Property("^[0-9]+(\\.[0-9]+)*$", "Schema version identifier.")
            },
            ["required"] = new[] { "from", "asset", "spender", "amount", "nonce", "deadline", "signature", "version" }
        }
    };

    public static Dictionary<string, object> DeclareErc20Approval() => new()
    {
        ["info"] = new Dictionary<string, object>
        {
            ["description"] = "The facilitator broadcasts a pre-signed ERC-20 approve() transaction to grant Permit2 allowance.",
            ["version"] = "1"
        },
        ["schema"] = new Dictionary<string, object>
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["from"] = Property(AddressPattern, "The address of the sender (token owner)."),
                ["asset"] = Property(AddressPattern, "The address of the ERC-20 token contract."),
                ["spender"] = Property(AddressPattern, "The address of the spender (Canonical Permit2)."),
                ["amount"] = Property(NumberPattern, "The amount approved (uint256). Always MaxUint256."),
                ["signedTransaction"] = Property("^0x[a-fA-F0-9]+$", "The RLP-encoded signed EIP-1559 transaction as a hex string."),
                ["version"] = Property("^[0-9]+(\\.[0-9]+)*$", "Schema version identifier.")
            },
            ["required"] = new[] { "from", "asset", "spender", "amount", "signedTransaction", "version" }
        }
    };

    private static Dictionary<string, object> Property(string pattern, string description) => new()
    {
        ["type"] = "string",
        ["pattern"] = pattern,
        ["description"] = description
    };
}
