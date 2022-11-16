using System.Collections.Generic;
using Nethereum.Siwe.Core;
using Nethereum.Siwe.Core.Recap;
using Xunit;

namespace Nethereum.Siwe.UnitTests
{
    public class SiweRecapTests
    {
        [Fact]
        public void BasicTest()
        {
            string SiweRecapUri = "did:key:example";

            SiweNamespace credentialNamespace = new SiweNamespace("credential");
            SiweNamespace keplerNamespace     = new SiweNamespace("kepler");

            var emptyList     = new HashSet<string>();
            var emptyMap      = new Dictionary<string,string>();
            var capabilityMap = new Dictionary<SiweNamespace, SiweRecapCapability>();

            var defaultCredentialActions = new HashSet<string>() {"present"};

            var targetCredentialActions = new Dictionary<string, HashSet<string>>();
            targetCredentialActions["type:type1"] = new HashSet<string>() {"present"};

            var targetKeplerActions = new Dictionary<string, HashSet<string>>();
            targetKeplerActions["kepler:ens:example.eth://default/kv"] = new HashSet<string>() {"list", "get", "metadata"};
            targetKeplerActions["kepler:ens:example.eth://default/kv/public"] = new HashSet<string>() {"list", "get", "metadata", "put", "delete"};
            targetKeplerActions["kepler:ens:example.eth://default/kv/dapp-space"] = new HashSet<string>() {"list", "get", "metadata", "put", "delete"};

            capabilityMap[credentialNamespace] =
                new SiweRecapCapability(defaultCredentialActions, targetCredentialActions, emptyMap);

            capabilityMap[keplerNamespace] = 
                new SiweRecapCapability(emptyList, targetKeplerActions, emptyMap);

            SiweMessage recapMessage =
                new SiweMessage()
                {
                    Domain = "example.com"
                    , Address = "0x94618601fe6cb8912b274e5a00453949a57f8c1e"
                    , Statement = string.Empty
                    , Uri = SiweRecapUri
                    , Version = "v1"
                    , ChainId = "1"
                    , Nonce = "MyNonce1"
                    , IssuedAt = "2022-06-21T12:00:00.000Z"
                    , ExpirationTime = string.Empty
                    , NotBefore = string.Empty
                    , RequestId = string.Empty
                }.InitRecap(capabilityMap, SiweRecapUri);

            bool canCredentialPresent = 
                recapMessage.HasPermissions(credentialNamespace, string.Empty, "present");

            Assert.True(canCredentialPresent);

            bool canKeplerPublicMetadata =
                recapMessage.HasPermissions(keplerNamespace, "kepler:ens:example.eth://default/kv/public", "metadata");

            Assert.True(canKeplerPublicMetadata);

            bool canKeplerPublicStargaze =
                recapMessage.HasPermissions(keplerNamespace, "kepler:ens:example.eth://default/kv/public", "stargaze");

            Assert.False(canKeplerPublicStargaze);
        }

        [Fact]
        public void BuilderTest()
        {
            string SiweRecapUri = "did:key:example";

            SiweNamespace credentialNamespace = new SiweNamespace("credential");
            SiweNamespace keplerNamespace     = new SiweNamespace("kepler");

            SiweMessage recapMessageDeux =
                SiweRecapMsgBuilder.Init(new SiweMessage()
                                         {
                                            Domain = "example.com"
                                            , Address = "0x94618601fe6cb8912b274e5a00453949a57f8c1e"
                                            , Statement = string.Empty
                                            , Uri = SiweRecapUri
                                            , Version = "v1"
                                            , ChainId = "1"
                                            , Nonce = "MyNonce1"
                                            , IssuedAt = "2022-06-21T12:00:00.000Z"
                                            , ExpirationTime = string.Empty
                                            , NotBefore = string.Empty
                                            , RequestId = string.Empty
                                         })
                                   .AddDefaultActions(credentialNamespace, new HashSet<string>() { "present" })
                                   .AddTargetActions(credentialNamespace, "type:type1", new HashSet<string>() { "present" })
                                   .AddTargetActions(keplerNamespace, "kepler:ens:example.eth://default/kv", new HashSet<string>() { "list", "get", "metadata" })
                                   .AddTargetActions(keplerNamespace, "kepler:ens:example.eth://default/kv/public", new HashSet<string>() { "list", "get", "metadata", "put", "delete" })
                                   .AddTargetActions(keplerNamespace, "kepler:ens:example.eth://default/kv/dapp-space", new HashSet<string>() { "list", "get", "metadata", "put", "delete" })
                                   .Build();

            bool canCredentialPresent =
                recapMessageDeux.HasPermissions(credentialNamespace, string.Empty, "present");

            Assert.True(canCredentialPresent);

            bool canKeplerPublicMetadata =
                recapMessageDeux.HasPermissions(keplerNamespace, "kepler:ens:example.eth://default/kv/public", "metadata");

            Assert.True(canKeplerPublicMetadata);

            bool canKeplerPublicStargaze =
                recapMessageDeux.HasPermissions(keplerNamespace, "kepler:ens:example.eth://default/kv/public", "stargaze");

            Assert.False(canKeplerPublicStargaze);
        }

    }
}
