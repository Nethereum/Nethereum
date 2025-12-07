// NOTE: Adapted from the Trezor.Net project (https://github.com/MelbourneDeveloper/Trezor.Net).
// This copy lives in Nethereum temporarily until upstream is upgraded.

﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Trezor.Net
{
    public class ByteBuffer
    {
        #region Fields
        private readonly List<byte> _Bytes;

        #endregion

        #region Public Properties
        public int Position { get; private set; }
        #endregion

        #region Constructor
        public ByteBuffer(int size) => _Bytes = new byte[size].ToList();
        #endregion

        #region Public Methods


        public void Put(byte theByte)
        {
            _Bytes[Position] = theByte;
            Position++;
        }

        public void Put(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            foreach (var thebyte in bytes)
            {
                Put(thebyte);
            }
        }

        public byte[] ToArray() => _Bytes.ToArray();

        public void Put(byte[] bytes, int startIndex, int Length)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));

            for (var i = startIndex; i < Length; i++)
            {
                Put(bytes[i]);
            }
        }
        #endregion
    }
}
