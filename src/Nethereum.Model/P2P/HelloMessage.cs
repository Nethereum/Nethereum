using System.Collections.Generic;
using System.Text;
using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    public class P2PCapability
    {
        public string Name { get; set; }
        public int Version { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
    }

    public class HelloMessage
    {
        public int ProtocolVersion { get; set; } = 5;
        public string ClientId { get; set; } = "Nethereum";
        public List<P2PCapability> Capabilities { get; set; } = new();
        public int ListenPort { get; set; }
        public byte[] NodeId { get; set; }
    }

    public static class HelloMessageEncoder
    {
        public static byte[] Encode(HelloMessage msg)
        {
            var caps = new List<byte[]>();
            foreach (var cap in msg.Capabilities)
            {
                caps.Add(RLP.RLP.EncodeList(
                    RLP.RLP.EncodeElement(Encoding.ASCII.GetBytes(cap.Name)),
                    RLP.RLP.EncodeElement(((long)cap.Version).ToBytesForRLPEncoding())
                ));
            }

            return RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(((long)msg.ProtocolVersion).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(Encoding.ASCII.GetBytes(msg.ClientId)),
                RLP.RLP.EncodeList(caps.ToArray()),
                RLP.RLP.EncodeElement(((long)msg.ListenPort).ToBytesForRLPEncoding()),
                RLP.RLP.EncodeElement(msg.NodeId)
            );
        }

        public static HelloMessage Decode(byte[] data)
        {
            var items = (RLPCollection)RLP.RLP.Decode(data);

            var msg = new HelloMessage
            {
                ProtocolVersion = items[0].RLPData.ToIntFromRLPDecoded(),
                // Geth's devp2p tool sends an empty Name/ClientId (rlp encodes
                // to an empty byte string), which our codec sees as null.
                ClientId = items[1].RLPData == null ? string.Empty : Encoding.ASCII.GetString(items[1].RLPData),
                ListenPort = items[3].RLPData == null ? 0 : items[3].RLPData.ToIntFromRLPDecoded(),
                NodeId = items[4].RLPData
            };

            var capsList = (RLPCollection)items[2];
            foreach (RLPCollection cap in capsList)
            {
                msg.Capabilities.Add(new P2PCapability
                {
                    Name = Encoding.ASCII.GetString(cap[0].RLPData),
                    Version = cap[1].RLPData.ToIntFromRLPDecoded()
                });
            }

            return msg;
        }
    }
}
