using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.UniActionPolicy.ContractDefinition;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession
{
    public enum ParamCondition : byte
    {
        Equal = 0,
        GreaterThan = 1,
        LessThan = 2,
        GreaterThanOrEqual = 3,
        LessThanOrEqual = 4,
        NotEqual = 5,
        InRange = 6
    }

    public class UniActionPolicyBuilder
    {
        private BigInteger _valueLimitPerUse = BigInteger.Zero;
        private readonly List<ParamRule> _rules = new List<ParamRule>();

        public UniActionPolicyBuilder WithValueLimit(BigInteger limit)
        {
            _valueLimitPerUse = limit;
            return this;
        }

        public UniActionPolicyBuilder WithParamRule(
            ParamCondition condition,
            ulong offset,
            byte[] refValue,
            bool isLimited = false,
            BigInteger? usageLimit = null)
        {
            if (_rules.Count >= 16)
                throw new InvalidOperationException("Maximum of 16 parameter rules allowed");

            _rules.Add(new ParamRule
            {
                Condition = (byte)condition,
                Offset = offset,
                IsLimited = isLimited,
                Ref = refValue ?? new byte[32],
                Usage = new LimitUsage
                {
                    Limit = usageLimit ?? BigInteger.Zero,
                    Used = BigInteger.Zero
                }
            });
            return this;
        }

        public UniActionPolicyBuilder WithEqualityCheck(ulong offset, byte[] expectedValue)
        {
            return WithParamRule(ParamCondition.Equal, offset, PadTo32Bytes(expectedValue));
        }

        public UniActionPolicyBuilder WithMaxValue(ulong offset, byte[] maxValue)
        {
            return WithParamRule(ParamCondition.LessThanOrEqual, offset, PadTo32Bytes(maxValue));
        }

        public UniActionPolicyBuilder WithMinValue(ulong offset, byte[] minValue)
        {
            return WithParamRule(ParamCondition.GreaterThanOrEqual, offset, PadTo32Bytes(minValue));
        }

        public UniActionPolicyBuilder WithLimitedUsage(ulong offset, byte[] refValue, BigInteger limit)
        {
            return WithParamRule(ParamCondition.Equal, offset, PadTo32Bytes(refValue), true, limit);
        }

        public byte[] Build()
        {
            var paddedRules = new List<ParamRule>();
            paddedRules.AddRange(_rules);

            while (paddedRules.Count < 16)
            {
                paddedRules.Add(CreateEmptyRule());
            }

            var paramRules = new ParamRules
            {
                Length = _rules.Count,
                Rules = paddedRules
            };

            var abiEncoder = new ABIEncode();
            return abiEncoder.GetABIParamsEncoded(new ActionConfigDto
            {
                ValueLimitPerUse = _valueLimitPerUse,
                ParamRules = paramRules
            });
        }

        private class ActionConfigDto
        {
            [Parameter("uint256", "valueLimitPerUse", 1)]
            public virtual BigInteger ValueLimitPerUse { get; set; }

            [Parameter("tuple", "paramRules", 2)]
            public virtual ParamRules ParamRules { get; set; }
        }

        public static byte[] EmptyPolicy(BigInteger valueLimitPerUse = default)
        {
            return new UniActionPolicyBuilder()
                .WithValueLimit(valueLimitPerUse)
                .Build();
        }

        private static ParamRule CreateEmptyRule()
        {
            return new ParamRule
            {
                Condition = (byte)ParamCondition.Equal,
                Offset = 0,
                IsLimited = false,
                Ref = new byte[32],
                Usage = new LimitUsage
                {
                    Limit = BigInteger.Zero,
                    Used = BigInteger.Zero
                }
            };
        }

        private static byte[] PadTo32Bytes(byte[] input)
        {
            if (input == null || input.Length == 0)
                return new byte[32];

            if (input.Length >= 32)
            {
                var result = new byte[32];
                Array.Copy(input, input.Length - 32, result, 0, 32);
                return result;
            }

            var padded = new byte[32];
            Array.Copy(input, 0, padded, 32 - input.Length, input.Length);
            return padded;
        }
    }
}
