#nullable enable
using System;
using System.IO;

namespace Nethereum.Consensus.Ssz.Tests
{
    internal static class RepositoryPath
    {
        private static readonly Lazy<string?> _root = new Lazy<string?>(ResolveRepositoryRoot);

        public static string? Root => _root.Value;

        private static string? ResolveRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var marker = Path.Combine(directory.FullName, "LIGHTCLIENT_ROADMAP.md");
                if (File.Exists(marker))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return null;
        }
    }
}
