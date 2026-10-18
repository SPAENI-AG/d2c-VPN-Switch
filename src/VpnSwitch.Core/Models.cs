namespace VpnSwitch.Core;

/// <summary>Zustandsschlüssel (Anzeige über <see cref="Loc.StateText"/>, Vergleiche im Code nur mit diesen Konstanten).</summary>
public static class St
{
    public const string Running = "Running";
    public const string Stopped = "Stopped";
    public const string StartPending = "StartPending";
    public const string StopPending = "StopPending";
    public const string NotInstalled = "NotInstalled";
    public const string ServiceMissing = "ServiceMissing";
    public const string Disabled = "Disabled";
    public const string Connected = "Connected";
    public const string Connecting = "Connecting";
    public const string Disconnected = "Disconnected";
    public const string Unknown = "Unknown";
}

public sealed class Provider
{
    public string ServPath { get; set; } = "";   // openvpnserv.exe
    public string Label { get; set; } = "";
    public string OpenVpnVersion { get; set; } = "";
    public string Kind { get; set; } = "ovpn";   // "ovpn" = OpenVPNServiceInteractive, "ewon" = eCatcher
    public string? ServiceName { get; set; }
    public string State { get; set; } = "";
}

public sealed class SvcInfo
{
    public string Name { get; set; } = "";
    public string Display { get; set; } = "";
    public string State { get; set; } = "";
    public string StartMode { get; set; } = "";
    public string Path { get; set; } = "";
}

public sealed class AdapterInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Guid { get; set; } = "";
    public string Status { get; set; } = "";
    public int StatusCode { get; set; }
    public string DriverVersion { get; set; } = "";
    /// <summary>Win32_NetworkAdapter.ConfigManagerErrorCode (0 = Gerät läuft, sonst Problem im Geräte-Manager).</summary>
    public int ProblemCode { get; set; }
    /// <summary>Adapter nutzt den gemeinsamen TAP-Treiber tap0901.sys.</summary>
    public bool IsTap0901 { get; set; }
}

public sealed class SoftwareInfo
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Publisher { get; set; } = "";
}

public sealed class ScanResult
{
    public List<Provider> Providers { get; } = [];
    public List<SvcInfo> Services { get; } = [];
    public List<AdapterInfo> Adapters { get; } = [];
    public List<SoftwareInfo> Apps { get; } = [];
    public string InteractivePath { get; set; } = "";
    public string InteractiveState { get; set; } = "";
    /// <summary>Dateiversion von %SystemRoot%\System32\drivers\tap0901.sys ("" wenn nicht vorhanden).</summary>
    public string TapSysVersion { get; set; } = "";
    /// <summary>Verschiedene Treiberversionen der tap0901-Adapter (mehr als eine = Konflikt).</summary>
    public List<string> TapVersions { get; } = [];
    /// <summary>Namen von TAP-Adaptern, die im Geräte-Manager ein Problem melden.</summary>
    public List<string> TapProblems { get; } = [];
}
