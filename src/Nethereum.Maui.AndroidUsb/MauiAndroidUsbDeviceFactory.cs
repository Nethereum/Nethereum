#if ANDROID || __ANDROID__
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Android.Content;
using Android.Hardware.Usb;
using Device.Net;
using Microsoft.Extensions.Logging;


namespace Nethereum.Maui.AndroidUsb;

public sealed class MauiAndroidUsbDeviceFactory : IDeviceFactory
{
    private readonly UsbManager _usbManager;
    private readonly Context _context;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Func<global::Android.App.Activity?> _activityProvider;
    private readonly IReadOnlyList<FilterDeviceDefinition> _filters;

    public MauiAndroidUsbDeviceFactory(
        UsbManager usbManager,
        Context context,
        ILoggerFactory loggerFactory,
        Func<global::Android.App.Activity?> activityProvider,
        IEnumerable<FilterDeviceDefinition>? filters = null)
    {
        _usbManager = usbManager ?? throw new ArgumentNullException(nameof(usbManager));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _activityProvider = activityProvider ?? (() => null);
        _filters = (filters ?? Array.Empty<FilterDeviceDefinition>()).ToList();
    }

    public Task<IEnumerable<ConnectedDeviceDefinition>> GetConnectedDeviceDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<ConnectedDeviceDefinition>();
        var devices = _usbManager.DeviceList?.Values;
        if (devices == null)
        {
            return Task.FromResult<IEnumerable<ConnectedDeviceDefinition>>(result);
        }

        foreach (var device in devices)
        {
            if (!IsSupported(device))
            {
                continue;
            }

            result.Add(new ConnectedDeviceDefinition(
                deviceId: device.DeviceId.ToString(),
                deviceType: Device.Net.DeviceType.Usb,
                vendorId: (uint)device.VendorId,
                productId: (uint)device.ProductId,
                productName: device.ProductName,
                manufacturer: device.ManufacturerName,
                serialNumber: device.SerialNumber,
                writeBufferSize: device.GetInterface(0)?.GetEndpoint(0)?.MaxPacketSize,
                readBufferSize: device.GetInterface(0)?.GetEndpoint(0)?.MaxPacketSize));
        }

        return Task.FromResult<IEnumerable<ConnectedDeviceDefinition>>(result);
    }

    public Task<IDevice> GetDeviceAsync(ConnectedDeviceDefinition connectedDeviceDefinition, CancellationToken cancellationToken = default)
    {
        var logger = _loggerFactory.CreateLogger<MauiAndroidUsbDevice>();
        IDevice device = new MauiAndroidUsbDevice(connectedDeviceDefinition, _usbManager, _context, logger, _activityProvider);
        return Task.FromResult(device);
    }

    public Task<bool> SupportsDeviceAsync(ConnectedDeviceDefinition connectedDeviceDefinition, CancellationToken cancellationToken = default)
    {
        var supported = _filters.Any(filter =>
            (!filter.VendorId.HasValue || filter.VendorId.Value == connectedDeviceDefinition.VendorId) &&
            (!filter.ProductId.HasValue || filter.ProductId.Value == connectedDeviceDefinition.ProductId));

        return Task.FromResult(supported);
    }

    private bool IsSupported(UsbDevice device)
    {
        foreach (var filter in _filters)
        {
            var vendorMatch = !filter.VendorId.HasValue || filter.VendorId.Value == (uint)device.VendorId;
            var productMatch = !filter.ProductId.HasValue || filter.ProductId.Value == (uint)device.ProductId;
            if (vendorMatch && productMatch)
            {
                return true;
            }
        }
        return false;
    }
}
#endif
