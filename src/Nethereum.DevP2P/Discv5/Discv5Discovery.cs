using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Nethereum.Documentation;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;

namespace Nethereum.DevP2P.Discv5
{
    [NethereumDocExample(DocSection.DevP2P, "devp2p", "Discv5Discovery.StartMainnet — one-call discv5 peer discovery")]
    public static class Discv5Discovery
    {
        public static (Discv5PeerDiscoveryService Discovery, Discv5Listener Listener) StartMainnet(
            EthECKey localKey,
            Action<string> enqueueEnode,
            IPAddress bindAddress = null,
            int udpPort = 0,
            Action<string> log = null,
            Func<byte[], bool> ethForkIdFilter = null,
            CancellationToken ct = default)
        {
            if (localKey == null) throw new ArgumentNullException(nameof(localKey));
            if (enqueueEnode == null) throw new ArgumentNullException(nameof(enqueueEnode));

            var bind = bindAddress ?? IPAddress.Any;
            var listener = new Discv5Listener(localKey);
            listener.Start(bind, udpPort);

            AttachSignedLocalEnr(listener, localKey, bind);

            var bootnodes = new List<(EnrRecord Enr, IPEndPoint Endpoint)>(Discv5Bootnodes.ResolveMainnet());
            var discovery = new Discv5PeerDiscoveryService(listener, enqueueEnode, bootnodes, log, ethForkIdFilter);
            discovery.StartAsync(ct).GetAwaiter().GetResult();

            return (discovery, listener);
        }

        private static void AttachSignedLocalEnr(Discv5Listener listener, EthECKey localKey, IPAddress bind)
        {
            var port = listener.Port;
            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["ip"] = bind.GetAddressBytes();
            enr.Pairs["udp"] = new byte[] { (byte)((port >> 8) & 0xff), (byte)(port & 0xff) };
            EnrRecordSigner.Sign(enr, localKey);
            listener.LocalEnrEncoded = EnrRecordEncoder.EncodeRecord(enr);
            listener.LocalEnrSequence = enr.Sequence;
        }
    }
}
