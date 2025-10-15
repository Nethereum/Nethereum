namespace Nethereum.Uniswap.UniversalRouter.Commands
{
    public abstract class UniversalRouterCommandRevertable : UniversalRouterCommand
    {
        public const byte ALLOW_REVERT_FLAG = 0x80;
        public bool AllowRevert { get; set; }

        public override byte GetFullCommandType()
        {
            return (byte)(CommandType | (AllowRevert ? ALLOW_REVERT_FLAG : 0));
        }
    }
    
}
