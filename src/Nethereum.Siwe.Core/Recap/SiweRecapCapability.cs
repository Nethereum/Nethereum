using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

namespace Nethereum.Siwe.Core.Recap
{
    using NamespaceActionsMap = Dictionary<string, HashSet<string>>;

    internal class SiweRecapCapabilitySeed
    {
        [JsonProperty("def")]
        public HashSet<string> DefaultActions { get; set; }

        [JsonProperty("tar")]
        public NamespaceActionsMap TargetedActions { get; set; }

        public SiweRecapCapabilitySeed()
        { 
            DefaultActions  = new HashSet<string>();
            TargetedActions = new NamespaceActionsMap();
        }
    }

    public class SiweRecapCapability
    {
        public const string DefaultTarget = "any";

        private readonly HashSet<string> _defaultActions;

        private readonly NamespaceActionsMap _targetedActions;

        private readonly Dictionary<string, string> _extraFields;

        [JsonProperty("def")]
        public HashSet<string> DefaultActions { get { return _defaultActions; } }

        [JsonProperty("tar")]
        public NamespaceActionsMap TargetedActions { get { return _targetedActions; } }

        public SiweRecapCapability(HashSet<string> defaultActions,
                               NamespaceActionsMap targetedActions,
                        Dictionary<string, string> extraFields)
        {
            _defaultActions  = defaultActions;
            _targetedActions = targetedActions;
            _extraFields     = extraFields;
        }

        public static SiweRecapCapability? DecodeResourceUrn(string resourceUrn, 
                           Dictionary<string, SiweRecapCapability>? capabilityMap = null)
        {
            if (string.IsNullOrEmpty(resourceUrn))
            {
                throw new SiweRecapException("Resource Urn is empty or null.");
            }

            if (!resourceUrn.ToLower().StartsWith(SiweRecapExtensions.SiweRecapResourcePrefix))
            {
                throw new SiweRecapException("Resource Urn is not of the SIWE Recap type.");
            }

            string[] resourceUrnFields = resourceUrn.Split(':');
            if (resourceUrnFields.Count() < 4)
            {
                throw new SiweRecapException("Resource Urn has incorrect number of fields.");
            }

            string siweNamespaceField    = resourceUrnFields[2];
            string encodedJsonCapability = resourceUrnFields[3];

            if (string.IsNullOrEmpty(siweNamespaceField))
            {
                throw new SiweRecapException("Resource Urn has a null/empty namespace.");
            }

            if (string.IsNullOrEmpty(encodedJsonCapability))
            {
                throw new SiweRecapException("Resource Urn has a null/empty Recap Object.");
            }

            SiweNamespace siweNamespace = new SiweNamespace(siweNamespaceField);

            string decodedJsonCapability =
                Encoding.ASCII.GetString(Convert.FromBase64String(encodedJsonCapability));

            SiweRecapCapabilitySeed? capabilitySeed =
                 JsonConvert.DeserializeObject<SiweRecapCapabilitySeed?>(decodedJsonCapability);

            SiweRecapCapability? capability = null;

            if (capabilitySeed != null)
            {
                capability =
                    new SiweRecapCapability(capabilitySeed.DefaultActions
                                            , capabilitySeed.TargetedActions
                                            , new Dictionary<string, string>());

                if ((capabilityMap != null) && (capability != null))
                {
                    if (!capabilityMap.ContainsKey(siweNamespace.ToString()))
                    {
                        capabilityMap[siweNamespace.ToString()] = capability;
                    }
                }
            }

            return capability;
        }

        public string Encode()
        {
            string jsonCapability = JsonConvert.SerializeObject(this, Formatting.Indented);

            return Convert.ToBase64String(Encoding.ASCII.GetBytes(jsonCapability));
        }

        public bool HasTargetPermission(string target, string action)
        {
            HashSet<string>? targetActions = null;

            return _targetedActions.TryGetValue(target, out targetActions) &&
                   (HasPermissionByDefault(action) || targetActions.Any(x => x.ToLower() == action.ToLower()));
        }

        public bool HasPermissionByDefault(string action)
        {
            return _defaultActions.Any(x => x.ToLower() == action.ToLower());
        }

        public HashSet<string> ToStatementText(SiweNamespace siweNamespace)
        {            
            var capabilityTextLines = new HashSet<string>();

            if (_defaultActions.Count > 0)
            {
                capabilityTextLines.Add(FormatDefaultActions(siweNamespace, _defaultActions));
            }

            foreach (var target in _targetedActions.Keys)
            {
                capabilityTextLines.Add(FormatActions(siweNamespace, target, _targetedActions[target]));
            }

            return capabilityTextLines;
        }

        #region Static Methods

        static public string FormatDefaultActions(SiweNamespace siweNamespace, HashSet<string> actions)
        {
            return FormatActions(siweNamespace, DefaultTarget, actions);
        }

        static public string FormatActions(SiweNamespace siweNamespace, string target, HashSet<string> actions)
        {
            return string.Format("{0}: {1} for {2}.", siweNamespace, string.Join(", ", actions), target);
        }

        #endregion 
    }
}