using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Nethereum.Signer.Trezor.Maui.Services;
using Nethereum.Maui.AndroidUsb;
#if ANDROID || __ANDROID__
using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Nethereum.Signer.Trezor.Maui.Platforms.Android;
#endif

namespace Nethereum.Signer.Trezor.Maui;

public partial class MainPage : ContentPage
{
    private readonly TrezorSigningService _signingService;

    public MainPage(TrezorSigningService signingService)
    {
        _signingService = signingService;
        InitializeComponent();
    }

    private async void OnSignClicked(object sender, EventArgs e)
    {
        var message = MessageEntry.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            await DisplayAlert("Validation", "Please enter a message to sign.", "OK");
            return;
        }

        try
        {
            SignButton.IsEnabled = false;
            SignatureLabel.Text = string.Empty;
            StatusLabel.Text = "Waiting for Trezor...";

            var signature = await _signingService.SignMessageAsync(message);

            StatusLabel.Text = "Signature generated successfully.";
            SignatureLabel.Text = signature;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            SignatureLabel.Text = string.Empty;
        }
        finally
        {
            SignButton.IsEnabled = true;
        }
    }

    private async void OnCheckDeviceClicked(object sender, EventArgs e)
    {
        try
        {
            CheckButton.IsEnabled = false;
            StatusLabel.Text = "Checking for Trezor...";
            var detected = await _signingService.DetectDeviceAsync();
            StatusLabel.Text = detected ? "Trezor detected." : "No Trezor detected (timeout).";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private async void OnRawUsbTestClicked(object sender, EventArgs e)
    {
#if ANDROID || __ANDROID__
        try
        {
            RawUsbButton.IsEnabled = false;
            StatusLabel.Text = "Running raw USB test...";

            var activity = Platform.CurrentActivity;
            if (activity == null)
            {
                StatusLabel.Text = "No current activity.";
                return;
            }

            var usbManager = (UsbManager?)activity.GetSystemService(Context.UsbService);
            if (usbManager == null)
            {
                StatusLabel.Text = "UsbManager unavailable.";
                return;
            }

            var devices = usbManager.DeviceList?.Values;
            if (devices == null || devices.Count == 0)
            {
                StatusLabel.Text = "No USB devices detected.";
                return;
            }

            var device = devices.First();
            UsbInterface? selectedInterface = null;
            UsbEndpoint? outEndpoint = null;
            UsbEndpoint? inEndpoint = null;

            static bool IsSupportedEndpoint(UsbEndpoint endpoint) =>
                endpoint.Type == UsbAddressing.XferBulk || endpoint.Type == UsbAddressing.XferInterrupt;

            var diagnostics = $"Interfaces: {device.InterfaceCount}\n";
            for (var i = 0; i < device.InterfaceCount; i++)
            {
                var iface = device.GetInterface(i);
                if (iface == null)
                {
                    diagnostics += $"Interface {i}: null\n";
                    continue;
                }

                diagnostics += $"Interface {i}: Endpoints {iface.EndpointCount}\n";
                UsbEndpoint? candidateOut = null;
                UsbEndpoint? candidateIn = null;
                for (var j = 0; j < iface.EndpointCount; j++)
                {
                    var endpoint = iface.GetEndpoint(j);
                    if (endpoint == null) continue;
                    diagnostics += $"  Endpoint {j}: Type={endpoint.Type} Dir={endpoint.Direction} MaxPacket={endpoint.MaxPacketSize}\n";
                    if (!IsSupportedEndpoint(endpoint)) continue;
                    if (endpoint.Direction == UsbAddressing.Out)
                    {
                        candidateOut ??= endpoint;
                    }
                    else if (endpoint.Direction == UsbAddressing.In)
                    {
                        candidateIn ??= endpoint;
                    }
                }

                if (candidateOut != null)
                {
                    selectedInterface = iface;
                    outEndpoint = candidateOut;
                    inEndpoint = candidateIn;
                    break;
                }
            }

            if (selectedInterface == null || outEndpoint == null)
            {
                StatusLabel.Text = $"Bulk OUT endpoint not found.\n{diagnostics}";
                return;
            }

            if (!usbManager.HasPermission(device))
            {
                var granted = await UsbPermissionHelper.EnsurePermissionAsync(activity, usbManager, device, default).ConfigureAwait(false);
                if (!granted)
                {
                    StatusLabel.Text = "Permission denied.";
                    return;
                }
            }

            using var connection = usbManager.OpenDevice(device);
            if (connection == null)
            {
                StatusLabel.Text = "Failed to open device.";
                return;
            }

            if (!connection.ClaimInterface(selectedInterface, true))
            {
                StatusLabel.Text = "Failed to claim interface.";
                return;
            }

            var ping = new byte[outEndpoint.MaxPacketSize > 0 ? outEndpoint.MaxPacketSize : 64];
            var resultOut = connection.BulkTransfer(outEndpoint, ping, ping.Length, 1000);

            string inStatus;
            if (inEndpoint != null)
            {
                var readBuffer = new byte[inEndpoint.MaxPacketSize > 0 ? inEndpoint.MaxPacketSize : 64];
                var resultIn = connection.BulkTransfer(inEndpoint, readBuffer, readBuffer.Length, 1000);
                inStatus = resultIn >= 0 ? $"Read {resultIn} bytes" : "Read failed";
            }
            else
            {
                inStatus = "No IN endpoint";
            }

            StatusLabel.Text = resultOut >= 0
                ? $"Write returned {resultOut}. {inStatus}."
                : "BulkTransfer write failed.";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Raw USB test error: {ex.Message}";
        }
        finally
        {
            RawUsbButton.IsEnabled = true;
        }
#else
        StatusLabel.Text = "Raw USB test is Android-only.";
#endif
    }

}
