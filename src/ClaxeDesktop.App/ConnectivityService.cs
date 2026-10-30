using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using Windows.Networking.Connectivity;
namespace ClaxeDesktop.App;

/// <summary>What the bar's status icons show. Null means Windows did not report it (no adapter, no battery, no permission).</summary>
internal sealed record Connectivity(string Kind, string? Name, int? Bars, bool Internet, bool? WifiOn, bool? BluetoothOn, IReadOnlyList<string> BluetoothDevices, int? Battery, bool Charging)
{
    internal static readonly Connectivity Unknown = new("none", null, null, false, null, null, [], null, false);
}

/// <summary>
/// Network, Wi-Fi/Bluetooth radios, connected Bluetooth devices and battery through documented WinRT and .NET APIs only.
/// The network name is the connection profile name (usually the Wi-Fi name); it needs no location permission.
/// </summary>
internal static class ConnectivityService
{
    private static int reads;
    private static IReadOnlyList<string> devices = [];
    internal static Connectivity Last { get; private set; } = Connectivity.Unknown;
    internal static event Action? Changed;
    internal static async Task<Connectivity> ReadAsync()
    {
        string kind = "none"; string? name = null; int? bars = null; var internet = false;
        try
        {
            if (NetworkInformation.GetInternetConnectionProfile() is { } profile)
            {
                var level = profile.GetNetworkConnectivityLevel();
                if (level != NetworkConnectivityLevel.None)
                {
                    internet = level == NetworkConnectivityLevel.InternetAccess;
                    kind = profile.IsWlanConnectionProfile ? "wifi" : profile.IsWwanConnectionProfile ? "cellular" : "ethernet";
                    name = profile.ProfileName; bars = profile.GetSignalBars() is { } signal ? signal : null;
                }
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException) { }
        bool? wifi = null, bluetooth = null;
        try
        {
            foreach (var radio in await Radio.GetRadiosAsync())
            {
                bool? on = radio.State switch { RadioState.On => true, RadioState.Off or RadioState.Disabled => false, _ => null };
                if (radio.Kind == RadioKind.WiFi) wifi ??= on; else if (radio.Kind == RadioKind.Bluetooth) bluetooth ??= on;
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException) { }
        // Device queries cost more than the rest, so the connected list refreshes on every third read.
        if (bluetooth != true) devices = [];
        else if (reads++ % 3 == 0)
        {
            try
            {
                var classic = await DeviceInformation.FindAllAsync(BluetoothDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected));
                var low = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromConnectionStatus(BluetoothConnectionStatus.Connected));
                devices = classic.Concat(low).Select(d => d.Name).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException) { devices = []; }
        }
        int? battery = null; var charging = false;
        var power = SystemInformation.PowerStatus;
        if (!power.BatteryChargeStatus.HasFlag(BatteryChargeStatus.NoSystemBattery) && power.BatteryChargeStatus != BatteryChargeStatus.Unknown && power.BatteryLifePercent is >= 0 and <= 1)
        { battery = (int)Math.Round(power.BatteryLifePercent * 100); charging = power.PowerLineStatus == PowerLineStatus.Online; }
        var next = new Connectivity(kind, name, bars, internet, wifi, bluetooth, devices, battery, charging);
        var same = next with { BluetoothDevices = Last.BluetoothDevices } == Last && next.BluetoothDevices.SequenceEqual(Last.BluetoothDevices);
        if (!same) { Last = next; Changed?.Invoke(); }
        return Last;
    }
    /// <summary>Switches a radio the way the Windows quick settings do; Windows may refuse (policy, airplane mode, driver).</summary>
    internal static async Task<string> SetRadioAsync(RadioKind kind, bool on)
    {
        var label = kind == RadioKind.Bluetooth ? "Bluetooth" : "Wi-Fi";
        try
        {
            if (await Radio.RequestAccessAsync() != RadioAccessStatus.Allowed) return $"Windows {label} ayarını değiştirmeye izin vermedi; Windows ayarlarından değiştirebilirsin.";
            var radio = (await Radio.GetRadiosAsync()).FirstOrDefault(r => r.Kind == kind);
            if (radio is null) return $"Bu bilgisayarda {label} bulunamadı.";
            if (radio.State == RadioState.Disabled) return $"{label} şu anda Windows tarafından devre dışı (uçak modu veya sürücü).";
            var result = await radio.SetStateAsync(on ? RadioState.On : RadioState.Off);
            await ReadAsync();
            return result == RadioAccessStatus.Allowed ? $"{label} {(on ? "açıldı" : "kapatıldı")}." : $"Windows {label} ayarını değiştirmeye izin vermedi.";
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException) { return $"{label} değiştirilemedi."; }
    }
}
