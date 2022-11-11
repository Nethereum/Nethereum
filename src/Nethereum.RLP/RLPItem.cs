namespace Nethereum.RLP
{
    public class RLPItem : IRLPElement
    {
        private readonly byte[] _rlpData;

        public RLPItem(byte[] rlpData)
        {
            this._rlpData = rlpData;
        }

        public byte[] RLPData => GetRLPData();

        private byte[] GetRLPData()
        {
            return _rlpData.Length == 0 ? null : _rlpData;
        }
    }
}