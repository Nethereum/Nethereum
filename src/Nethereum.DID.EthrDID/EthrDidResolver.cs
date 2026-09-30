using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;
using Nethereum.DID.EthrDID.EthereumDIDRegistry;
using Nethereum.DID.EthrDID.EthereumDIDRegistry.ContractDefinition;

namespace Nethereum.DID.EthrDID
{
    public class EthrDidResolver
    {
        private readonly IWeb3 _web3;
        private readonly string _registryAddress;

        public EthrDidResolver(IWeb3 web3, string registryAddress = null)
        {
            _web3 = web3;
            _registryAddress = registryAddress ?? EthrDidConstants.MainnetRegistryAddress;
        }

        public async Task<DidDocument> ResolveAsync(string did)
        {
            var parsed = ParseEthrDid(did);
            var address = parsed.Address;

            var service = new EthereumDIDRegistryService(_web3, _registryAddress);
            var owner = await service.IdentityOwnerQueryAsync(address).ConfigureAwait(false);

            var events = await CollectEventsAsync(service, address).ConfigureAwait(false);

            return BuildDocument(did, address, owner, events);
        }

        private async Task<List<DIDEvent>> CollectEventsAsync(EthereumDIDRegistryService service, string address)
        {
            var events = new List<DIDEvent>();
            var lastBlock = await service.ChangedQueryAsync(address).ConfigureAwait(false);

            while (lastBlock > 0)
            {
                var blockParam = new BlockParameter((ulong)lastBlock);
                var filter = new NewFilterInput
                {
                    FromBlock = blockParam,
                    ToBlock = blockParam,
                    Address = new[] { _registryAddress }
                };

                var ownerChangedHandler = _web3.Eth.GetEvent<DIDOwnerChangedEventDTO>(_registryAddress);
                var delegateChangedHandler = _web3.Eth.GetEvent<DIDDelegateChangedEventDTO>(_registryAddress);
                var attributeChangedHandler = _web3.Eth.GetEvent<DIDAttributeChangedEventDTO>(_registryAddress);

                var ownerFilter = ownerChangedHandler.CreateFilterInput(address, blockParam, blockParam);
                var delegateFilter = delegateChangedHandler.CreateFilterInput(address, blockParam, blockParam);
                var attributeFilter = attributeChangedHandler.CreateFilterInput(address, blockParam, blockParam);

                var ownerLogs = await ownerChangedHandler.GetAllChangesAsync(ownerFilter).ConfigureAwait(false);
                var delegateLogs = await delegateChangedHandler.GetAllChangesAsync(delegateFilter).ConfigureAwait(false);
                var attributeLogs = await attributeChangedHandler.GetAllChangesAsync(attributeFilter).ConfigureAwait(false);

                BigInteger previousChange = 0;

                foreach (var log in ownerLogs)
                {
                    events.Add(new DIDEvent { EventType = DIDEventType.OwnerChanged, OwnerChanged = log.Event });
                    if (log.Event.PreviousChange > previousChange)
                        previousChange = log.Event.PreviousChange;
                }

                foreach (var log in delegateLogs)
                {
                    events.Add(new DIDEvent { EventType = DIDEventType.DelegateChanged, DelegateChanged = log.Event });
                    if (log.Event.PreviousChange > previousChange)
                        previousChange = log.Event.PreviousChange;
                }

                foreach (var log in attributeLogs)
                {
                    events.Add(new DIDEvent { EventType = DIDEventType.AttributeChanged, AttributeChanged = log.Event });
                    if (log.Event.PreviousChange > previousChange)
                        previousChange = log.Event.PreviousChange;
                }

                lastBlock = previousChange;
            }

            return events;
        }

