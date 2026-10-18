using System.Diagnostics;
using System.Management;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace VpnSwitch.Core;

/// <summary>Sucht openvpnserv.exe-Varianten, VPN-Dienste, Adapter und installierte VPN-Software.</summary>
public static class Scanner
{
    public const string InteractiveSvc = "OpenVPNServiceInteractive";
    public const string InteractiveKey = @"SYSTEM\CurrentControlSet\Services\OpenVPNServiceInteractive";
    public const string EcatcherAdapterName = "Talk2m-eCatcher";

    private static readonly Regex VpnRx = new(
        @"vpn|openvpn|sophos|talk2m|ecatcher|machinetoservice|device2cloud|wireguard|forti|anyconnect|globalprotect|tosibox|ixon|secomea|mguard|wintun|tap0901|tap-windows|ovpn",
        RegexOptions.IgnoreCase);

    public static readonly Regex EwonRx = new("talk2m|ecatcher", RegexOptions.IgnoreCase);
    public static readonly Regex Talk2mRx = new("talk2m", RegexOptions.IgnoreCase);

    /// <summary>ImagePath -> Pfad ohne Anführungszeichen/Argumente, klein geschrieben.</summary>
    public static string Norm(string? p)
    {
        if (string.IsNullOrEmpty(p)) return "";
        p = p.Trim();
        if (p.StartsWith('"'))
        {
            var e = p.IndexOf('"', 1);
            if (e > 0) p = p.Substring(1, e - 1);
        }
        return p.Trim().ToLowerInvariant();
    }

