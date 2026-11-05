using System;

namespace Nethereum.AppChain.Anchoring.Postgres
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.DataIndexing, "appchain-anchoring-postgres", "Message block progress")]
    public class MessageBlockProgress
    {
        public int Id { get; set; }
        public long SourceChainId { get; set; }
        public string LastBlockProcessed { get; set; } = "";
        public DateTime? UpdatedAt { get; set; }
    }
}
