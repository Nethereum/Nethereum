namespace Nethereum.Uniswap.UniversalRouter.Commands
{
    public interface IUniversalRouterCommand
    {
        byte CommandType { get; set; }

        byte GetFullCommandType();
    }
}