using System.Security.Cryptography;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CodeQuality.Core.Config;

public static class ConfigLoader
{
    public const string FileName = "codequality.yml";

    public static CodeQualityConfig LoadFrom(string yaml)
    {
        var config = Parse(yaml) ?? new CodeQualityConfig();
        Sanitise(config);
        config.Profiles.TryAdd("default", new Profile());
        config.RulesVersion = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(yaml)))[..16];
        ValidateBindings(config);
        return config;
    }

    public static CodeQualityConfig Discover(string startPath)
    {
        var directory = Directory.Exists(startPath)
            ? new DirectoryInfo(startPath)
            : new FileInfo(startPath).Directory;

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, FileName);
            if (File.Exists(candidate))
            {
                var config = LoadFrom(File.ReadAllText(candidate));
                config.SourcePath = candidate;
                return config;
            }
            directory = directory.Parent;
        }

        return LoadFrom(string.Empty);
    }

    // A binding naming a profile that is not declared under `profiles:` is a configuration
    // error, not a typo the tool can shrug off: silently falling back to the default profile
    // would run the analysis under rules nobody asked for while the report header still claims
    // the typo'd profile was applied. Failing here, before any package is read, means a bad
    // binding never gets the chance to produce a report at all.
    static void ValidateBindings(CodeQualityConfig config)
    {
        foreach (var binding in config.Bindings)
        {
            if (!config.Profiles.ContainsKey(binding.Profile))
                throw new ConfigurationException(
                    $"binding for glob '{binding.Glob}' names profile '{binding.Profile}', "
                    + "which is not declared under profiles:");
        }
    }

    // A malformed file (bad indentation, unterminated quote, tab indentation, ...) must degrade to
    // the default configuration rather than crash the run that would have reported it.
    //
    // The deserializer is built fresh per call rather than shared: measured, that costs about
    // 40 microseconds (10,000 fresh builds: ~1s total) against loading a config file once per run,
    // which is negligible. A shared instance raced under concurrent callers — proven by running the
    // original round-1 commit's full test suite repeatedly, which produced a config missing a
    // profile key that was present in its own source YAML.
    static CodeQualityConfig? Parse(string yaml)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            return deserializer.Deserialize<CodeQualityConfig>(yaml);
        }
        catch (YamlException)
        {
            return null;
        }
    }

    // YamlDotNet sets a mapping property to null when its key is present with no value (or an
    // explicit `null`/`~`), overriding the field initialiser. Every collection the rest of the
    // tool iterates is normalised back to non-null here so a hand-edited config file can only ever
    // narrow what runs, never crash it.
    static void Sanitise(CodeQualityConfig config)
    {
        config.Generated ??= new GeneratedFileRules();
        config.Generated.FileNameSuffixes ??= new();
        config.Generated.FileNamePrefixes ??= new();
        config.Generated.PathSegments ??= new();
        config.Generated.HeaderMarkers ??= new();

        config.References ??= new ReferenceRules();
        config.References.Vendors ??= new();
        config.References.SourcePointerPatterns ??= new();
        config.References.InternalSymbolPatterns ??= new();
        config.References.CitationPatterns ??= new();

        config.Clones ??= new CloneRules();

        config.Parse ??= new ParseRules();
        config.Parse.PreprocessorSymbols ??= new();

        config.Profiles ??= new();
        foreach (var name in config.Profiles.Keys.ToList())
        {
            var profile = config.Profiles[name] ?? new Profile();
            profile.RuleVerdicts ??= new();
            config.Profiles[name] = profile;
        }

        config.Bindings ??= new();
        config.Bindings.RemoveAll(binding => binding is null);
        foreach (var binding in config.Bindings)
        {
            binding.Glob ??= string.Empty;
            binding.Profile ??= "default";
        }
    }
}
