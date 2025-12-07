// NOTE: Adapted from the Trezor.Net project (https://github.com/MelbourneDeveloper/Trezor.Net).
// This copy lives in Nethereum temporarily until upstream is upgraded.

﻿using System;

namespace Trezor.Net.Manager
{
    public class TrezorManagerConnectionEventArgs<TMessageType> : EventArgs
    {
        public TrezorManagerBase<TMessageType> TrezorManager { get; }

        public TrezorManagerConnectionEventArgs(TrezorManagerBase<TMessageType> trezorManager) => TrezorManager = trezorManager;
    }
}
