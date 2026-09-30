using System.Collections.Generic;
using System.Linq;
using Nethereum.Documentation;

namespace Nethereum.Freezer
{
    public sealed class GethFreezerImporter
    {
        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "GethFreezerImporter.InspectSource — import-readiness gate")]
        public ImportReadiness InspectSource(FreezerLayout source)
        {
            using (var freezer = Freezer.Open(source, FreezerOpenMode.ReadOnly))
            {
                return BuildReadiness(freezer.Tails, freezer.Items);
            }
        }

        [NethereumDocExample(DocSection.ChainInfrastructure, "chain-infrastructure", "GethFreezerImporter.BuildReadiness — classify pruned tail groups")]
        public static ImportReadiness BuildReadiness(IReadOnlyDictionary<string, TableTail> tails, long items)
        {
            var prunedGroups = tails
                .Where(pair => pair.Value.VirtualTail > 0)
                .Select(pair => new PrunedGroup(pair.Key, pair.Value.VirtualTail, pair.Value.Members))
                .ToList();
            return new ImportReadiness(prunedGroups, items);
        }
    }

    public sealed class ImportReadiness
    {
        public bool IsCompleteHistory { get; }
        public IReadOnlyList<PrunedGroup> PrunedGroups { get; }
        public long Items { get; }

        public ImportReadiness(IReadOnlyList<PrunedGroup> prunedGroups, long items)
        {
            PrunedGroups = prunedGroups;
            IsCompleteHistory = prunedGroups.Count == 0;
            Items = items;
        }
    }

    public sealed class PrunedGroup
    {
        public string GroupLabel { get; }
        public long VirtualTail { get; }
        public IReadOnlyList<string> Members { get; }

        public PrunedGroup(string groupLabel, long virtualTail, IReadOnlyList<string> members)
        {
            GroupLabel = groupLabel;
            VirtualTail = virtualTail;
            Members = members;
        }
    }
}
