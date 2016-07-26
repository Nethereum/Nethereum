namespace Nethereum.ABI.Util.RLP
{
    public class RLPItem : IRLPElement
    {
        private readonly byte[] rlpData;
        public byte[] RLPData => GetRLPData();

        public RLPItem(byte[] rlpData)
        {
            this.rlpData = rlpData;
        }

        private byte[] GetRLPData()
        {
            return rlpData.Length == 0 ? null : rlpData;
        }
    }
}