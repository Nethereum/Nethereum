using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Nethereum.CircomWitnessCalc
{
    internal static class CircomWitnessCalcNativeResolver
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

            NativeLibrary.SetDllImportResolver(typeof(CircomWitnessCalcNativeResolver).Assembly, Resolve);
        }

        private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != CircomWitnessCalcBindings.LibName)
                return IntPtr.Zero;

            IntPtr handle;

            if (OperatingSystem.IsAndroid())
            {
                if (NativeLibrary.TryLoad("libcircom_witnesscalc.so", out handle))
                    return handle;
                if (NativeLibrary.TryLoad("libcircom_witnesscalc", out handle))
                    return handle;
            }
            else if (OperatingSystem.IsLinux())
            {
                if (NativeLibrary.TryLoad("libcircom_witnesscalc.so", assembly, searchPath, out handle))
                    return handle;
            }
            else if (OperatingSystem.IsWindows())
            {
                if (NativeLibrary.TryLoad("circom_witnesscalc.dll", assembly, searchPath, out handle))
                    return handle;
            }
            else if (OperatingSystem.IsMacOS())
            {
                if (NativeLibrary.TryLoad("libcircom_witnesscalc.dylib", assembly, searchPath, out handle))
                    return handle;
            }

            return IntPtr.Zero;
        }
    }
}
