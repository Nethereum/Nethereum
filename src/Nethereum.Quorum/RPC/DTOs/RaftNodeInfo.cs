using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Nethereum.Quorum.RPC.DTOs
{
    [DataContract]
    public class RaftNodeInfo
    {
        /// <summary>
        /// DNS name or the host IP address
        /// </summary>
        [DataMember(Name = "hostName")]
        public string HostName { get; set; }

        /// <summary>
        /// Indicates if the node is active in the Raft cluster
        /// </summary>
        [DataMember(Name = "nodeActive")]
        public bool NodeActive { get; set; }

        /// <summary>
        /// enode ID of the node
        /// </summary>
        [DataMember(Name = "nodeId")]
        public string NodeId { get; set; }

        /// <summary>
        ///p2p port
        /// </summary>
        [DataMember(Name = "p2pPort")]
        public int P2PPort { get; set; }

        /// <summary>
        ///Raft ID of the node
        /// </summary>
        [DataMember(Name = "raftId")]
        public int RaftId { get; set; }

        /// <summary>
        ///Raft port of the node
        /// </summary>
        [DataMember(Name = "raftPort")]
        public int RaftPort { get; set; }

        /// <summary>
        ///  role of the node in the Raft cluster(minter/verifier/learner); "" if there is no leader at the network level
        /// </summary>
        [DataMember(Name = "role")]
        public string Role { get; set; }

    }
}