        private DidDocument BuildDocument(string did, string address, string owner, List<DIDEvent> events)
        {
            var doc = new DidDocument
            {
                Context = new List<object>
                {
                    DidConstants.DidContextV1,
                    DidConstants.DidContextSecuritySuitesSecp256k1Recovery_2020
                },
                Id = did,
                Controller = new List<string>(),
                VerificationMethod = new List<VerificationMethod>(),
                Authentication = new List<VerificationRelationship>(),
                AssertionMethod = new List<VerificationRelationship>(),
                Service = new List<Service>()
            };

            var controllerId = did + "#controller";
            var controllerDid = "did:ethr:" + owner.ToLower();

            doc.Controller.Add(controllerDid);

            doc.VerificationMethod.Add(new VerificationMethod
            {
                Id = controllerId,
                Type = DidConstants.EcdsaSecp256k1RecoveryMethod2020,
                Controller = did,
                BlockchainAccountId = "eip155:1:" + owner.ToLower()
            });

            doc.Authentication.Add(new VerificationRelationship(controllerId));
            doc.AssertionMethod.Add(new VerificationRelationship(controllerId));

            int delegateCount = 0;
            int serviceCount = 0;

            foreach (var evt in events)
            {
                switch (evt.EventType)
                {
                    case DIDEventType.DelegateChanged:
                        var delEvt = evt.DelegateChanged;
                        var delegateTypeStr = GetBytes32String(delEvt.DelegateType);

                        if (delEvt.ValidTo > 0)
                        {
                            delegateCount++;
                            var delegateId = did + "#delegate-" + delegateCount;
                            var vm = new VerificationMethod
                            {
                                Id = delegateId,
                                Type = DidConstants.EcdsaSecp256k1RecoveryMethod2020,
                                Controller = did,
                                BlockchainAccountId = "eip155:1:" + delEvt.Delegate.ToLower()
                            };

                            doc.VerificationMethod.Add(vm);

                            if (delegateTypeStr == EthrDidConstants.DelegateTypeVeriKey)
                            {
                                doc.AssertionMethod.Add(new VerificationRelationship(delegateId));
                            }
                            else if (delegateTypeStr == EthrDidConstants.DelegateTypeSignAuth)
                            {
                                doc.Authentication.Add(new VerificationRelationship(delegateId));
                            }
                        }
                        break;

                    case DIDEventType.AttributeChanged:
                        var attrEvt = evt.AttributeChanged;
                        var attrName = GetBytes32String(attrEvt.Name);

                        if (attrEvt.ValidTo > 0)
                        {
                            if (attrName.StartsWith(EthrDidConstants.AttributePubKeyPrefix))
                            {
                                delegateCount++;
                                var keyId = did + "#delegate-" + delegateCount;
                                var keyParts = attrName.Substring(EthrDidConstants.AttributePubKeyPrefix.Length).Split('/');
                                var algorithm = keyParts.Length > 0 ? keyParts[0] : "";
                                var encoding = keyParts.Length > 2 ? keyParts[2] : "hex";

                                var keyVm = new VerificationMethod
                                {
                                    Id = keyId,
                                    Controller = did
                                };

                                if (algorithm == "Secp256k1")
                                    keyVm.Type = DidConstants.EcdsaSecp256k1VerificationKey2019;
                                else if (algorithm == "Ed25519")
                                    keyVm.Type = DidConstants.Ed25519VerificationKey2018;
                                else
                                    keyVm.Type = algorithm;

                                if (encoding == "hex")
                                    keyVm.PublicKeyHex = attrEvt.Value.ToHex();
                                else if (encoding == "base64")
                                    keyVm.PublicKeyBase64 = Convert.ToBase64String(attrEvt.Value);
                                else if (encoding == "base58")
                                    keyVm.PublicKeyBase58 = Encoding.UTF8.GetString(attrEvt.Value);
                                else
                                    keyVm.PublicKeyHex = attrEvt.Value.ToHex();

                                doc.VerificationMethod.Add(keyVm);
                                doc.AssertionMethod.Add(new VerificationRelationship(keyId));
                            }
                            else if (attrName.StartsWith(EthrDidConstants.AttributeServicePrefix))
                            {
                                serviceCount++;
                                var serviceType = attrName.Substring(EthrDidConstants.AttributeServicePrefix.Length);
                                var endpoint = Encoding.UTF8.GetString(attrEvt.Value);

                                doc.Service.Add(new Service
                                {
                                    Id = did + "#service-" + serviceCount,
                                    Type = serviceType,
                                    ServiceEndpoint = endpoint
                                });
                            }
                        }
                        break;

                    case DIDEventType.OwnerChanged:
                        var ownerEvt = evt.OwnerChanged;
                        doc.Controller = new List<string> { "did:ethr:" + ownerEvt.Owner.ToLower() };
                        doc.VerificationMethod[0] = new VerificationMethod
                        {
                            Id = controllerId,
                            Type = DidConstants.EcdsaSecp256k1RecoveryMethod2020,
                            Controller = did,
                            BlockchainAccountId = "eip155:1:" + ownerEvt.Owner.ToLower()
                        };
                        break;
                }
            }

            if (doc.Service.Count == 0)
                doc.Service = null;

            return doc;
        }

        private static string GetBytes32String(byte[] bytes)
        {
            if (bytes == null) return string.Empty;
            int length = bytes.Length;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] == 0)
                {
                    length = i;
                    break;
                }
            }
            return Encoding.UTF8.GetString(bytes, 0, length);
        }

        private EthrDidParsed ParseEthrDid(string did)
        {
            var url = DidUrlParser.Parse(did);

            if (url.Method != EthrDidConstants.MethodName)
                throw new ArgumentException("Not a did:ethr identifier: " + did);

            var parts = url.Id.Split(':');
            if (parts.Length == 1)
            {
                return new EthrDidParsed { Address = parts[0], ChainId = 1 };
            }
            else if (parts.Length == 2)
            {
                long chainId;
                if (long.TryParse(parts[0], out chainId))
                {
                    return new EthrDidParsed { Address = parts[1], ChainId = chainId };
                }
                return new EthrDidParsed { Address = parts[1], ChainId = 1, NetworkName = parts[0] };
            }

            throw new FormatException("Invalid did:ethr format: " + did);
        }

        private class EthrDidParsed
        {
            public string Address { get; set; }
            public long ChainId { get; set; }
            public string NetworkName { get; set; }
        }

        private class DIDEvent
        {
            public DIDEventType EventType { get; set; }
            public DIDOwnerChangedEventDTO OwnerChanged { get; set; }
            public DIDDelegateChangedEventDTO DelegateChanged { get; set; }
            public DIDAttributeChangedEventDTO AttributeChanged { get; set; }
        }

        private enum DIDEventType
        {
            OwnerChanged,
            DelegateChanged,
            AttributeChanged
        }
    }
}
