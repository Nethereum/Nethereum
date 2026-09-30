using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Nethereum.WebAuthn.Windows
{
    internal sealed class NativeAllocationScope : IDisposable
    {
        private readonly List<IntPtr> _allocations = new List<IntPtr>();

        public IntPtr AllocBytes(byte[]? data)
        {
            if (data == null || data.Length == 0)
            {
                return IntPtr.Zero;
            }

            var ptr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, ptr, data.Length);
            _allocations.Add(ptr);
            return ptr;
        }

        public IntPtr AllocStruct<T>(T value) where T : struct
        {
            var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
            Marshal.StructureToPtr(value, ptr, fDeleteOld: false);
            _allocations.Add(ptr);
            return ptr;
        }

        public void Dispose()
        {
            foreach (var ptr in _allocations)
            {
                Marshal.FreeHGlobal(ptr);
            }
            _allocations.Clear();
        }
    }
}
