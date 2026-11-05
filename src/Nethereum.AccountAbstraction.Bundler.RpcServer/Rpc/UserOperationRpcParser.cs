using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using RpcUserOperation = Nethereum.RPC.AccountAbstraction.DTOs.UserOperation;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc
{
    public static class UserOperationRpcParser
    {
        private const int AddressHexLength = 40;
        private const string Eip7702FactorySentinel = "0x7702";

        private static readonly string[] UserOperationFieldNames =
        {
            "sender", "nonce", "factory", "factoryData", "callData", "callGasLimit", "verificationGasLimit",
            "preVerificationGas", "maxFeePerGas", "maxPriorityFeePerGas", "paymasterVerificationGasLimit",
            "paymasterPostOpGasLimit", "signature", "paymaster", "paymasterData", "eip7702Auth"
        };

        private static readonly string[] AuthorisationFieldNames = { "chainId", "address", "nonce", "yParity", "r", "s" };
        private static readonly BigInteger MaxUint64 = (BigInteger.One << 64) - 1;
        private static readonly BigInteger MaxUint128 = (BigInteger.One << 128) - 1;
        private static readonly BigInteger MaxUint256 = (BigInteger.One << 256) - 1;

        public static RpcUserOperation ParseUserOperation(JsonElement element)
        {
            var userOp = ParseFields(element);
            RequireSendFields(userOp);
            return userOp;
        }

        public static RpcUserOperation ParseUserOperationForEstimate(JsonElement element)
        {
            var userOp = ParseFields(element);
            Require(userOp.Sender, "sender");
            return userOp;
        }

        private static RpcUserOperation ParseFields(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw RpcException.InvalidParams("UserOperation must be a JSON object");
            }

            var fields = ReadFields(element, UserOperationFieldNames);

            return new RpcUserOperation
            {
                Sender = Address(fields, "sender"),
                Nonce = Quantity(fields, "nonce", MaxUint256),
                Factory = Factory(fields),
                FactoryData = Data(fields, "factoryData"),
                CallData = Data(fields, "callData"),
                CallGasLimit = Quantity(fields, "callGasLimit", MaxUint128),
                VerificationGasLimit = Quantity(fields, "verificationGasLimit", MaxUint128),
                PreVerificationGas = Quantity(fields, "preVerificationGas", MaxUint256),
                MaxFeePerGas = Quantity(fields, "maxFeePerGas", MaxUint128),
                MaxPriorityFeePerGas = Quantity(fields, "maxPriorityFeePerGas", MaxUint128),
                PaymasterVerificationGasLimit = Quantity(fields, "paymasterVerificationGasLimit", MaxUint128),
                PaymasterPostOpGasLimit = Quantity(fields, "paymasterPostOpGasLimit", MaxUint128),
                Signature = Data(fields, "signature"),
                Paymaster = Address(fields, "paymaster"),
                PaymasterData = Data(fields, "paymasterData"),
                Eip7702Auth = ParseAuthorisation(fields)
            };
        }

        private static void RequireSendFields(RpcUserOperation userOp)
        {
            Require(userOp.Sender, "sender");
            Require(userOp.Nonce, "nonce");
            Require(userOp.CallData, "callData");
            Require(userOp.CallGasLimit, "callGasLimit");
            Require(userOp.VerificationGasLimit, "verificationGasLimit");
            Require(userOp.PreVerificationGas, "preVerificationGas");
            Require(userOp.MaxFeePerGas, "maxFeePerGas");
            Require(userOp.MaxPriorityFeePerGas, "maxPriorityFeePerGas");
            Require(userOp.Signature, "signature");

            if (userOp.Paymaster != null)
            {
                Require(userOp.PaymasterVerificationGasLimit, "paymasterVerificationGasLimit");
                Require(userOp.PaymasterPostOpGasLimit, "paymasterPostOpGasLimit");
            }
        }

        private static void Require(object? value, string name)
        {
            if (value == null)
            {
                throw RpcException.InvalidParams($"{name} is required");
            }
        }

        public static string ParseEntryPoint(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                throw RpcException.InvalidParams("entryPoint must be a 0x-prefixed 20-byte hex address");
            }

            var entryPoint = element.GetRpcString();
            if (string.IsNullOrEmpty(entryPoint))
            {
                throw RpcException.InvalidParams("entryPoint address is required");
            }

            if (!IsAddress(entryPoint))
            {
                throw RpcException.InvalidParams("entryPoint must be a 0x-prefixed 20-byte hex address");
            }

            return entryPoint;
        }

        public static string ParseUserOpHash(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.String ||
                !MempoolHashKey.TryNormalizeStrict(element.GetRpcString(), out var normalized))
            {
                throw RpcException.InvalidParams("Missing/invalid userOpHash: must be a 0x-prefixed 32-byte hex string");
            }

            return normalized;
        }

        private static string PropertyName(JsonProperty property)
        {
            try
            {
                return property.Name;
            }
            catch (InvalidOperationException)
            {
                throw RpcException.InvalidParams("member name is not valid UTF-16");
            }
        }

        private static Dictionary<string, JsonElement> ReadFields(JsonElement element, string[] knownNames)
        {
            var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                var propertyName = PropertyName(property);
                var known = Array.Find(knownNames, name => string.Equals(name, propertyName, StringComparison.OrdinalIgnoreCase));
                if (known == null) continue;

                if (known != propertyName)
                {
                    throw RpcException.InvalidParams($"{propertyName} must be spelled {known}");
                }

                if (!seen.Add(propertyName))
                {
                    throw RpcException.InvalidParams($"{propertyName} appears more than once");
                }

                if (property.Value.ValueKind != JsonValueKind.Null && property.Value.ValueKind != JsonValueKind.Undefined)
                {
                    fields[propertyName] = property.Value;
                }
            }

            return fields;
        }

        private static Authorisation? ParseAuthorisation(Dictionary<string, JsonElement> fields)
        {
            if (!fields.TryGetValue("eip7702Auth", out var element)) return null;

            if (element.ValueKind != JsonValueKind.Object)
            {
                throw RpcException.InvalidParams("eip7702Auth must be a JSON object");
            }

            var auth = ReadFields(element, AuthorisationFieldNames);

            return new Authorisation
            {
                ChainId = Quantity(auth, "chainId", MaxUint256),
                Address = Address(auth, "address"),
                Nonce = Quantity(auth, "nonce", MaxUint64),
                YParity = QuantityText(auth, "yParity", BigInteger.One),
                R = QuantityText(auth, "r", MaxUint256),
                S = QuantityText(auth, "s", MaxUint256)
            };
        }

        private static string? Text(Dictionary<string, JsonElement> fields, string name)
        {
            if (!fields.TryGetValue(name, out var element)) return null;

            if (element.ValueKind != JsonValueKind.String)
            {
                throw RpcException.InvalidParams($"{name} must be a 0x-prefixed hex string");
            }

            return element.GetRpcString();
        }

        private static string? Address(Dictionary<string, JsonElement> fields, string name)
        {
            var text = Text(fields, name);
            if (text == null) return null;

            if (!IsAddress(text))
            {
                throw RpcException.InvalidParams($"{name} must be a 0x-prefixed 20-byte hex address");
            }

            return text;
        }

        private static string? Factory(Dictionary<string, JsonElement> fields)
        {
            var text = Text(fields, "factory");
            if (text == null) return null;

            if (text == Eip7702FactorySentinel)
            {
                if (!fields.ContainsKey("eip7702Auth"))
                {
                    throw RpcException.InvalidParams("factory 0x7702 requires eip7702Auth");
                }

                return text;
            }

            if (!IsAddress(text))
            {
                throw RpcException.InvalidParams("factory must be a 0x-prefixed 20-byte hex address or the 0x7702 sentinel");
            }

            return text;
        }

        private static string? Data(Dictionary<string, JsonElement> fields, string name)
        {
            var text = Text(fields, name);
            if (text == null) return null;

            if (!HasHexPrefix(text) || (text.Length - 2) % 2 != 0 || !AllHex(text, 2))
            {
                throw RpcException.InvalidParams($"{name} must be 0x-prefixed hex with an even number of digits");
            }

            return text;
        }

        private static HexBigInteger? Quantity(Dictionary<string, JsonElement> fields, string name, BigInteger max)
        {
            var text = Text(fields, name);
            if (text == null) return null;

            return new HexBigInteger(ParseQuantity(text, name, max));
        }

        private static string? QuantityText(Dictionary<string, JsonElement> fields, string name, BigInteger max)
        {
            var text = Text(fields, name);
            if (text == null) return null;

            ParseQuantity(text, name, max);
            return text;
        }

        private static BigInteger ParseQuantity(string text, string name, BigInteger max)
        {
            if (!HasHexPrefix(text) || text.Length == 2 || !AllHex(text, 2) ||
                !BigInteger.TryParse("0" + text.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value) ||
                value > max)
            {
                throw RpcException.InvalidParams($"{name} must be a 0x-prefixed hex quantity within range");
            }

            return value;
        }

        private static bool IsAddress(string text) =>
            HasHexPrefix(text) && text.Length == 2 + AddressHexLength && AllHex(text, 2);

        private static bool HasHexPrefix(string text) =>
            text.Length >= 2 && text[0] == '0' && text[1] == 'x';

        private static bool AllHex(string text, int start)
        {
            for (var i = start; i < text.Length; i++)
            {
                if (!Uri.IsHexDigit(text[i])) return false;
            }

            return true;
        }
    }
}
