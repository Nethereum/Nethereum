using System;

namespace Nethereum.WebAuthn.Windows
{
    public class WebAuthnNativeException : Exception
    {
        public WebAuthnNativeException(string message, int hresult) : base(message)
        {
            HResult = hresult;
        }
    }
}
