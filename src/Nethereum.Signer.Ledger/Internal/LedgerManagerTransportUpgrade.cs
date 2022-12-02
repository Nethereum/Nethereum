
using Device.Net;
using Ledger.Net;
using Ledger.Net.Requests;
using Ledger.Net.Responses;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq;


//This fixes chunk issues until is upgraded in the main branch it is a copy of the original LedgerManagerTransport
internal class LedgerManagerTransportUpgrade : IHandlesRequest, IDisposable
{
    private readonly SemaphoreSlim _SemaphoreSlim = new SemaphoreSlim(1, 1);

    private bool _IsDisposed;

    public IDevice LedgerHidDevice { get; }

    public LedgerManagerTransportUpgrade(IDevice ledgerHidDevice)
    {
        LedgerHidDevice = ledgerHidDevice;
    }

    private async Task<IEnumerable<byte[]>> WriteRequestAndReadAsync<TRequest>(TRequest request) where TRequest : RequestBase
    {
        List<byte[]> responseData = new List<byte[]>();
        List<byte[]> apduChunks = RequestBaseHelper.ToAPDUChunks(request);
        for (int i = 0; i < apduChunks.Count; i++)
        {

            byte[] array = apduChunks[i];

            if (apduChunks.Count == 1)
            {
                array[2] = request.Argument1;
            }
            ///main change just 0x80 instead of last one marking
            else if (apduChunks.Count > 1)
            {
                if (i == 0)
                {
                    array[2] = 0;
                }
                else
                {
                    array[2] = 128; //Ox80 only
                }
            }

            int packetIndex = 0;
            using (MemoryStream memoryStream = new MemoryStream(array))
            {
                do
                {
                    byte[] requestDataPacket = Nethereum.Signer.Ledger.Internal.Helpers.GetRequestDataPacket(memoryStream, packetIndex);
                    packetIndex++;
                    await LedgerHidDevice.WriteAsync(requestDataPacket);
                }
                while (memoryStream.Position != memoryStream.Length);
            }

            byte[] array2 = await ReadAsync();
            responseData.Add(array2);
            if (ResponseBase.GetReturnCode(array2) != 36864)
            {
                return responseData;
            }
        }

        return responseData;
    }

    private async Task<byte[]> ReadAsync()
    {
        int remaining = 0;
        int packetIndex = 0;
        using MemoryStream response = new MemoryStream();
        do
        {
            byte[] responseDataPacket = Nethereum.Signer.Ledger.Internal.Helpers.GetResponseDataPacket(await LedgerHidDevice.ReadAsync(), packetIndex, ref remaining);
            packetIndex++;
            if (responseDataPacket == null)
            {
                return null;
            }

            response.Write(responseDataPacket, 0, responseDataPacket.Length);
        }
        while (remaining != 0);
        return response.ToArray();
    }

    private async Task<TResponse> SendRequestAsync<TResponse>(RequestBase request) where TResponse : ResponseBase
    {
        await _SemaphoreSlim.WaitAsync();
        try
        {
            IEnumerable<byte[]> source = await WriteRequestAndReadAsync(request);
            return (TResponse)Activator.CreateInstance(typeof(TResponse), source.Last());
        }
        finally
        {
            _SemaphoreSlim.Release();
        }
    }

    public async Task<TResponse> SendRequestAsync<TResponse, TRequest>(TRequest request) where TResponse : ResponseBase where TRequest : RequestBase
    {
        return await SendRequestAsync<TResponse>(request);
    }

    public void Dispose()
    {
        if (!_IsDisposed)
        {
            _IsDisposed = true;
            _SemaphoreSlim.Dispose();
            LedgerHidDevice?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
