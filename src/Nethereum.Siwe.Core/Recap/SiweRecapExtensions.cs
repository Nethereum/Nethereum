using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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

        public static SiweMessage InitRecap(this SiweMessage siweMessage, CapabilityMap capabilites, string delegateUri)
        {
            siweMessage.InitRecapStatement(capabilites, delegateUri);

            siweMessage.InitRecapResources(capabilites, delegateUri);

            return siweMessage;
        }

        public static void InitRecapStatement(this SiweMessage siweMessage, CapabilityMap capabilites, string delegateUri)
        {
            var lineNum = 0;

            var recapStatementBuilder =
                new StringBuilder(SiweRecapStatementPrefix + delegateUri + SiweRecapStatementSuffix);

            foreach (var siweNamespace in capabilites.Keys)
            {
                var capability = capabilites[siweNamespace];
                foreach (var actionStatement in capability
                    .ToStatementText(siweNamespace))
                {
                    recapStatementBuilder.Append(string.Format(" ({0}) {1}", ++lineNum, actionStatement));
                }
            }

            siweMessage.Statement = recapStatementBuilder.ToString();
        }

        public static void InitRecapResources(this SiweMessage siweMessage, CapabilityMap capabilites, string delegateUri)
        {
            siweMessage.Resources = new List<string>();

            foreach (var siweNamespace in capabilites.Keys)
            {
                var capability = capabilites[siweNamespace];

                siweMessage.Resources.Add(string.Format("{0}:{1}:{2}"
                                                , SiweRecapResourcePrefix
                                                , siweNamespace
                                                , capability.Encode()));
            }
        }

        public static bool HasPermissions(this SiweMessage siweMessage, SiweNamespace siweNamespace, string target, string action)
        {
            var hasPermissions = false;

            var capabilities = new Dictionary<string, SiweRecapCapability>();
            
            if(siweMessage.Resources!= null)
            {
                foreach (var resource in siweMessage.Resources)
                {
                   capabilities.Add(resource, SiweRecapCapability.DecodeResourceUrn(resource, capabilities));
                }
            }

            
            if (capabilities.ContainsKey(siweNamespace.ToString()))
            {
                var capability = capabilities[siweNamespace.ToString()];

                hasPermissions =
                    capability.DefaultActions.Any(x => x.EqualsIgnoreCase(action)) ||
                    capability.TargetedActions.Where(x => x.Key.EqualsIgnoreCase(target))
                                              .Any(x => x.Value.ContainsIgnoreCase(action));
            }

            return hasPermissions;
        }

        public static bool HasStatementMatchingPermissions(this SiweMessage siweMessage)
        {
            var matchingStmtAndPermissions = false;

            if (string.IsNullOrEmpty(siweMessage.Statement) || !siweMessage.Statement.Contains(SiweRecapStatementSuffix))
            {
                throw new SiweRecapException("ERROR!  Invalid recap statement has been provided in the message.");
            }

            if ((siweMessage.Resources == null) || (siweMessage.Resources.Count == 0))
            {
                throw new SiweRecapException("ERROR!  No resources are contained in the message.");
            }

            var siweRecapCapabilitiesText
                = siweMessage.Statement.Substring(siweMessage.Statement.IndexOf(SiweRecapStatementSuffix) + SiweRecapStatementSuffix.Length);

            if (!string.IsNullOrEmpty(siweRecapCapabilitiesText))
            {
                var matchesAllPermissions = true;

                var capabilityPhrases       = new HashSet<string>();
                var namespaceAndActionsList = new List<List<string>>();

                foreach(var minimumSiweRecapPhrase in siweRecapCapabilitiesText.Split('(')
                                         .Where(x => x.Length > SiweRecapCapPhraseMinLength))
                {
                    capabilityPhrases.Add(minimumSiweRecapPhrase.Replace(")", ""));
                }

                var nameSpaceAndActionItemsUntrimmed = capabilityPhrases
                     .Select(x => Regex.Replace(x, @"^[\d-] ", ""))
                     .Select(x => x.Remove(x.LastIndexOf("."), 1).Trim())
                     .Where(x => x.Contains(":"))
                     .Select(x => x.Split(':'));

               foreach (var nameSpaceAndActionItemUntrimmed in nameSpaceAndActionItemsUntrimmed)
                {
                    namespaceAndActionsList.Add(
                        new List<string>() {
                        nameSpaceAndActionItemUntrimmed[0],
                        string.Join(":", nameSpaceAndActionItemUntrimmed.Skip(1).Take(nameSpaceAndActionItemUntrimmed.Length-1).ToArray()).Trim()
                      });
                }

              
                    
                if (namespaceAndActionsList.Count > 0)
                {
                    foreach (var entry in namespaceAndActionsList)
                    {
                        var tempNamespace       = new SiweNamespace(entry[0]);
                        var namespaceCapability = entry[1];

                        if (!string.IsNullOrEmpty(namespaceCapability) && namespaceCapability.Contains(" for "))
                        {
                            var capabilitySplit = namespaceCapability.Split(new[] { " for " }, StringSplitOptions.None);
                            var actions         = capabilitySplit[0]?.Split(',');
                            var targets         = capabilitySplit[1]?.Split(',');

                            if (actions != null)
                            {
                                foreach (var action in actions)
                                {
                                    if (targets != null)
                                    {
                                        foreach (var target in targets)
                                        {
                                            matchesAllPermissions &= siweMessage.HasPermissions(tempNamespace, target.Trim(), action.Trim());
                                        }
                                    }
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
