// Einfacher Testlauf ohne Framework: dotnet run --project tests/VpnSwitch.Tests -c Release
// Prüft Übersetzungen, Hilfe-Anker und Hilfsfunktionen der Kernlogik. Exitcode 1 bei Fehlern.
using System.Text.RegularExpressions;
using VpnSwitch.Core;

var failures = new List<string>();
void Check(bool ok, string msg) { if (!ok) failures.Add(msg); }

// Repository-Wurzel finden
var root = AppContext.BaseDirectory;
while (root != null && !File.Exists(Path.Combine(root, "Directory.Build.props"))) root = Path.GetDirectoryName(root);
if (root == null) { Console.Error.WriteLine("Repository-Wurzel nicht gefunden"); return 1; }

// 1. Übersetzungen: Platzhalter in DE und EN identisch, keine leeren Texte
static string[] Placeholders(string s) => Regex.Matches(s, @"\{\d+\}").Select(m => m.Value).Distinct().OrderBy(x => x).ToArray();
foreach (var (key, t) in Strings.Table)
{
    Check(t.De.Length > 0 && t.En.Length > 0, $"Leerer Text: {key}");
    Check(Placeholders(t.De).SequenceEqual(Placeholders(t.En)), $"Platzhalter DE/EN verschieden: {key}");
}

// 2. Alle im Code/XAML verwendeten Schlüssel existieren
var files = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
    .Where(f => (f.EndsWith(".cs") || f.EndsWith(".xaml")) && !f.EndsWith("Loc.cs") && !f.EndsWith("TrExtension.cs") && !f.Contains(@"\obj\") && !f.Contains(@"\bin\"));
var used = new HashSet<string>();
foreach (var f in files)
{
    var text = File.ReadAllText(f);
    foreach (Match m in Regex.Matches(text, @"Loc\.T\(\s*""([A-Za-z_]+)""\s*[,)]")) used.Add(m.Groups[1].Value);
    foreach (Match m in Regex.Matches(text, @"\{l:Tr\s+([A-Za-z_]+)\}")) used.Add(m.Groups[1].Value);
}
used.UnionWith(new[] { "start", "stop", "restart" }.Select(a => "Act_" + a));  // zusammengesetzte Schlüssel
used.UnionWith(typeof(St).GetFields().Select(f => "State_" + f.GetRawConstantValue()));
foreach (var k in used) Check(Strings.Table.ContainsKey(k), $"Schlüssel fehlt in Strings: {k}");

// 3. Hilfe-Anker: jeder Tag="…" in XAML und die Navigation existiert in beiden Seiten
var help = Path.Combine(root, "src", "VpnSwitch.App", "Help");
var ids = new Dictionary<string, HashSet<string>>();
foreach (var lang in new[] { "de", "en" })
{
    var html = File.ReadAllText(Path.Combine(help, lang + ".html"));
    ids[lang] = Regex.Matches(html, @"<section id=""([^""]+)""").Select(m => m.Groups[1].Value).ToHashSet();
    foreach (Match m in Regex.Matches(html, @"href=""#([^""]+)"""))
        Check(ids[lang].Contains(m.Groups[1].Value), $"{lang}.html: Link auf fehlenden Anker #{m.Groups[1].Value}");
}
Check(ids["de"].SetEquals(ids["en"]), "Hilfe-Anker in de.html und en.html verschieden");
var tags = new HashSet<string>();
foreach (var f in files.Where(f => f.EndsWith(".xaml")))
    foreach (Match m in Regex.Matches(File.ReadAllText(f), @"Tag=""([a-z\-]+)"""))
        if (!m.Groups[1].Value.Equals("de") && !m.Groups[1].Value.Equals("en")) tags.Add(m.Groups[1].Value);
foreach (var t in tags) Check(ids["de"].Contains(t), $"Hilfe-Anker fehlt: {t}");

// 4. Kernlogik
Check(Scanner.Norm(@"""C:\Program Files\OpenVPN\bin\openvpnserv.exe""") == @"c:\program files\openvpn\bin\openvpnserv.exe", "Norm mit Anführungszeichen");
Check(Scanner.Norm("") == "", "Norm leer");
Check(UpdateChecker.ParseTag("v1.3") == new Version(1, 3, 0, 0), "ParseTag v1.3");
Check(UpdateChecker.ParseTag("v1.3.0-beta") is null, "ParseTag Vorabversion");
Check(UpdateChecker.IsNewer(new Version(2, 0, 1), new Version(2, 0)), "IsNewer");
Check(Loc.StateText("Running") == "läuft", "StateText de");
Loc.Instance.Language = "en";
Check(Loc.StateText("Running") == "running", "StateText en");
Check(Loc.StateText("Paused") == "Paused", "StateText unbekannt");

if (failures.Count == 0) { Console.WriteLine("Alle Tests bestanden."); return 0; }
foreach (var f in failures) Console.Error.WriteLine("FEHLER: " + f);
return 1;
