using System.Windows;
using System.Windows.Controls;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;
using Windows.Security.Credentials;
using ClaxeDesktop.Core;
namespace ClaxeDesktop.App;

/// <summary>Wi-Fi networks through the documented WiFiAdapter API. Passwords go straight to Windows, which stores the network profile; Noxelyra keeps nothing.</summary>
internal static class WifiService
{
    internal sealed record Network(string Name, byte Bars, bool Secured, bool Enterprise, bool Connected, WiFiAdapter Adapter, WiFiAvailableNetwork Raw);
    internal const string LocationHint = "Windows ağ listesini vermedi. Wi-Fi taraması için Ayarlar → Gizlilik ve güvenlik → Konum → Masaüstü uygulamalarının konuma erişmesine izin ver açık olmalı.";
    /// <summary>The networks in range, strongest first; Problem says why there are none when Windows refuses.</summary>
    internal static async Task<(IReadOnlyList<Network> Items, string? Problem, bool Location)> ScanAsync()
    {
        try
        {
            if (await WiFiAdapter.RequestAccessAsync() != WiFiAccessStatus.Allowed) return ([], LocationHint, true);
            var adapters = await WiFiAdapter.FindAllAdaptersAsync();
            if (adapters.Count == 0) return ([], "Bu bilgisayarda Wi-Fi bağdaştırıcısı yok.", false);
            var adapter = adapters[0];
            await adapter.ScanAsync();
            string? connected = null;
            if (await adapter.NetworkAdapter.GetConnectedProfileAsync() is { } profile) connected = profile.IsWlanConnectionProfile ? profile.WlanConnectionProfileDetails.GetConnectedSsid() : null;
            var items = adapter.NetworkReport.AvailableNetworks.Where(n => n.Ssid.Length > 0).GroupBy(n => n.Ssid).Select(g => g.OrderByDescending(n => n.SignalBars).First())
                .Select(n =>
                {
                    var kind = n.SecuritySettings.NetworkAuthenticationType;
                    return new Network(n.Ssid, n.SignalBars, kind is not (NetworkAuthenticationType.Open80211 or NetworkAuthenticationType.None), kind is NetworkAuthenticationType.Rsna or NetworkAuthenticationType.Wpa, n.Ssid == connected, adapter, n);
                })
                .OrderByDescending(n => n.Connected).ThenByDescending(n => n.Bars).ThenBy(n => n.Name, StringComparer.CurrentCultureIgnoreCase).Take(30).ToArray();
            return items.Length == 0 ? ([], LocationHint, true) : (items, null, false);
        }
        catch (UnauthorizedAccessException) { return ([], LocationHint, true); }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException) { return ([], "Wi-Fi ağları okunamadı: " + ex.Message, false); }
    }
    /// <summary>Connects; a saved network needs no password. Returns the outcome so the caller can ask for a password.</summary>
    internal static async Task<WiFiConnectionStatus> ConnectAsync(Network network, string? password)
    {
        try
        {
            var result = password is null
                ? await network.Adapter.ConnectAsync(network.Raw, WiFiReconnectionKind.Automatic)
                : await network.Adapter.ConnectAsync(network.Raw, WiFiReconnectionKind.Automatic, new PasswordCredential { Password = password });
            return result.ConnectionStatus;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException or ArgumentException) { return WiFiConnectionStatus.UnspecifiedFailure; }
    }
    internal static string Message(WiFiConnectionStatus status, string name) => status switch
    {
        WiFiConnectionStatus.Success => name + " ağına bağlandı.", WiFiConnectionStatus.InvalidCredential => "Parola yanlış.", WiFiConnectionStatus.AccessRevoked => "Bağlantı izni yok.",
        WiFiConnectionStatus.NetworkNotAvailable => name + " artık görünmüyor.", WiFiConnectionStatus.Timeout => "Bağlantı zaman aşımına uğradı.", WiFiConnectionStatus.UnsupportedAuthenticationProtocol => "Bu ağ Windows ayarlarından bağlanmalı.",
        _ => "Bağlanılamadı.",
    };
}

/// <summary>Paired Bluetooth devices. Windows has no documented call to connect a paired device, so connecting stays in Windows' own settings.</summary>
internal static class BluetoothList
{
    internal sealed record Device(string Name, bool Connected);
    internal static async Task<IReadOnlyList<Device>> PairedAsync()
    {
        var classic = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
        var low = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true));
        var connected = (await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected))).Concat(await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected))).Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return classic.Concat(low).Select(d => d.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).Select(n => new Device(n, connected.Contains(n))).OrderByDescending(d => d.Connected).ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
}

/// <summary>Asks for a Wi-Fi password in a real window, because the island card may sit over a game without keyboard focus.</summary>
internal sealed class WifiPasswordWindow : Window
{
    internal WifiPasswordWindow(DesktopController controller, WifiService.Network network, Action<string> report)
    {
        Title = "Noxelyra · Wi-Fi"; Width = 420; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen; Topmost = true;
        SetResourceReference(BackgroundProperty, "Canvas"); SetResourceReference(ForegroundProperty, "Text"); Glass.PrepareFramed(this, controller.ViewModel.Settings);
        var root = new StackPanel { Margin = new(24) }; Content = root;
        root.Children.Add(new TextBlock { Text = network.Name, FontSize = 18, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        var note = BarPopup.Muted("Parola doğrudan Windows'a verilir; Noxelyra kaydetmez.", 12); note.Margin = new(0, 4, 0, 12); root.Children.Add(note);
        var box = new PasswordBox { Padding = new(8, 6, 8, 6), MaxLength = 63 }; System.Windows.Automation.AutomationProperties.SetName(box, "Wi-Fi parolası"); root.Children.Add(box);
        var status = BarPopup.Muted("", 12); status.Margin = new(0, 8, 0, 0); root.Children.Add(status);
        var connect = new Button { Content = "Bağlan", Padding = new(16, 7, 16, 7), Margin = new(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, IsDefault = true }; root.Children.Add(connect);
        connect.Click += async (_, _) =>
        {
            connect.IsEnabled = false; status.Text = "Bağlanıyor…";
            var result = await WifiService.ConnectAsync(network, box.Password);
            var message = WifiService.Message(result, network.Name); report(message); status.Text = message; connect.IsEnabled = true;
            if (result == WiFiConnectionStatus.Success) Close(); else { box.Clear(); box.Focus(); }
        };
        PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) { Close(); e.Handled = true; } };
        Loaded += (_, _) => { Activate(); box.Focus(); };
    }
}
