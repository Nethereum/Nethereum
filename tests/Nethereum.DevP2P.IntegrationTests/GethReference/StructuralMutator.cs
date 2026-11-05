using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Nethereum.DevP2P.IntegrationTests.GethReference
{
    public enum MutationOp
    {
        LeadingZero,
        WidthPlus,
        WidthMinus,
        StringToList,
        ListToString,
        DropItem,
        DuplicateItem,
        AppendEmpty,
        AppendCopy,
        Boundary,
        LongForm,
        LongFormLeadingZero,
        Truncate,
        Trailing
    }

    public readonly struct MutationSite
    {
        public MutationSite(MutationOp op, int node, int parameter)
        {
            Op = op;
            Node = node;
            Parameter = parameter;
        }

        public MutationOp Op { get; }
        public int Node { get; }
        public int Parameter { get; }

        public override string ToString() => Op + "@" + Node + "/" + Parameter;
    }

    public static class StructuralMutator
    {
        private static readonly byte[][] BoundaryValues = BuildBoundaryValues();

        private static readonly byte[][] TrailingBytes =
        {
            new byte[] { 0x00 }, new byte[] { 0x80 }, new byte[] { 0xc0 }, new byte[] { 0x01, 0x02 }, new byte[] { 0xff, 0xff, 0xff }
        };

        private static byte[][] BuildBoundaryValues()
        {
            var values = new List<byte[]>();
            foreach (var exponent in new[] { 32, 63, 64, 256 })
            {
                var power = BigInteger.Pow(2, exponent);
                foreach (var delta in new[] { -1, 0, 1 })
                    values.Add((power + delta).ToByteArray(isUnsigned: true, isBigEndian: true));
            }
            return values.ToArray();
        }

        public static int BoundaryValueCount => BoundaryValues.Length;

        public static IEnumerable<MutationSite> Sites(RlpNode root)
        {
            var nodes = root.Descendants().ToList();
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node.IsTyped)
                {
                    yield return new MutationSite(MutationOp.LongForm, i, 0);
                    continue;
                }

                if (!node.IsList)
                {
                    yield return new MutationSite(MutationOp.LeadingZero, i, 0);
                    yield return new MutationSite(MutationOp.WidthPlus, i, 0);
                    if (node.Data.Length > 0) yield return new MutationSite(MutationOp.WidthMinus, i, 0);
                    yield return new MutationSite(MutationOp.StringToList, i, 0);
                    if (node.Data.Length <= 33)
                        for (var b = 0; b < BoundaryValues.Length; b++)
                            yield return new MutationSite(MutationOp.Boundary, i, b);
                    yield return new MutationSite(MutationOp.LongForm, i, 0);
                    yield return new MutationSite(MutationOp.LongFormLeadingZero, i, 0);
                }
                else
                {
                    yield return new MutationSite(MutationOp.ListToString, i, 0);
                    for (var item = 0; item < node.Items.Count; item++)
                    {
                        yield return new MutationSite(MutationOp.DropItem, i, item);
                        yield return new MutationSite(MutationOp.DuplicateItem, i, item);
                    }
                    yield return new MutationSite(MutationOp.AppendEmpty, i, 0);
                    if (node.Items.Count > 0) yield return new MutationSite(MutationOp.AppendCopy, i, 0);
                    yield return new MutationSite(MutationOp.LongForm, i, 0);
                    yield return new MutationSite(MutationOp.LongFormLeadingZero, i, 0);
                }
                yield return new MutationSite(MutationOp.Truncate, i, 0);
            }
            for (var t = 0; t < TrailingBytes.Length; t++)
                yield return new MutationSite(MutationOp.Trailing, 0, t);
        }

        public static byte[] Apply(RlpNode root, MutationSite site, bool binary = false)
        {
            var clone = root.Clone();
            var nodes = clone.Descendants().ToList();
            var node = nodes[site.Node];
            switch (site.Op)
            {
                case MutationOp.LeadingZero:
                    node.Data = new byte[] { 0 }.Concat(node.Data).ToArray();
                    break;
                case MutationOp.WidthPlus:
                    node.Data = node.Data.Concat(new byte[] { 0x5a }).ToArray();
                    break;
                case MutationOp.WidthMinus:
                    node.Data = node.Data.Take(node.Data.Length - 1).ToArray();
                    break;
                case MutationOp.StringToList:
                    node.IsList = true;
                    node.Items = new List<RlpNode> { RlpNode.Str(node.Data) };
                    node.Data = Array.Empty<byte>();
                    break;
                case MutationOp.ListToString:
                    node.Data = node.Items.SelectMany(i => i.Encode()).ToArray();
                    node.IsList = false;
                    node.Items = new List<RlpNode>();
                    break;
                case MutationOp.DropItem:
                    node.Items.RemoveAt(site.Parameter);
                    break;
                case MutationOp.DuplicateItem:
                    node.Items.Insert(site.Parameter, node.Items[site.Parameter].Clone());
                    break;
                case MutationOp.AppendEmpty:
                    node.Items.Add(RlpNode.Str(Array.Empty<byte>()));
                    break;
                case MutationOp.AppendCopy:
                    node.Items.Add(node.Items[node.Items.Count - 1].Clone());
                    break;
                case MutationOp.Boundary:
                    node.Data = BoundaryValues[site.Parameter];
                    break;
                case MutationOp.LongForm:
                    node.Form = LengthForm.LongForm;
                    break;
                case MutationOp.LongFormLeadingZero:
                    node.Form = LengthForm.LongFormLeadingZero;
                    break;
                case MutationOp.Truncate:
                    var encoded = binary ? clone.EncodeBinary() : clone.Encode();
                    var starts = RlpNode.PreorderStarts(clone, binary);
                    return encoded.Take(starts[site.Node]).ToArray();
                case MutationOp.Trailing:
                    return (binary ? clone.EncodeBinary() : clone.Encode()).Concat(TrailingBytes[site.Parameter]).ToArray();
                default:
                    throw new InvalidOperationException(site.Op.ToString());
            }
            return binary ? clone.EncodeBinary() : clone.Encode();
        }

        public static IEnumerable<(MutationSite Site, byte[] Bytes)> Sample(RlpNode root, int count, Random random, bool binary = false)
        {
            var sites = Sites(root).ToList();
            if (sites.Count == 0) yield break;
            var chosen = sites.Count <= count
                ? sites
                : Enumerable.Range(0, sites.Count).OrderBy(_ => random.Next()).Take(count).Select(i => sites[i]).ToList();
            foreach (var site in chosen)
            {
                byte[] bytes;
                try { bytes = Apply(root, site, binary); }
                catch (ArgumentOutOfRangeException) { continue; }
                yield return (site, bytes);
            }
        }
    }
}
