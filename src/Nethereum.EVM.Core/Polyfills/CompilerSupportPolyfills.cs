// Compiler-required types for C# 9+ `init` accessors and C# 11 `required`
// members. These ship in System.Runtime starting with .NET 5 (IsExternalInit)
// and .NET 7 (the required-member trio). On older targets — net451, net461,
// netstandard2.0 — they don't exist, which makes records with `init` setters
// and types with `required` properties uncompilable.
//
// Nethereum.EVM.Core itself only targets net8+, so these polyfills are dead
// code there. The Nethereum.EVM project links every .cs from this directory
// and multi-targets down to net451, which is where the polyfills become live.

#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
#endif

#if !NET7_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct |
        AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = false,
        Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName)
        {
            FeatureName = featureName;
        }

        public string FeatureName { get; }
        public bool IsOptional { get; init; }

        public const string RefStructs = nameof(RefStructs);
        public const string RequiredMembers = nameof(RequiredMembers);
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(
        AttributeTargets.Constructor,
        AllowMultiple = false,
        Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute { }
}
#endif
