using System;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;

namespace Nethereum.JsonRpc.IpcClient
{
    // Licensed to the .NET Foundation under one or more agreements.
    // The .NET Foundation licenses this file to you under the MIT license.
    // See https://github.com/dotnet/corefx/blob/master/LICENSE.TXT

    //This is part of Dotnet Core, waiting to be released externally. NamedPipesStream uses it but it does not allow for paths.

    public sealed class UnixDomainSocketEndPoint : EndPoint
    {
        private const AddressFamily EndPointAddressFamily = AddressFamily.Unix;

        private static readonly Encoding PathEncoding = Encoding.UTF8;
        private static readonly int NativePathOffset = 2; // = offsetof(struct sockaddr_un, sun_path). It's the same on Linux and OSX
        private static readonly int NativePathLength = 91; // sockaddr_un.sun_path at http://pubs.opengroup.org/onlinepubs/9699919799/basedefs/sys_un.h.html, -1 for terminator
        private static readonly int NativeAddressSize = NativePathOffset + NativePathLength;

        private readonly string _path;
        private readonly byte[] _encodedPath;

        public UnixDomainSocketEndPoint(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            _path = path;
            _encodedPath = PathEncoding.GetBytes(_path);

            if (path.Length == 0 || _encodedPath.Length > NativePathLength)
            {
                throw new ArgumentOutOfRangeException(nameof(path));
            }
        }

        internal UnixDomainSocketEndPoint(SocketAddress socketAddress)
        {
            if (socketAddress == null)
            {
                throw new ArgumentNullException(nameof(socketAddress));
            }

            if (socketAddress.Family != EndPointAddressFamily ||
                socketAddress.Size > NativeAddressSize)
            {
                throw new ArgumentOutOfRangeException(nameof(socketAddress));
            }

            if (socketAddress.Size > NativePathOffset)
            {
                _encodedPath = new byte[socketAddress.Size - NativePathOffset];
                for (int i = 0; i < _encodedPath.Length; i++)
                {
                    _encodedPath[i] = socketAddress[NativePathOffset + i];
                }

                _path = PathEncoding.GetString(_encodedPath, 0, _encodedPath.Length);
            }
            else
            {
                _encodedPath = new byte[0];
                _path = string.Empty;
            }
        }

        public override SocketAddress Serialize()
        {
            var result = new SocketAddress(AddressFamily.Unix, NativeAddressSize);
            Debug.Assert(_encodedPath.Length + NativePathOffset <= result.Size, "Expected path to fit in address");

            for (int index = 0; index < _encodedPath.Length; index++)
            {
                result[NativePathOffset + index] = _encodedPath[index];
            }
            result[NativePathOffset + _encodedPath.Length] = 0; // path must be null-terminated

            return result;
        }

        public override EndPoint Create(SocketAddress socketAddress) => new UnixDomainSocketEndPoint(socketAddress);

        public override AddressFamily AddressFamily => EndPointAddressFamily;

        public override string ToString() => _path;
    }
}
