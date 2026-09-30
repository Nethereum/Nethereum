using System.Numerics;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.UniActionPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Modules.SmartSession
{
    public class UniActionPolicyBuilderEncodingTests
    {
        private class ActionConfigDecodeDto
        {
            [Parameter("uint256", "valueLimitPerUse", 1)]
            public virtual BigInteger ValueLimitPerUse { get; set; }

            [Parameter("tuple", "paramRules", 2)]
            public virtual ParamRules ParamRules { get; set; }
        }

        [Theory]
        [InlineData(ParamCondition.Equal, 0)]
        [InlineData(ParamCondition.GreaterThan, 1)]
        [InlineData(ParamCondition.LessThan, 2)]
        [InlineData(ParamCondition.GreaterThanOrEqual, 3)]
        [InlineData(ParamCondition.LessThanOrEqual, 4)]
        [InlineData(ParamCondition.NotEqual, 5)]
        [InlineData(ParamCondition.InRange, 6)]
        public void ParamCondition_MatchesUniActionPolicySolidityEnum(ParamCondition condition, byte expectedSolidityValue)
        {
            Assert.Equal(expectedSolidityValue, (byte)condition);
        }

        [Fact]
        public void WithMaxValue_EncodesSolidityLessThanOrEqualConditionByte()
        {
            var encoded = new UniActionPolicyBuilder()
                .WithMaxValue(0, new byte[] { 0x05 })
                .Build();

            var decoded = (ActionConfigDecodeDto)new ParameterDecoder()
                .DecodeAttributes(encoded, typeof(ActionConfigDecodeDto));

            Assert.Equal((byte)4, decoded.ParamRules.Rules[0].Condition);
        }

        [Fact]
        public void Build_EncodesFlatStaticActionConfigTuple()
        {
            var maxValue = new byte[] { 0x05 };
            var encoded = new UniActionPolicyBuilder()
                .WithValueLimit(1000)
                .WithMaxValue(0, maxValue)
                .Build();

            Assert.Equal(3136, encoded.Length);

            var decoded = (ActionConfigDecodeDto)new ParameterDecoder()
                .DecodeAttributes(encoded, typeof(ActionConfigDecodeDto));

            Assert.Equal(new BigInteger(1000), decoded.ValueLimitPerUse);
            Assert.Equal(new BigInteger(1), decoded.ParamRules.Length);
            Assert.Equal(16, decoded.ParamRules.Rules.Count);

            var configuredRule = decoded.ParamRules.Rules[0];
            Assert.Equal((byte)ParamCondition.LessThanOrEqual, configuredRule.Condition);
            Assert.Equal(0UL, configuredRule.Offset);
            Assert.False(configuredRule.IsLimited);

            var expectedRef = new byte[32];
            expectedRef[31] = 0x05;
            Assert.Equal(expectedRef, configuredRule.Ref);

            for (var i = 1; i < 16; i++)
            {
                Assert.Equal((byte)ParamCondition.Equal, decoded.ParamRules.Rules[i].Condition);
            }
        }

        [Fact]
        public void EmptyPolicy_EncodesFlatStaticActionConfigTupleWithNoRules()
        {
            var encoded = UniActionPolicyBuilder.EmptyPolicy(BigInteger.Zero);

            Assert.Equal(3136, encoded.Length);

            var decoded = (ActionConfigDecodeDto)new ParameterDecoder()
                .DecodeAttributes(encoded, typeof(ActionConfigDecodeDto));

            Assert.Equal(BigInteger.Zero, decoded.ValueLimitPerUse);
            Assert.Equal(BigInteger.Zero, decoded.ParamRules.Length);
            Assert.Equal(16, decoded.ParamRules.Rules.Count);
        }
    }
}
