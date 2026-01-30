namespace Nethereum.Signer.Bls
{
    public interface IBls12381Operations
    {
        byte[] G1Add(byte[] p1, byte[] p2);
        byte[] G1Mul(byte[] point, byte[] scalar);
        byte[] G1Msm(byte[][] points, byte[][] scalars);

        byte[] G2Add(byte[] p1, byte[] p2);
        byte[] G2Mul(byte[] point, byte[] scalar);
        byte[] G2Msm(byte[][] points, byte[][] scalars);

        bool Pairing(byte[][] g1Points, byte[][] g2Points);

        byte[] MapFpToG1(byte[] fp);
        byte[] MapFp2ToG2(byte[] fp2);
    }
}
