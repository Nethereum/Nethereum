#if ANDROID || __ANDROID__
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Hardware.Usb;
using Device.Net;
using Microsoft.Extensions.Logging;
using Java.Nio;

namespace Nethereum.Maui.AndroidUsb;

public sealed class MauiAndroidUsbDevice : IDevice
{
    private readonly ConnectedDeviceDefinition _definition;
    private readonly UsbManager _usbManager;
    private readonly Context _context;
    private readonly Func<global::Android.App.Activity?> _activityProvider;
    private readonly ILogger _logger;

    private UsbDevice? _device;
    private UsbDeviceConnection? _connection;
    private UsbInterface? _interface;
        private UsbEndpoint? _readEndpoint;
        private UsbEndpoint? _writeEndpoint;
    private bool _disposed;

    public MauiAndroidUsbDevice(
        ConnectedDeviceDefinition definition,
        UsbManager usbManager,
        Context context,
        ILogger logger,
        Func<global::Android.App.Activity?> activityProvider)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _usbManager = usbManager ?? throw new ArgumentNullException(nameof(usbManager));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _activityProvider = activityProvider ?? (() => null);
    }

    public ConnectedDeviceDefinition ConnectedDeviceDefinition => _definition;
    public string DeviceId => _definition.DeviceId;
    public bool IsInitialized { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsInitialized) return;

        _device = FindDevice();
        if (_device == null)
        {
            throw new IOException("Trezor device not found.");
        }

        var permissionContext = (Context?)_activityProvider() ?? _context;
        var granted = await UsbPermissionHelper.EnsurePermissionAsync(permissionContext, _usbManager, _device, cancellationToken).ConfigureAwait(false);
        if (!granted)
        {
            throw new IOException("USB permission denied.");
        }

        _connection = _usbManager.OpenDevice(_device) ?? throw new IOException("Failed to open USB device.");

        _interface = SelectInterface(_device) ?? throw new IOException("Suitable USB interface not found.");

        if (!_connection.ClaimInterface(_interface, true))
        {
            throw new IOException("Unable to claim USB interface.");
        }

        (_readEndpoint, _writeEndpoint) = ResolveEndpoints(_interface);
        if (_readEndpoint == null || _writeEndpoint == null)
        {
            throw new IOException("Missing read/write endpoints.");
        }

        IsInitialized = true;
    }

    public async Task<TransferResult> WriteAndReadAsync(byte[] writeBuffer, CancellationToken cancellationToken = default)
    {
        _ = await WriteAsync(writeBuffer, cancellationToken).ConfigureAwait(false);
        return await ReadAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task Flush(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<TransferResult> ReadAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var length = _readEndpoint!.MaxPacketSize > 0 ? _readEndpoint.MaxPacketSize : 64;
        var byteBuffer = ByteBuffer.Allocate(length);
        var request = new UsbRequest();
        if (!request.Initialize(_connection, _readEndpoint))
        {
            throw new IOException("Failed to initialize USB request.");
        }

#pragma warning disable CS0618
        if (!request.Queue(byteBuffer, length))
#pragma warning restore CS0618
        {
            request.Close();
            throw new IOException("Failed to queue USB request.");
        }

        var waitResult = _connection!.RequestWait();
        request.Close();
        if (waitResult == null)
        {
            throw new IOException("USB request wait failed.");
        }

        var data = new byte[length];
        byteBuffer.Rewind();
        for (var i = 0; i < length; i++)
        {
            data[i] = (byte)byteBuffer.Get();
        }

        return Task.FromResult(new TransferResult(data, (uint)data.Length));
    }

    public Task<uint> WriteAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        EnsureInitialized();

        var chunkSize = _writeEndpoint!.MaxPacketSize > 0 ? _writeEndpoint.MaxPacketSize : data.Length;
        var bytesWritten = _connection!.BulkTransfer(_writeEndpoint, data, Math.Min(data.Length, chunkSize), 1000);
        if (bytesWritten < 0)
        {
            throw new IOException("BulkTransfer write failed.");
        }

        return Task.FromResult((uint)bytesWritten);
    }

    public void Close()
    {
        if (_connection == null) return;

        try
        {
            if (_interface != null)
            {
                _connection.ReleaseInterface(_interface);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to release USB interface.");
        }

        try
        {
            _connection.Close();
            _connection.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing USB connection.");
        }

        _connection = null;
        _interface = null;
        _readEndpoint = null;
        _writeEndpoint = null;
        IsInitialized = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Close();
    }

    private UsbDevice? FindDevice()
    {
        var devices = _usbManager.DeviceList?.Values;
        if (devices == null) return null;

        foreach (var device in devices)
        {
            if (_definition.VendorId.HasValue && device.VendorId != _definition.VendorId)
            {
                continue;
            }

            if (_definition.ProductId.HasValue && device.ProductId != _definition.ProductId)
            {
                continue;
            }

            return device;
        }

        return null;
    }

        private static UsbInterface? SelectInterface(UsbDevice device)
    {
            for (var i = 0; i < device.InterfaceCount; i++)
            {
                var usbInterface = device.GetInterface(i);
                var endpoints = ResolveEndpoints(usbInterface);
                if (endpoints.read != null && endpoints.write != null)
                {
                    return usbInterface;
                }
            }

            return null;
        }

        private static bool IsSupportedEndpoint(UsbEndpoint endpoint) =>
            endpoint.Type == UsbAddressing.XferBulk || endpoint.Type == UsbAddressing.XferInterrupt;

        private static (UsbEndpoint? read, UsbEndpoint? write) ResolveEndpoints(UsbInterface usbInterface)
        {
            UsbEndpoint? read = null;
            UsbEndpoint? write = null;

            for (var i = 0; i < usbInterface.EndpointCount; i++)
            {
                var endpoint = usbInterface.GetEndpoint(i);
                if (endpoint == null || !IsSupportedEndpoint(endpoint))
                {
                    continue;
                }

                if (endpoint.Direction == UsbAddressing.In)
                {
                    read ??= endpoint;
                }
                else if (endpoint.Direction == UsbAddressing.Out)
                {
                    write ??= endpoint;
                }
            }

            return (read, write);
        }

    private void EnsureInitialized()
    {
        if (!IsInitialized || _connection == null || _readEndpoint == null || _writeEndpoint == null)
        {
            throw new InvalidOperationException("USB device not initialized.");
        }
    }
}
#endif
