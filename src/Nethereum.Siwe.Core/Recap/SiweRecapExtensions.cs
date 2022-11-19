using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Nethereum.Siwe.Core;

namespace Nethereum.Siwe.Core.Recap
{
    using CapabilityMap = Dictionary<SiweNamespace, SiweRecapCapability>;

    public static class SiweRecapExtensions
    {
        public const string SiweRecapResourcePrefix = "urn:recap";

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
                new StringBuilder("I further authorize " + delegateUri +
                                  " to perform the following actions on my behalf:");

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
    }
}
