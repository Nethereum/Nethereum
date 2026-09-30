using System.Reflection;

namespace Nethereum.CoreChain
{
    public static class NodeVersion
    {
        private static readonly string InformationalVersion =
            typeof(NodeVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";

        public static string Version { get; } = ReleaseVersion(InformationalVersion);

        public static string CommitPrefix { get; } = FirstFourCommitBytes(InformationalVersion);

        public static string ClientVersion => "Nethereum/v" + Version + "/dotnet";

        private static string ReleaseVersion(string informational)
        {
            var plusIndex = informational.IndexOf('+');
            var version = plusIndex >= 0 ? informational.Substring(0, plusIndex) : informational;
            return string.IsNullOrEmpty(version) ? "0.0.0" : version;
        }

        private static string FirstFourCommitBytes(string informational)
        {
            var plusIndex = informational.IndexOf('+');
            if (plusIndex < 0) return null;
            var commit = informational.Substring(plusIndex + 1);
            return commit.Length >= 8 ? "0x" + commit.Substring(0, 8).ToLowerInvariant() : null;
        }
    }
}
