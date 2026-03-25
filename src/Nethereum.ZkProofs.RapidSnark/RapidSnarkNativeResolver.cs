using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Nethereum.ZkProofs.RapidSnark
{
    internal static class RapidSnarkNativeResolver
    {
        private static int _registered;

#pragma warning disable CA2255
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

            NativeLibrary.SetDllImportResolver(typeof(RapidSnarkNativeResolver).Assembly, ResolveRapidSnark);
        }

        private static IntPtr ResolveRapidSnark(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != RapidSnarkBindings.LibName)
                return IntPtr.Zero;

            IntPtr handle;

            if (OperatingSystem.IsAndroid())
            {
                if (NativeLibrary.TryLoad("librapidsnark.so", out handle))
                    return handle;
                if (NativeLibrary.TryLoad("librapidsnark", out handle))
                    return handle;
            }
            else if (OperatingSystem.IsLinux())
            {
                if (NativeLibrary.TryLoad("librapidsnark.so", assembly, searchPath, out handle))
                    return handle;
            }
            else if (OperatingSystem.IsWindows())
            {
                if (NativeLibrary.TryLoad("rapidsnark.dll", assembly, searchPath, out handle))
                    return handle;
            }
            else if (OperatingSystem.IsMacOS())
            {
                if (NativeLibrary.TryLoad("librapidsnark.dylib", assembly, searchPath, out handle))
                    return handle;
            }

            return IntPtr.Zero;
        }
    }
}
