using System;
using System.Collections.Generic;
using System.Linq;

namespace Nethereum.DevP2P.IntegrationTests.GethReference
{
    public enum Agreement
    {
        AgreeAccept,
        AgreeRefuse,
        PolicyDeferred,
        Disagree
    }

    public sealed class ComparisonResult
    {
        public Agreement Agreement { get; set; }
        public string Reason { get; set; }
        public string Class { get; set; }
    }

    public static class DecodeComparer
    {
        private static readonly (string Kind, string Marker)[] DeferredPolicies =
        {
            ("eth_BlockRangeUpdate", "earliest > latest"),
            ("eth_BlockRangeUpdate", "zero latest hash"),
            ("eth_Status", "earliest > latest"),
            ("eth_Status", "zero latest hash")
        };

        public const string ClassWiderScalar = "class:C1";
        public const string ClassUnknownHeaderShape = "class:C2";
        public const string ClassEmptySignature = "class:C3";
        public const string ClassUnavailableMarker = "class:D1";

        public static ComparisonResult Compare(string kind, GethAnswer geth, NethereumVerdict nethereum)
        {
            var gethRefuses = !geth.Ok || !geth.ItemsOk;
            var policy = geth.PolicyRefusal;

            if (gethRefuses)
            {
                if (!nethereum.Accepted) return new ComparisonResult { Agreement = Agreement.AgreeRefuse };
                var unavailable = kind == "eth_BlockAccessLists" && geth.Error != null && geth.Error.Contains("unavailable marker (empty string entry)");
                return Disagreement("geth refuses (" + Truncate(geth.Error) + ") but Nethereum accepts", unavailable ? ClassUnavailableMarker : null);
            }

            if (policy != null)
            {
                if (nethereum.Accepted && DeferredPolicies.Any(d => d.Kind == kind && policy.Contains(d.Marker)))
                    return new ComparisonResult { Agreement = Agreement.PolicyDeferred };
                return nethereum.Accepted
                    ? Disagreement("geth refuses at its handler or validator (" + policy + ") but Nethereum accepts")
                    : new ComparisonResult { Agreement = Agreement.AgreeRefuse };
            }

            if (!nethereum.Accepted)
                return Disagreement("geth accepts but Nethereum refuses (" + Truncate(nethereum.Error) + ")", ClassOfRefusal(nethereum.Error));

            var difference = FirstDifference(geth, nethereum);
            return difference == null
                ? new ComparisonResult { Agreement = Agreement.AgreeAccept }
                : Disagreement(difference);
        }

        private static string FirstDifference(GethAnswer geth, NethereumVerdict nethereum)
        {
            if (geth.Canonical != null && nethereum.Canonical != null
                && !string.Equals(geth.Canonical, nethereum.Canonical, StringComparison.OrdinalIgnoreCase))
                return "canonical re-encoding differs";

            if (geth.Hash != null && nethereum.Hash != null
                && !string.Equals(geth.Hash, nethereum.Hash, StringComparison.OrdinalIgnoreCase))
                return "hash differs";

            var gethSender = geth.Text("sender");
            if (gethSender != null && nethereum.Sender != null && !string.Equals(gethSender, nethereum.Sender, StringComparison.OrdinalIgnoreCase))
                return "sender differs";

            var gethCount = geth.Number("count");
            if (gethCount != null && nethereum.Count != null && gethCount != nethereum.Count)
                return "item count differs: geth " + gethCount + ", Nethereum " + nethereum.Count;

            var gethId = geth.Unsigned("id");
            if (gethId != null && nethereum.Id != null && gethId != nethereum.Id)
                return "request id differs";

            var gethHashes = geth.Strings("hashes");
            if (gethHashes != null && nethereum.Hashes != null && !gethHashes.SequenceEqual(nethereum.Hashes, StringComparer.OrdinalIgnoreCase))
                return "element hashes differ";

            var gethBodies = geth.Strings("bodies");
            if (gethBodies != null && nethereum.Bodies != null && !gethBodies.SequenceEqual(nethereum.Bodies, StringComparer.OrdinalIgnoreCase))
                return "re-encoded bodies differ";

            return null;
        }

        private static string ClassOfRefusal(string error)
        {
            if (error == null) return null;
            if (error.StartsWith("ScalarWiderThanItsFieldException:") || error.Contains("RLP_INVALID_FIELD_OVERFLOW"))
                return ClassWiderScalar;
            if (error.Contains("matches no fork this build knows"))
                return ClassUnknownHeaderShape;
            if (error.StartsWith("TransactionFieldFormatException: RLP_INVALID_SIGNATURER: the field is empty"))
                return ClassEmptySignature;
            return null;
        }

        private static ComparisonResult Disagreement(string reason, string disagreementClass = null) =>
            new ComparisonResult { Agreement = Agreement.Disagree, Reason = reason, Class = disagreementClass };

        private static string Truncate(string text) =>
            text == null ? "" : text.Length <= 120 ? text : text.Substring(0, 120);
    }
}
