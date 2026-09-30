using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Nethereum.EVM;
using Nethereum.EVM.Hardforks;
using Xunit;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class ForkRegistrationCompletenessTests
    {
        [Fact]
        [Trait("Category", "ForkRegistration")]
        public void Given_EveryRegisteredForkName_When_Parsed_Then_RoundTripsToTheSameFork()
        {
            Assert.True(HardforkSpecRegistry.All.Length > 0,
                "HardforkSpecRegistry.All is empty — no forks are registered to verify.");

            foreach (var spec in HardforkSpecRegistry.All)
            {
                Assert.Equal(spec.Name, HardforkNames.Parse(spec.Name.ToString()));
            }

            var lowerCasedName = HardforkSpecRegistry.All[0].Name.ToString().ToLowerInvariant();
            Assert.Equal(HardforkSpecRegistry.All[0].Name, HardforkNames.Parse(lowerCasedName));
        }

        [Fact]
        [Trait("Category", "ForkRegistration")]
        public void Given_EveryRegisteredSpec_When_RequiredFieldsInspected_Then_NoneAreNull()
        {
            var referenceTypedProperties = typeof(HardforkSpec)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => !property.PropertyType.IsValueType)
                .ToArray();

            Assert.True(referenceTypedProperties.Length > 0,
                "Reflection found zero reference-typed properties on HardforkSpec — the " +
                "reflection filter no longer matches the type's shape, so this test would " +
                "silently check nothing. If HardforkSpec's slots became fields (or all " +
                "properties became value types), update the filter above.");

            Assert.True(HardforkSpecRegistry.All.Length > 0,
                "HardforkSpecRegistry.All is empty — no forks are registered to verify.");

            var failures = new List<string>();
            foreach (var spec in HardforkSpecRegistry.All)
            {
                foreach (var property in referenceTypedProperties)
                {
                    if (property.GetValue(spec) == null)
                        failures.Add($"{spec.Name}.{property.Name} is null");
                }
            }

            Assert.True(failures.Count == 0,
                "HardforkSpec required reference-typed slot(s) are null despite being " +
                "compiler-required:\n  " + string.Join("\n  ", failures));
        }

        [Fact]
        [Trait("Category", "ForkRegistration")]
        public void Given_EveryRegisteredFork_When_ConfigAccessed_Then_AccessorExists()
        {
            var accessorsByName = typeof(HardforkConfig)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(property => property.PropertyType == typeof(HardforkConfig))
                .ToDictionary(property => property.Name);

            Assert.True(accessorsByName.Count > 0,
                "Reflection found zero static HardforkConfig-typed properties on HardforkConfig " +
                "— the reflection filter no longer matches the type's shape, so this test would " +
                "silently check nothing. If the per-fork accessors were renamed or restructured, " +
                "update the filter above.");

            Assert.True(HardforkSpecRegistry.All.Length > 0,
                "HardforkSpecRegistry.All is empty — no forks are registered to verify.");

            var failures = new List<string>();
            foreach (var spec in HardforkSpecRegistry.All)
            {
                var forkName = spec.Name.ToString();
                if (!accessorsByName.TryGetValue(forkName, out var accessor))
                {
                    failures.Add($"{forkName}: registered in HardforkSpecRegistry.All but " +
                        $"HardforkConfig has no static '{forkName}' accessor");
                    continue;
                }

                if (accessor.GetValue(null) == null)
                    failures.Add($"{forkName}: HardforkConfig.{forkName} accessor exists but returns null");
            }

            Assert.True(failures.Count == 0,
                "HardforkConfig accessors drifted from HardforkSpecRegistry.All:\n  " +
                string.Join("\n  ", failures));
        }

        [Fact]
        [Trait("Category", "ForkRegistration")]
        public void Given_EveryNonAliasHardforkName_When_RegistryInspected_Then_SpecIsRegistered()
        {
            var aliasForksWithoutSpecs = new HashSet<HardforkName>
            {
                HardforkName.FrontierThawing,
                HardforkName.DaoFork,
                HardforkName.MuirGlacier,
                HardforkName.ArrowGlacier,
                HardforkName.GrayGlacier,
            };

            var registeredForkNames = HardforkSpecRegistry.All
                .Select(spec => spec.Name)
                .ToHashSet();

            var failures = new List<string>();

            foreach (var promoted in aliasForksWithoutSpecs.Where(registeredForkNames.Contains))
            {
                failures.Add($"{promoted}: listed in aliasForksWithoutSpecs but now has a " +
                    $"registered spec in HardforkSpecRegistry.All — remove {promoted} from " +
                    "aliasForksWithoutSpecs, it is no longer alias-only");
            }

            foreach (HardforkName fork in Enum.GetValues(typeof(HardforkName)))
            {
                if (fork == HardforkName.Unspecified) continue;
                if (aliasForksWithoutSpecs.Contains(fork)) continue;

                if (!registeredForkNames.Contains(fork))
                    failures.Add($"{fork}: declared in HardforkName but has no matching " +
                        $"spec.Name in HardforkSpecRegistry.All — add {fork}Spec.Instance to " +
                        "HardforkSpecRegistry.All");
            }

            Assert.True(failures.Count == 0,
                "HardforkName enum value(s) have no registered HardforkSpec:\n  " +
                string.Join("\n  ", failures));
        }
    }
}
