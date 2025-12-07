// NOTE: Adapted from the Trezor.Net project (https://github.com/MelbourneDeveloper/Trezor.Net).
// This copy lives in Nethereum temporarily until upstream is upgraded.

﻿using Device.Net;
using Microsoft.Extensions.Logging;
using hw.trezor.messages;


namespace Trezor.Net.Manager
{
    public class TrezorManagerBroker : TrezorManagerBrokerBase<TrezorManager, MessageType>
    {
        #region Constructor
        public TrezorManagerBroker(
            EnterPinArgs enterPinArgs,
            EnterPinArgs enterPassphraseArgs,
            int? pollInterval,
            IDeviceFactory deviceFactory,
            ICoinUtility coinUtility = null,
            ILoggerFactory loggerFactory = null
            ) : base(
                enterPinArgs,
                enterPassphraseArgs,
                pollInterval,
                deviceFactory,
                coinUtility,
                loggerFactory)
        {
        }
        #endregion

        #region Protected Overrides
        protected override TrezorManager CreateTrezorManager(IDevice device) => new TrezorManager(EnterPinArgs, EnterPassphraseArgs, device, LoggerFactory.CreateLogger<TrezorManager>());
        #endregion
    }
}
