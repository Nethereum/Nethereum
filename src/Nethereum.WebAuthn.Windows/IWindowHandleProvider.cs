using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Nethereum.WebAuthn.Windows
{
    public interface IWindowHandleProvider
    {
        IntPtr GetWindowHandle();
    }

    [SupportedOSPlatform("windows")]
    public class ForegroundWindowHandleProvider : IWindowHandleProvider
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public IntPtr GetWindowHandle() => GetForegroundWindow();
    }
}
