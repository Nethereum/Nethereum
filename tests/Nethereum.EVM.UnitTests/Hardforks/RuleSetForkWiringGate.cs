using System;
using System.Collections.Generic;
using Nethereum.EVM.Hardforks;

namespace Nethereum.EVM.UnitTests.Hardforks
{
    internal static class RuleSetForkWiringGate
    {
        public static List<string> FindMismatches<TRule>(
            IEnumerable<HardforkSpec> specs,
            IReadOnlyDictionary<HardforkName, TRule> expectedByFork,
            Func<HardforkSpec, TRule> resolveWired,
            string ruleSetName)
            where TRule : class
        {
            var mismatches = new List<string>();

            foreach (var spec in specs)
            {
                if (!expectedByFork.TryGetValue(spec.Name, out var expected))
                {
                    mismatches.Add(
                        $"{spec.Name}: no expected {ruleSetName} rule declared in this test's " +
                        "expectation map — add an entry naming the rule instance this fork should wire.");
                    continue;
                }

                var wired = resolveWired(spec);
                if (!ReferenceEquals(expected, wired))
                {
                    mismatches.Add(
                        $"{spec.Name}: expected {ruleSetName} {Describe(expected)} but resolved {Describe(wired)}");
                }
            }

            return mismatches;
        }

        private static string Describe(object rule) => rule?.GetType().Name ?? "null";
    }
}
