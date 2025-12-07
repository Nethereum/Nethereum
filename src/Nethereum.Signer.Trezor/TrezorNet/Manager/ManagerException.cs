// NOTE: Adapted from the Trezor.Net project (https://github.com/MelbourneDeveloper/Trezor.Net).
// This copy lives in Nethereum temporarily until upstream is upgraded.

﻿using System;

namespace Trezor.Net
{
    public class ManagerException : Exception
    {
        public ManagerException(string message) : base(message)
        {

        }

        public ManagerException(string message, Exception innerException) : base(message, innerException)
        {

        }
    }
}
