using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Nethereum.Signer.Bls.Herumi
{
    internal static class BlsNativeLibraryResolver
    {
        private static int _registered;

#pragma warning disable CA2255 // ModuleInitializer in library is intentional for native library resolution
        [ModuleInitializer]
#pragma warning restore CA2255
        internal static void Initialize()
        {
            EnsureRegistered();
        }

        public static void EnsureRegistered()
        {
            if (Interlocked.CompareExchange(ref _registered, 1, 0) != 0)
                return;

            NativeLibrary.SetDllImportResolver(typeof(BlsNativeLibraryResolver).Assembly, ResolveBls);
        }

        private static IntPtr ResolveBls(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != "bls_eth")
                return IntPtr.Zero;

            IntPtr handle;

            if (OperatingSystem.IsAndroid())
            {
                // Android extracts native libs to app's lib directory
                // DllImport("foo") doesn't auto-resolve to "libfoo.so" on Android
                if (NativeLibrary.TryLoad("libbls_eth.so", out handle))
                    return handle;
                if (NativeLibrary.TryLoad("libbls_eth", out handle))
                    return handle;
            }
            else if (OperatingSystem.IsLinux())
            {
                if (NativeLibrary.TryLoad("libbls_eth.so", assembly, searchPath, out handle))
                    return handle;
            }
            else if (OperatingSystem.IsWindows())
            {
                if (NativeLibrary.TryLoad("bls_eth.dll", assembly, searchPath, out handle))
                    return handle;
            }
            else if (OperatingSystem.IsMacOS())
            {
                if (NativeLibrary.TryLoad("libbls_eth.dylib", assembly, searchPath, out handle))
                    return handle;
            }

            return IntPtr.Zero;
        }
    }
}
