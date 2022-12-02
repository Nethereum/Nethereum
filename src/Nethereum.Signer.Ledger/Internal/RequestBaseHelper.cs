using Ledger.Net.Requests;
using System.Collections.Generic;
using System;
//This fixes chunk issues until is upgraded in the main branch
internal class RequestBaseHelper
{

    public static byte[] GetNextApduCommand(RequestBase request, ref int offset)
    {
        var isFirst = offset == 0;

        var maxChunkSize = isFirst ? 150 - 1 - 4 : 150;
        var chunkSize =
            offset + maxChunkSize > request.Data.Length
            ? request.Data.Length - offset
            : maxChunkSize;

        var buffer = new byte[5 + chunkSize];
        buffer[0] = request.Cla;
        buffer[1] = request.Ins;
        //buffer[2] will be filled in later when we know how many chunks there are
        buffer[3] = request.Argument2;
        buffer[4] = (byte)chunkSize;
        Array.Copy(request.Data, offset, buffer, 5, chunkSize);
        offset += chunkSize;
        return buffer;
    }

    public static List<byte[]> ToAPDUChunks(RequestBase request)
    {
        var offset = 0;

        if (request.Data.Length > 0)
        {
            var retVal = new List<byte[]>();

            while (offset < request.Data.Length - 1)
            {
                retVal.Add(GetNextApduCommand(request, ref offset));
            }

            return retVal;
        }
        else
        {
            return new List<byte[]> { GetNextApduCommand(request, ref offset) };
        }
    }
}
