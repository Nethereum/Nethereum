// NOTE: Adapted from the Trezor.Net project (https://github.com/MelbourneDeveloper/Trezor.Net).
// This copy lives in Nethereum temporarily until upstream is upgraded.

﻿using System;

namespace Trezor.Net
{
    public class NotSupportedException : Exception
    {
        public NotSupportedException(string message) : base(message)
        {

        }

        public NotSupportedException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}