    public static ScanResult Scan()
    {
        var r = new ScanResult();

        // Aktueller Pfad des Interactive Service
        using (var k = Registry.LocalMachine.OpenSubKey(InteractiveKey))
        {
            if (k != null) r.InteractivePath = Convert.ToString(k.GetValue("ImagePath", "")) ?? "";
        }
        try
        {
            using var sc = new ServiceController(InteractiveSvc);
            r.InteractiveState = ToState(sc.Status);
        }
        catch { r.InteractiveState = St.NotInstalled; }

        // openvpnserv.exe suchen
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var search = Task.Run(() =>
        {
            foreach (var root in new[] {
                Environment.GetEnvironmentVariable("ProgramW6432"),
                Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
            {
                if (!string.IsNullOrEmpty(root) && Directory.Exists(root)) Find(root, "openvpnserv.exe", 4, found);
            }
        });

        // Dienste
        try
        {
            using var s = new ManagementObjectSearcher("SELECT Name, DisplayName, State, StartMode, PathName FROM Win32_Service");
            foreach (ManagementObject o in s.Get())
            {
                string n = Str(o["Name"]), d = Str(o["DisplayName"]), p = Str(o["PathName"]);
                if (VpnRx.IsMatch(n) || VpnRx.IsMatch(d) || VpnRx.IsMatch(p))
                    r.Services.Add(new SvcInfo { Name = n, Display = d, State = WmiState(Str(o["State"])), StartMode = Str(o["StartMode"]), Path = p });
            }
        }
        catch { }

        // Adapter
        try
        {
            using var s = new ManagementObjectSearcher("SELECT NetConnectionID, Name, ServiceName, GUID, NetConnectionStatus, NetEnabled, PNPDeviceID, ConfigManagerErrorCode FROM Win32_NetworkAdapter");
            foreach (ManagementObject o in s.Get())
            {
                string id = Str(o["NetConnectionID"]), name = Str(o["Name"]), svc = Str(o["ServiceName"]);
                if (id.Length == 0) continue;
                if (!(VpnRx.IsMatch(name) || VpnRx.IsMatch(svc) || VpnRx.IsMatch(id))) continue;
                var (status, code) = AdapterStatus(o["NetEnabled"], o["NetConnectionStatus"]);
                int.TryParse(Str(o["ConfigManagerErrorCode"]), out var problem);
                r.Adapters.Add(new AdapterInfo
                {
                    Name = id, Description = name, Guid = Str(o["GUID"]), Status = status, StatusCode = code,
                    ProblemCode = problem, IsTap0901 = svc.Equals("tap0901", StringComparison.OrdinalIgnoreCase),
                    DriverVersion = Str(o["PNPDeviceID"])   // vorläufig: Geräte-ID, unten durch die Treiberversion ersetzt
                });
            }
        }
        catch { }

        FillDriverVersions(r);

        search.Wait();   // Dateisuche lief parallel zu WMI und Registry
        var cur = Norm(r.InteractivePath);
        if (cur.Length > 0 && File.Exists(cur)) found.Add(cur);

        foreach (var f in found)
        {
            var dir = Path.GetDirectoryName(f) ?? "";
            var ovpn = Path.Combine(dir, "openvpn.exe");
            var ver = "";
            if (File.Exists(ovpn))
            {
                try { ver = FileVersionInfo.GetVersionInfo(ovpn).ProductVersion ?? ""; } catch { }
            }
            r.Providers.Add(new Provider { ServPath = f, Label = LabelFor(f), OpenVpnVersion = ver });
        }


        // Installierte Software
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ReadUninstall(RegistryHive.LocalMachine, RegistryView.Registry64, r.Apps, seen);
        ReadUninstall(RegistryHive.LocalMachine, RegistryView.Registry32, r.Apps, seen);
        ReadUninstall(RegistryHive.CurrentUser, RegistryView.Default, r.Apps, seen);

        // eWON / eCatcher (eigener Dienst, kein OpenVPNServiceInteractive)
        var ewSvc = r.Services.FirstOrDefault(x => EwonRx.IsMatch(x.Name + " " + x.Display + " " + x.Path));
        var ewApp = r.Apps.FirstOrDefault(x => EwonRx.IsMatch(x.Name));
        string? ewDir = null;
        foreach (var root in new[] { Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramW6432") })
        {
            if (ewDir != null || string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
            try { ewDir = Directory.GetDirectories(root, "*eCatcher*").FirstOrDefault(); } catch { }
        }
        if (ewSvc != null || ewApp != null || ewDir != null)
        {
            r.Providers.Add(new Provider
            {
                Kind = "ewon",
                Label = "eWON eCatcher (Talk2M)",
                OpenVpnVersion = ewApp?.Version ?? "",
                ServPath = ewSvc != null ? Norm(ewSvc.Path) : (ewDir ?? ""),
                ServiceName = ewSvc?.Name,
                State = ewSvc != null ? ewSvc.State : St.ServiceMissing
            });
        }

        return r;
    }

    /// <summary>Treiberversion je Adapter (WMI Win32_PnPSignedDriver) und Auswertung der tap0901-Adapter.</summary>
    private static void FillDriverVersions(ScanResult r)
    {
        var byDevice = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var s = new ManagementObjectSearcher("SELECT DeviceID, DriverVersion FROM Win32_PnPSignedDriver WHERE DeviceClass='NET'");
            foreach (ManagementObject o in s.Get())
            {
                var id = Str(o["DeviceID"]);
                if (id.Length > 0) byDevice[id] = Str(o["DriverVersion"]);
            }
        }
        catch { }

        foreach (var a in r.Adapters)
        {
            a.DriverVersion = byDevice.TryGetValue(a.DriverVersion, out var v) ? v : "";
            if (a.IsTap0901 && a.DriverVersion.Length > 0 && !r.TapVersions.Contains(a.DriverVersion))
                r.TapVersions.Add(a.DriverVersion);
            if ((a.IsTap0901 || TapRx.IsMatch(a.Description)) && a.ProblemCode != 0)
                r.TapProblems.Add(a.Name);
        }
        r.TapVersions.Sort(StringComparer.Ordinal);

        try
        {
            var sys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\tap0901.sys");
            if (File.Exists(sys))
            {
                var v = FileVersionInfo.GetVersionInfo(sys);
                r.TapSysVersion = $"{v.FileMajorPart}.{v.FileMinorPart}.{v.FileBuildPart}.{v.FilePrivatePart}";
            }
        }
        catch { }
    }

    private static readonly Regex TapRx = new(@"\bTAP\b|tap0901", RegexOptions.IgnoreCase);

    private static void ReadUninstall(RegistryHive hive, RegistryView view, List<SoftwareInfo> list, HashSet<string> seen)
    {
        try
        {
            using var b = RegistryKey.OpenBaseKey(hive, view);
            using var k = b.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (k == null) return;
            foreach (var sub in k.GetSubKeyNames())
            {
                using var e = k.OpenSubKey(sub);
                if (e == null) continue;
                var n = Str(e.GetValue("DisplayName"));
                if (n.Length == 0 || !VpnRx.IsMatch(n)) continue;
                var v = Str(e.GetValue("DisplayVersion"));
                if (!seen.Add(n + "|" + v)) continue;
                list.Add(new SoftwareInfo { Name = n, Version = v, Publisher = Str(e.GetValue("Publisher")) });
            }
        }
        catch { }
    }

    private static void Find(string dir, string file, int depth, HashSet<string> result)
    {
        try
        {
            foreach (var f in Directory.GetFiles(dir, file)) result.Add(f.ToLowerInvariant());
            if (depth <= 0) return;
            foreach (var d in Directory.GetDirectories(dir))
            {
                var name = Path.GetFileName(d);
                if (name.Equals("WindowsApps", StringComparison.OrdinalIgnoreCase)) continue;
                Find(d, file, depth - 1, result);
            }
        }
        catch { }
    }

    private static string LabelFor(string path)
    {
        var p = path.ToLowerInvariant();
        if (p.Contains("sophos")) return "Sophos Connect";
        if (p.Contains(@"\openvpn\bin\")) return "OpenVPN (device2cloud / machinetoservice)";
        if (p.Contains("ecatcher") || p.Contains("talk2m")) return "eCatcher / Talk2M";
        if (p.Contains("openvpn connect")) return "OpenVPN Connect";
        var parts = (Path.GetDirectoryName(path) ?? "").Split('\\');
        return parts.Length >= 3 ? parts[2] : path;
    }

    private static string Str(object? o) => o?.ToString() ?? "";

    public static string ToState(ServiceControllerStatus s) => s switch
    {
        ServiceControllerStatus.Running => St.Running,
        ServiceControllerStatus.Stopped => St.Stopped,
        ServiceControllerStatus.StartPending => St.StartPending,
        ServiceControllerStatus.StopPending => St.StopPending,
        _ => s.ToString()
    };

    /// <summary>WMI-Zustand ("Start Pending") -> Schlüssel ("StartPending").</summary>
    private static string WmiState(string s) => s.Replace(" ", "");

    private static (string Status, int Code) AdapterStatus(object? enabled, object? status)
    {
        if (enabled is bool b && !b) return (St.Disabled, -1);
        var st = -1;
        if (status != null) int.TryParse(status.ToString(), out st);
        return st switch
        {
            2 => (St.Connected, st),
            1 => (St.Connecting, st),
            7 or 0 => (St.Disconnected, st),
            _ => (St.Unknown, st)
        };
    }
}
