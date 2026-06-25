namespace Nethereum.AppChain.Anchoring.Postgres.Entities
{
    public class AnchorIndexProgress
    {
        public string Id { get; set; }
        public long LastBlockProcessed { get; set; }
    }
}
