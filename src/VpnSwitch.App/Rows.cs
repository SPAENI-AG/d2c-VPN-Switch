using VpnSwitch.Core;

namespace VpnSwitch.App;

// Zeilenobjekte für die Tabellen. "Tone" steuert die Zeilenfarbe: active | ok | off | bad | "" (siehe MainWindow.xaml).
public sealed record ProviderRow(string Active, string Label, string Version, string Path, string Tone, Provider Source);
public sealed record ServiceRow(string Name, string Display, string State, string StartMode, string Path, string Tone, SvcInfo Source);
public sealed record AdapterRow(string Name, string Description, string Status, string Driver, string Guid, string Tone, AdapterInfo Source);
public sealed record SoftwareRow(string Name, string Version, string Publisher);
