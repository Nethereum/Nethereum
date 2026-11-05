using System;
using System.Collections.Generic;
using Nethereum.CoreChain.Storage;
using Nethereum.Merkle.Patricia.Nodes;

namespace Nethereum.DevP2P.Sync.Snap.Bootstrap
{
    internal sealed class HealDamage
    {
        public List<byte[]> AccountPaths { get; } = new();
        public List<(byte[] Owner, byte[] Path)> StorageNodes { get; } = new();
        public List<byte[]> OwnersToWipe { get; } = new();

        public static HealDamage From(FlatReconcileUnresolvableNodeException hole)
        {
            var damage = new HealDamage();
            var ownersWithPaths = new HashSet<byte[]>(Nethereum.Util.ByteArrayComparer.Current);
            foreach (var (owner, path) in hole.MissingNodes)
            {
                if (owner.Length == 0)
                {
                    damage.AccountPaths.Add(path);
                    continue;
                }
                ownersWithPaths.Add(owner);
                damage.StorageNodes.Add((owner, path));
            }
            foreach (var (owner, _) in hole.Subtrees)
                if (!ownersWithPaths.Contains(owner)) damage.OwnersToWipe.Add(owner);
            return damage;
        }

        public void DeleteAncestorChains(IHealNodeSink sink)
        {
            foreach (var path in AccountPaths)
                DeleteProperPrefixes(sink, isStorage: false, owner: null, path);
            foreach (var (owner, path) in StorageNodes)
            {
                DeleteAccountChain(sink, owner);
                DeleteProperPrefixes(sink, isStorage: true, owner, path);
            }
            foreach (var owner in OwnersToWipe)
            {
                DeleteAccountChain(sink, owner);
                sink.ForceWipeStorage(owner);
            }
            sink.Flush();
        }

        private static void DeleteAccountChain(IHealNodeSink sink, byte[] owner)
            => DeleteProperPrefixes(sink, isStorage: false, owner: null, owner.ConvertToNibbles());

        private static void DeleteProperPrefixes(IHealNodeSink sink, bool isStorage, byte[]? owner, byte[] path)
        {
            for (int length = 0; length < path.Length; length++)
                sink.DeleteNode(isStorage, owner!, path.AsSpan(0, length).ToArray());
        }
    }
}
