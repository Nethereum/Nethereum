using System;
using System.Collections.Generic;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession
{
    public class ActionDataBuilder
    {
        private string _targetAddress;
        private byte[] _selector;
        private readonly List<PolicyData> _policies = new List<PolicyData>();

        public ActionDataBuilder WithTarget(string targetAddress)
        {
            _targetAddress = targetAddress;
            return this;
        }

        public ActionDataBuilder WithSelector(byte[] selector)
        {
            if (selector.Length != 4)
                throw new ArgumentException("Function selector must be 4 bytes", nameof(selector));
            _selector = selector;
            return this;
        }

        public ActionDataBuilder WithSelector(string selectorHex)
        {
            return WithSelector(selectorHex.HexToByteArray());
        }

        public ActionDataBuilder WithPolicy(string policyAddress, byte[] initData = null)
        {
            _policies.Add(new PolicyData
            {
                Policy = policyAddress,
                InitData = initData ?? Array.Empty<byte>()
            });
            return this;
        }

        public ActionDataBuilder WithSudoPolicy(string sudoPolicyAddress)
        {
            return WithPolicy(sudoPolicyAddress, Array.Empty<byte>());
        }

        public ActionDataBuilder WithSpendingLimitPolicy(
            string spendingLimitPolicyAddress,
            string tokenAddress,
            System.Numerics.BigInteger limit)
        {
            var initData = ERC20SpendingLimitBuilder.SingleToken(tokenAddress, limit);
            return WithPolicy(spendingLimitPolicyAddress, initData);
        }

        public ActionDataBuilder WithUniActionPolicy(
            string uniActionPolicyAddress,
            byte[] policyInitData)
        {
            return WithPolicy(uniActionPolicyAddress, policyInitData);
        }

        public ActionData Build()
        {
            if (string.IsNullOrEmpty(_targetAddress))
                throw new InvalidOperationException("Target address is required");

            if (_selector == null || _selector.Length != 4)
                throw new InvalidOperationException("Function selector must be 4 bytes");

            if (_policies.Count == 0)
                throw new InvalidOperationException("At least one policy is required");

            return new ActionData
            {
                ActionTarget = _targetAddress,
                ActionTargetSelector = _selector,
                ActionPolicies = new List<PolicyData>(_policies)
            };
        }

        public static ActionData ERC20Transfer(
            string tokenAddress,
            string spendingLimitPolicyAddress,
            System.Numerics.BigInteger spendingLimit)
        {
            var initData = ERC20SpendingLimitBuilder.SingleToken(tokenAddress, spendingLimit);
            return new ActionDataBuilder()
                .WithTarget(tokenAddress)
                .WithSelector("0xa9059cbb")
                .WithPolicy(spendingLimitPolicyAddress, initData)
                .Build();
        }

        public static ActionData ERC20Approve(
            string tokenAddress,
            string spendingLimitPolicyAddress,
            System.Numerics.BigInteger approvalLimit)
        {
            var initData = ERC20SpendingLimitBuilder.SingleToken(tokenAddress, approvalLimit);
            return new ActionDataBuilder()
                .WithTarget(tokenAddress)
                .WithSelector("0x095ea7b3")
                .WithPolicy(spendingLimitPolicyAddress, initData)
                .Build();
        }

        public static ActionData UnrestrictedAction(
            string targetAddress,
            string functionSelector,
            string sudoPolicyAddress)
        {
            return new ActionDataBuilder()
                .WithTarget(targetAddress)
                .WithSelector(functionSelector)
                .WithSudoPolicy(sudoPolicyAddress)
                .Build();
        }
    }
}
