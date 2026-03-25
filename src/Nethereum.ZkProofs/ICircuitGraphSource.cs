namespace Nethereum.ZkProofs
{
    public interface ICircuitGraphSource
    {
        byte[] GetGraphData(string circuitName);
        bool HasGraph(string circuitName);
    }
}
