using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Nethereum.EVM.Execution.TransactionValidation;
using Nethereum.EVM.Execution.TransactionValidation.Rules;
using Nethereum.EVM.Gas;
using Xunit;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    public class TransactionValidationRuleCompositionTests
    {
        private const string Expected = @"
Frontier: SupportedTransactionTypeRule.LegacyOnly, ChainIdValidationRule.Instance
Homestead: SupportedTransactionTypeRule.LegacyOnly, ChainIdValidationRule.Instance
Byzantium: SupportedTransactionTypeRule.LegacyOnly, ChainIdValidationRule.Instance
Constantinople: SupportedTransactionTypeRule.LegacyOnly, ChainIdValidationRule.Instance
Istanbul: SupportedTransactionTypeRule.LegacyOnly, ChainIdValidationRule.Instance
Berlin: SupportedTransactionTypeRule.UpToAccessList, ChainIdValidationRule.Instance
London: SupportedTransactionTypeRule.UpToFeeMarket, ChainIdValidationRule.Instance
Shanghai: SupportedTransactionTypeRule.UpToFeeMarket, ChainIdValidationRule.Instance
Cancun: SupportedTransactionTypeRule.UpToBlob, Eip4844BlobValidationRule.Instance, ChainIdValidationRule.Instance
Prague: SupportedTransactionTypeRule.UpToSetCode, Eip4844BlobValidationRule.Instance, Eip7702AuthListValidationRule.Instance, ChainIdValidationRule.Instance
Osaka: SupportedTransactionTypeRule.UpToSetCode, Eip4844BlobValidationRule.Instance, Eip7594MaxBlobsPerTxRule.Instance, Eip7702AuthListValidationRule.Instance, Eip7825RawTxGasCapRule.Instance, ChainIdValidationRule.Instance
Amsterdam: SupportedTransactionTypeRule.UpToSetCode, Eip4844BlobValidationRule.Instance, Eip7594MaxBlobsPerTxRule.Instance, Eip2780AuthListValidationRule.Instance, Eip8037IntrinsicGasCapRule.Instance, ChainIdValidationRule.Instance
";

        [Fact]
        public void Every_fork_states_its_validation_rules_explicitly()
        {
            var actual = RenderComposition();

            Assert.Equal(Expected.Trim().Replace("\r\n", "\n"), actual.Trim().Replace("\r\n", "\n"));
        }

        [Fact]
        public void Amsterdam_does_not_share_the_authorization_rule_with_Prague_or_Osaka()
        {
            var prague = RulesOf(nameof(TransactionValidationRuleSets.Prague));
            var osaka = RulesOf(nameof(TransactionValidationRuleSets.Osaka));
            var amsterdam = RulesOf(nameof(TransactionValidationRuleSets.Amsterdam));

            var pragueAuth = prague.Single(r => r.GetType().Name.Contains("AuthList"));
            var osakaAuth = osaka.Single(r => r.GetType().Name.Contains("AuthList"));
            var amsterdamAuth = amsterdam.Single(r => r.GetType().Name.Contains("AuthList"));

            Assert.Same(pragueAuth, osakaAuth);
            Assert.NotSame(pragueAuth, amsterdamAuth);
        }

        [Fact]
        public void Amsterdam_does_not_share_the_gas_cap_rule_with_Osaka()
        {
            var osakaCap = RulesOf(nameof(TransactionValidationRuleSets.Osaka))
                .Single(r => r.GetType().Name.Contains("GasCap"));
            var amsterdamCap = RulesOf(nameof(TransactionValidationRuleSets.Amsterdam))
                .Single(r => r.GetType().Name.Contains("GasCap"));

            Assert.NotSame(osakaCap, amsterdamCap);
        }

        [Fact]
        public void Amsterdam_authorization_rule_charges_7816_per_tuple_not_a_flat_25000()
        {
            var amsterdamAuth = RulesOf(nameof(TransactionValidationRuleSets.Amsterdam))
                .Single(r => r.GetType().Name.Contains("AuthList"));

            var costField = amsterdamAuth.GetType()
                .GetField("EXECUTION_PER_AUTH_BASE_COST", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(costField);
            Assert.Equal(7_816L, (long)costField.GetValue(null));
        }

        [Fact]
        public void Amsterdam_intrinsic_gas_cap_is_2_pow_24_not_Osakas_raw_tx_gas_form()
        {
            Assert.Equal(16_777_216L, GasConstants.EIP8037_TX_MAX_GAS_LIMIT);

            var amsterdamCap = RulesOf(nameof(TransactionValidationRuleSets.Amsterdam))
                .Single(r => r.GetType().Name.Contains("GasCap"));
            Assert.IsType<Eip8037IntrinsicGasCapRule>(amsterdamCap);
        }

        private static string RenderComposition()
        {
            var sb = new StringBuilder();
            foreach (var field in ForkFields())
            {
                var rules = RulesOf(field.Name);
                var labels = rules.Select(Label).ToList();
                sb.AppendLine($"{field.Name}: {(labels.Count == 0 ? "(none)" : string.Join(", ", labels))}");
            }
            return sb.ToString();
        }

        private static IEnumerable<FieldInfo> ForkFields()
        {
            var fields = typeof(TransactionValidationRuleSets)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(TransactionValidationRules))
                .ToList();

            Assert.True(fields.Count > 0, "no fork rule-set fields discovered - the reflection filter no longer matches");
            return fields;
        }

        private static IReadOnlyList<ITransactionValidationRule> RulesOf(string forkFieldName)
        {
            var field = typeof(TransactionValidationRuleSets)
                .GetField(forkFieldName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(field);

            var ruleSet = field.GetValue(null);
            var rulesField = typeof(TransactionValidationRules)
                .GetField("_rules", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(rulesField);

            return (ITransactionValidationRule[])rulesField.GetValue(ruleSet);
        }

        private static string Label(ITransactionValidationRule rule)
        {
            var type = rule.GetType();
            var named = type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => type.IsAssignableFrom(f.FieldType))
                .FirstOrDefault(f => ReferenceEquals(f.GetValue(null), rule));

            return named == null ? $"{type.Name}.<unnamed>" : $"{type.Name}.{named.Name}";
        }
    }
}
