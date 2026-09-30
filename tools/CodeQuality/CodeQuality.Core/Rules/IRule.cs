using CodeQuality.Core.Config;

namespace CodeQuality.Core.Rules;

public sealed record RuleContext(
    string Package,
    string RelativePath,
    string ProfileName,
    Profile Profile,
    CodeQualityConfig Config);
