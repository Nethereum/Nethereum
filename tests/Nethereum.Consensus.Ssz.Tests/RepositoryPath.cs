using System;
using System.IO;

namespace Nethereum.Consensus.Ssz.Tests
{
    internal static class RepositoryPath
    {
        private const string VectorsRelativePath = "tests/LightClientVectors";

        private static readonly Lazy<string> _root = new Lazy<string>(ResolveTheDirectoryHoldingTheVectors);

        public static string Root => _root.Value;

        private static string ResolveTheDirectoryHoldingTheVectors()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "tests", "LightClientVectors")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                $"No ancestor of {AppContext.BaseDirectory} contains {VectorsRelativePath}, so the " +
                "consensus-spec vectors cannot be located. Every vector-driven theory would otherwise " +
                "report \"No data found\", which says nothing about why.");
        }
    }
}
