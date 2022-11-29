using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Nethereum.Siwe.Core;

namespace Nethereum.Siwe.Core.Recap
{
    using CapabilityMap = Dictionary<SiweNamespace, SiweRecapCapability>;

    public static class SiweRecapExtensions
    {
        public const int SiweRecapCapPhraseMinLength = 5;

        public const string SiweRecapResourcePrefix = "urn:recap";

        public const string SiweRecapStatementPrefix = "I further authorize ";
        public const string SiweRecapStatementSuffix = " to perform the following actions on my behalf:";

        public static bool ContainsIgnoreCase(this IEnumerable<string> container, string rvalue)
        {
            return container.Any(x => x.EqualsIgnoreCase(rvalue));
        }

        public static bool EqualsIgnoreCase(this string lvalue, string rvalue)
        {
            return lvalue.Equals(rvalue, StringComparison.OrdinalIgnoreCase);
        }

        public static SiweMessage InitRecap(this SiweMessage msg, CapabilityMap capabilites, string delegateUri)
        {
            msg.InitRecapStatement(capabilites, delegateUri);

            msg.InitRecapResources(capabilites, delegateUri);

            return msg;
        }

        public static void InitRecapStatement(this SiweMessage msg, CapabilityMap capabilites, string delegateUri)
        {
            var lineNum = 0;

            StringBuilder recapStatementBuilder =
                new StringBuilder(SiweRecapStatementPrefix + delegateUri + SiweRecapStatementSuffix);

            foreach (var siweNamespace in capabilites.Keys)
            {
                var capability = capabilites[siweNamespace];

                capability
                    .ToStatementText(siweNamespace)
                    .ToList()
                    .ForEach(actionStmt =>
                             recapStatementBuilder.Append(string.Format(" ({0}) {1}", ++lineNum, actionStmt)));
            }

            msg.Statement = recapStatementBuilder.ToString();
        }

        public static void InitRecapResources(this SiweMessage msg, CapabilityMap capabilites, string delegateUri)
        {
            msg.Resources = new List<string>();

            foreach (var siweNamespace in capabilites.Keys)
            {
                var capability = capabilites[siweNamespace];

                msg.Resources.Add(string.Format("{0}:{1}:{2}"
                                                , SiweRecapResourcePrefix
                                                , siweNamespace
                                                , capability.Encode()));
            }
        }

        public static bool HasPermissions(this SiweMessage msg, SiweNamespace ns, string target, string action)
        {
            bool hasPermissions = false;

            Dictionary<string, SiweRecapCapability>? capabilities = new Dictionary<string, SiweRecapCapability>();

            msg.Resources?.ForEach(x => SiweRecapCapability.DecodeResourceUrn(x, capabilities));

            if (capabilities.ContainsKey(ns.ToString()))
            {
                SiweRecapCapability capability = capabilities[ns.ToString()];

                hasPermissions =
                    capability.DefaultActions.Any(x => x.EqualsIgnoreCase(action)) ||
                    capability.TargetedActions.Where(x => x.Key.EqualsIgnoreCase(target))
                                              .Any(x => x.Value.ContainsIgnoreCase(action));
            }

            return hasPermissions;
        }

        public static bool HasStatementMatchingPermissions(this SiweMessage msg)
        {
            bool matchingStmtAndPermissions = false;

            if (String.IsNullOrEmpty(msg.Statement) || !msg.Statement.Contains(SiweRecapStatementSuffix))
            {
                throw new SiweRecapException("ERROR!  Invalid recap statement has been provided in the message.");
            }

            if ((msg.Resources == null) || (msg.Resources.Count == 0))
            {
                throw new SiweRecapException("ERROR!  No resources are contained in the message.");
            }

            var siweRecapCapabilitiesText
                = msg.Statement.Substring(msg.Statement.IndexOf(SiweRecapStatementSuffix) + SiweRecapStatementSuffix.Length);

            if (!String.IsNullOrEmpty(siweRecapCapabilitiesText))
            {
                bool matchesAllPermissions = true;

                var capabilityPhrases       = new HashSet<string>();
                var namespaceAndActionsList = new List<List<string>>();

                siweRecapCapabilitiesText.Split('(')
                                         .Where(x => x.Length > SiweRecapCapPhraseMinLength)
                                         .ToList()
                                         .ForEach(x => capabilityPhrases.Add(x.Replace(")", "")));

                capabilityPhrases
                    .Select(x => Regex.Replace(x, @"^[\d-] ", ""))
                    .Select(x => x.Remove(x.LastIndexOf("."), 1).Trim())
                    .Where(x => x.Contains(":"))
                    .Select(x => x.Split(':'))
                    .ToList()
                    .ForEach(x => namespaceAndActionsList.Add(new List<string>() {
                                                                x[0],
                                                                String.Join(':', x.Skip(1).Take(x.Length-1).ToArray()).Trim()
                                                                }));
                if (namespaceAndActionsList.Count > 0)
                {
                    foreach (var entry in namespaceAndActionsList)
                    {
                        var tempNamespace       = new SiweNamespace(entry[0]);
                        var namespaceCapability = entry[1];

                        if (!String.IsNullOrEmpty(namespaceCapability) && namespaceCapability.Contains(" for "))
                        {
                            var capabilitySplit = namespaceCapability.Split(" for ");
                            var actions         = capabilitySplit[0]?.Split(',');
                            var targets         = capabilitySplit[1]?.Split(',');

                            if (actions != null)
                            {
                                foreach (var action in actions)
                                {
                                    targets?.ToList()
                                            .ForEach(x => matchesAllPermissions &= msg.HasPermissions(tempNamespace, x.Trim(), action.Trim()));
                                }
                            }
                        }
                        else
                        {
                            matchesAllPermissions = false;
                            break;
                        }
                    }
                }
                else
                {
                    matchesAllPermissions = false;
                }

                matchingStmtAndPermissions = matchesAllPermissions;
            }

            return matchingStmtAndPermissions;
        }
    }

}
