namespace Nethereum.Uniswap.UniversalRouter.Commands
{
    public class UnknownCommand : UniversalRouterCommand
    {
        public override byte CommandType { get; set; } = 0;
        public byte[] Data { get; set; } = new byte[0];
        public override void DecodeInputData(byte[] data)
        {
            Data = data;
        }
        public override byte[] GetInputData()
        {
            return Data;
        }
    }
    
}
