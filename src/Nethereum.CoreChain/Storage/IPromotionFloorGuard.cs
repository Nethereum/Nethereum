namespace Nethereum.CoreChain.Storage
{
    public interface IPromotionFloorGuard
    {
        ulong? PromotionFloor(ulong currentHead);
    }
}
