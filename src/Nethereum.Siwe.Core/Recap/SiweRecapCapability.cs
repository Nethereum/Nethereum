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

        [JsonIgnore]
        public Dictionary<string, string> ExtraFields { get { return _extraFields; } }

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

            var resourceUrnFields = resourceUrn.Split(':');
            if (resourceUrnFields.Count() < 4)
            {
                throw new SiweRecapException("Resource Urn has incorrect number of fields.");
            }

            var siweNamespaceField    = resourceUrnFields[2];
            var encodedJsonCapability = resourceUrnFields[3];

            if (string.IsNullOrEmpty(siweNamespaceField))
            {
                throw new SiweRecapException("Resource Urn has a null/empty namespace.");
            }

            if (string.IsNullOrEmpty(encodedJsonCapability))
            {
                throw new SiweRecapException("Resource Urn has a null/empty Recap Object.");
            }

            var siweNamespace = new SiweNamespace(siweNamespaceField);
            var base64EncodingCapability = Convert.FromBase64String(encodedJsonCapability);
#if NETSTANDARD1_1
            var decodedJsonCapability =
               Encoding.UTF8.GetString(base64EncodingCapability, 0, base64EncodingCapability.Length);
#else
            var decodedJsonCapability =
                   Encoding.ASCII.GetString(base64EncodingCapability, 0, base64EncodingCapability.Length);
#endif
            var capabilitySeed =
                 JsonConvert.DeserializeObject<SiweRecapCapabilitySeed?>(decodedJsonCapability);

            SiweRecapCapability capability = null;

            if (capabilitySeed != null)
            {
                capability =
                    new SiweRecapCapability(capabilitySeed.DefaultActions
                                            , capabilitySeed.TargetedActions
                                            , new Dictionary<string, string>());

                if ((capabilityMap != null) && !capabilityMap.ContainsKey(siweNamespace.ToString()))
                {
                    capabilityMap[siweNamespace.ToString()] = capability;
                }
            }

            return capability;
        }

        public string Encode(Formatting formatting = Formatting.Indented)
        {
            var jsonCapability = JsonConvert.SerializeObject(this, formatting);

#if NETSTANDARD1_1
            var encodedJsonCapability = Encoding.UTF8.GetBytes(jsonCapability);
#else
            var encodedJsonCapability = Encoding.ASCII.GetBytes(jsonCapability);
#endif

            return Convert.ToBase64String(encodedJsonCapability);
        }

        public bool HasTargetPermission(string target, string action)
        {
            HashSet<string> targetActions = null;

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
#if DOTNET35
            return string.Format("{0}: {1} for {2}.", siweNamespace, string.Join(", ", actions.ToArray()), target);
#else
            return string.Format("{0}: {1} for {2}.", siweNamespace, string.Join(", ", actions.ToArray()), target);
#endif
        }

        #endregion
    }
}