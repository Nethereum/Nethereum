using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using Newtonsoft.Json;

namespace Nethereum.Quorum.RPC.DTOs
{

    public class RaftNodeInfo
    {
        /// <summary>
        /// DNS name or the host IP address
        /// </summary>
        [JsonProperty(PropertyName = "hostName")]
        public string HostName { get; set; }

        /// <summary>
        /// Indicates if the node is active in the Raft cluster
        /// </summary>
        [JsonProperty(PropertyName = "nodeActive")]
        public bool NodeActive { get; set; }

        /// <summary>
        /// enode ID of the node
        /// </summary>
        [JsonProperty(PropertyName = "nodeId")]
        public string NodeId { get; set; }

        /// <summary>
        ///p2p port
        /// </summary>
        [JsonProperty(PropertyName = "p2pPort")]
        public int P2PPort { get; set; }

        /// <summary>
        ///Raft ID of the node
        /// </summary>
        [JsonProperty(PropertyName = "raftId")]
        public int RaftId { get; set; }

        /// <summary>
        ///Raft port of the node
        /// </summary>
        [JsonProperty(PropertyName = "raftPort")]
        public int RaftPort { get; set; }

        /// <summary>
        ///  role of the node in the Raft cluster(minter/verifier/learner); "" if there is no leader at the network level
        /// </summary>
        [JsonProperty(PropertyName = "role")]
        public string Role { get; set; }

    }
}
