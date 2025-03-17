using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Xunit;
using Nethereum.ABI.EIP712;
using Newtonsoft.Json;
using System.Numerics;

namespace Nethereum.Signer.UnitTests
{
    public class EIP712TypeDataSignatureMultipleComplexInnerObjects
    {

        [Struct("SessionSpec")]
        public class SessionSpec
        {
            [Parameter("address", "signer", 1)]
            [JsonProperty("signer")]
            public virtual string Signer { get; set; }

            [Parameter("uint256", "expiresAt", 2)]
            [JsonProperty("expiresAt")]
            public virtual BigInteger ExpiresAt { get; set; }

            [Parameter("tuple[]", "callPolicies", 3, structTypeName: "CallSpec[]")]
            [JsonProperty("callPolicies")]
            public virtual List<CallSpec> CallPolicies { get; set; }

            [Parameter("tuple[]", "transferPolicies", 4, structTypeName: "TransferSpec[]")]
            [JsonProperty("transferPolicies")]
            public virtual List<TransferSpec> TransferPolicies { get; set; }

            [Parameter("bytes32", "uid", 5)]
            [JsonProperty("uid")]
            public virtual byte[] Uid { get; set; }
        }

        [Struct("CallSpec")]
        public class CallSpec
        {
            [Parameter("address", "target", 1)]
            [JsonProperty("target")]
            public virtual string Target { get; set; }

            [Parameter("bytes4", "selector", 2)]
            [JsonProperty("selector")]
            public virtual byte[] Selector { get; set; }

            [Parameter("uint256", "maxValuePerUse", 3)]
            [JsonProperty("maxValuePerUse")]
            public virtual BigInteger MaxValuePerUse { get; set; }

            [Parameter("tuple", "valueLimit", 4, structTypeName: "UsageLimit")]
            [JsonProperty("valueLimit")]
            public virtual UsageLimit ValueLimit { get; set; }

            [Parameter("tuple[]", "constraints", 5, structTypeName: "Constraint[]")]
            [JsonProperty("constraints")]
            public virtual List<Constraint> Constraints { get; set; }
        }

        [Struct("TransferSpec")]
        public class TransferSpec
        {
            [Parameter("address", "target", 1)]
            [JsonProperty("target")]
            public virtual string Target { get; set; }

            [Parameter("uint256", "maxValuePerUse", 2)]
            [JsonProperty("maxValuePerUse")]
            public virtual BigInteger MaxValuePerUse { get; set; }

            [Parameter("tuple", "valueLimit", 3, structTypeName: "UsageLimit")]
            [JsonProperty("valueLimit")]
            public virtual UsageLimit ValueLimit { get; set; }
        }

        [Struct("UsageLimit")]
        public class UsageLimit
        {
            [Parameter("uint8", "limitType", 1)]
            [JsonProperty("limitType")]
            public virtual byte LimitType { get; set; }

            [Parameter("uint256", "limit", 2)]
            [JsonProperty("limit")]
            public virtual BigInteger Limit { get; set; }

            [Parameter("uint256", "period", 3)]
            [JsonProperty("period")]
            public virtual BigInteger Period { get; set; }
        }

        [Struct("Constraint")]
        public class Constraint
        {
            [Parameter("uint8", "condition", 1)]
            [JsonProperty("condition")]
            public virtual byte Condition { get; set; }

            [Parameter("uint64", "index", 2)]
            [JsonProperty("index")]
            public virtual ulong Index { get; set; }

            [Parameter("bytes32", "refValue", 3)]
            [JsonProperty("refValue")]
            public virtual byte[] RefValue { get; set; }

            [Parameter("tuple", "limit", 4, structTypeName: "UsageLimit")]
            [JsonProperty("limit")]
            public virtual UsageLimit Limit { get; set; }
        }

        [Fact]
        public void ShouldAppendObjectReferencesOrderedByNameWithNoDuplicatesInComplexObjectsThatContainsTheSameReference()
        {
            var expected = "SessionSpec(address signer,uint256 expiresAt,CallSpec[] callPolicies,TransferSpec[] transferPolicies,bytes32 uid)CallSpec(address target,bytes4 selector,uint256 maxValuePerUse,UsageLimit valueLimit,Constraint[] constraints)Constraint(uint8 condition,uint64 index,bytes32 refValue,UsageLimit limit)TransferSpec(address target,uint256 maxValuePerUse,UsageLimit valueLimit)UsageLimit(uint8 limitType,uint256 limit,uint256 period)";
            var result = Eip712TypedDataEncoder.Current.GetEncodedType("SessionSpec",
            typeof(Domain),
            typeof(SessionSpec),
            typeof(CallSpec),
            typeof(TransferSpec),
            typeof(UsageLimit),
            typeof(Constraint));

            Assert.Equal(expected, result);

        }
    }
}