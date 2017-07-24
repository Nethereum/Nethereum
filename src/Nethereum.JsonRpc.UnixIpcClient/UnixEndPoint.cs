using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Nethereum.JsonRpc.UnixIpcClient
{
    public class UnixEndPoint : EndPoint
    {
        string filename;

        public UnixEndPoint(string filename)
        {
            if (filename == null)
                throw new ArgumentNullException("filename");

            if (filename == "")
                throw new ArgumentException("Cannot be empty.", "filename");
            this.filename = filename;
        }

        public string Filename
        {
            get
            {
                return (filename);
            }
            set
            {
                filename = value;
            }
        }

        public override AddressFamily AddressFamily
        {
            get { return AddressFamily.Unix; }
        }

        public override EndPoint Create(SocketAddress socketAddress)
        {
            /*
             * Should also check this
             */
            int addr = (int) AddressFamily.Unix;
            if (socketAddress [0] != (addr & 0xFF))
                throw new ArgumentException ("socketAddress is not a unix socket address.");
            if (socketAddress [1] != ((addr & 0xFF00) >> 8))
                throw new ArgumentException ("socketAddress is not a unix socket address.");
             

            if (socketAddress.Size == 2)
            {
                // Empty filename.
                // Probably from RemoteEndPoint which on linux does not return the file name.
                UnixEndPoint uep = new UnixEndPoint("a");
                uep.filename = "";
                return uep;
            }
            int size = socketAddress.Size - 2;
            byte[] bytes = new byte[size];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = socketAddress[i + 2];
                // There may be junk after the null terminator, so ignore it all.
                if (bytes[i] == 0)
                {
                    size = i;
                    break;
                }
            }

            string name = Encoding.UTF8.GetString(bytes, 0, size);
            return new UnixEndPoint(name);
        }

        public override SocketAddress Serialize()
        {
            byte[] bytes = Encoding.UTF8.GetBytes(filename);
            SocketAddress sa = new SocketAddress(AddressFamily, 2 + bytes.Length + 1);
            // sa [0] -> family low byte, sa [1] -> family high byte
            for (int i = 0; i < bytes.Length; i++)
                sa[2 + i] = bytes[i];

            //NULL suffix for non-abstract path
            sa[2 + bytes.Length] = 0;

            return sa;
        }

        public override string ToString()
        {
            return (filename);
        }

        public override int GetHashCode()
        {
            return filename.GetHashCode();
        }

        public override bool Equals(object o)
        {
            UnixEndPoint other = o as UnixEndPoint;
            if (other == null)
                return false;

            return (other.filename == filename);
        }
    }
}
