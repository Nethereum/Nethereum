namespace Nethereum.Signer.Bls
{
    public interface IBls12381OutcomeOperations
    {
        bool TryG1Add(byte[] p1, byte[] p2, out byte[] result);
        bool TryG1Msm(byte[][] points, byte[][] scalars, out byte[] result);

        bool TryG2Add(byte[] p1, byte[] p2, out byte[] result);
        bool TryG2Msm(byte[][] points, byte[][] scalars, out byte[] result);

        bool TryPairing(byte[][] g1Points, byte[][] g2Points, out bool pairs);

        bool TryMapFpToG1(byte[] fp, out byte[] result);
        bool TryMapFp2ToG2(byte[] fp2, out byte[] result);
    }
}
