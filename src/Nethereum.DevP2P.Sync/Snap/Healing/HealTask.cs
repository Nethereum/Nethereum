using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.DevP2P.Sync.Snap.Healing
{
    internal readonly record struct HealTask(bool IsStorage, byte[]? AccountHash, byte[] NibblePath, byte[] ExpectedHash)
    {
        internal static string LocKey(in HealTask t)
            => t.IsStorage
                ? "s:" + t.AccountHash.ToHex() + ":" + t.NibblePath.ToHex()
                : "a:" + t.NibblePath.ToHex();
    }
}
