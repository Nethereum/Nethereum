// NOTE: Adapted from the Trezor.Net project (https://github.com/MelbourneDeveloper/Trezor.Net).
// This copy lives in Nethereum temporarily until upstream is upgraded.

﻿namespace Trezor.Net
{
    public interface ICoinUtility
    {
        CoinInfo GetCoinInfo(uint coinNumber);
    }
}
