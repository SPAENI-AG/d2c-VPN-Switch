using System.Diagnostics;
using System.Management;
using System.Text;

namespace VpnSwitch.Core;

/// <summary>Prozessaufrufe, PowerShell und TAP-Adapter-Verwaltung. Rückgabe "string?": null = OK, sonst Fehlertext.</summary>
public static class Tools
{
    public const string EcName = Scanner.EcatcherAdapterName;
    public const string OvpnName = "OpenVPN TAP-Windows6";

    private static string Q(string v) => v.Replace("'", "''");

    public static int Run(string file, string args, string? workDir, out string output)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (workDir != null) psi.WorkingDirectory = workDir;
        using var p = Process.Start(psi)!;
        var o = p.StandardOutput.ReadToEndAsync();
        var e = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(180000))
        {
            try { p.Kill(); } catch { }
            output = Loc.T("Err_Timeout");
            return -1;
        }
        output = (o.Result + "\n" + e.Result).Trim();
        return p.ExitCode;
    }

    /// <summary>PowerShell über -EncodedCommand. null = OK.</summary>
    public static string? PS(string command)
    {
        var enc = Convert.ToBase64String(Encoding.Unicode.GetBytes("$ErrorActionPreference='Stop'; " + command));
        var rc = Run("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + enc, null, out var outp);
        return rc == 0 ? null : (outp.Length > 0 ? outp : Loc.T("Err_ExitCode", rc));
    }

    public static string? RenameAdapter(string oldName, string newName) =>
        PS($"Rename-NetAdapter -Name '{Q(oldName)}' -NewName '{Q(newName)}'");

    public static string? EnableAdapter(string name, bool on) =>
        PS($"{(on ? "Enable" : "Disable")}-NetAdapter -Name '{Q(name)}' -Confirm:$false");

    public static List<AdapterInfo> TapAdapters()
    {
        var list = new List<AdapterInfo>();
        using var s = new ManagementObjectSearcher("SELECT NetConnectionID, Name, GUID FROM Win32_NetworkAdapter");
        foreach (ManagementObject o in s.Get())
        {
            var id = o["NetConnectionID"]?.ToString() ?? "";
            var name = o["Name"]?.ToString() ?? "";
            if (id.Length == 0 || name.IndexOf("TAP", StringComparison.OrdinalIgnoreCase) < 0) continue;
            list.Add(new AdapterInfo { Name = id, Description = name, Guid = o["GUID"]?.ToString() ?? "" });
        }
        return list;
    }

    /// <summary>Legt einen TAP-Windows6-Adapter mit Namen an. Bevorzugt tapctl.exe (OpenVPN), sonst addtap.bat (eCatcher).</summary>
    public static string? CreateTap(string name, Action<string> log)
    {
        if (TapAdapters().Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return Loc.T("Err_AdapterExists", name);

        var pf = Environment.GetEnvironmentVariable("ProgramW6432") ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var tapctl = Path.Combine(pf, @"OpenVPN\bin\tapctl.exe");
        string outp;
        if (File.Exists(tapctl))
        {
            log(Loc.T("Log_TapctlCreate"));
            var rc = Run(tapctl, "create --hwid root\\tap0901 --name \"" + name + "\"", null, out outp);
            if (rc == 0) { log(Loc.T("Log_TapctlDone", name, outp)); return null; }
            log(Loc.T("Log_TapctlFailed", outp));
        }

        var bat = FindAddTap();
        if (bat == null) return Loc.T("Err_NoTapTool");
        var before = new HashSet<string>(TapAdapters().Select(a => a.Guid), StringComparer.OrdinalIgnoreCase);
        log(Loc.T("Log_AddtapCreate", bat));
        Run("cmd.exe", "/c \"" + bat + "\" < nul", Path.GetDirectoryName(bat), out outp);
        Thread.Sleep(3000);
        var neu = TapAdapters().FirstOrDefault(a => !before.Contains(a.Guid));
        if (neu == null) return Loc.T("Err_AddtapNoAdapter", outp);
        log(Loc.T("Log_AddtapRename", neu.Guid, neu.Name, name));
        return RenameAdapter(neu.Name, name);
    }

    private static string? FindAddTap()
    {
        foreach (var root in new[] { Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramW6432") })
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
            try
            {
                foreach (var d in Directory.GetDirectories(root, "*eCatcher*"))
                {
                    var f = Directory.GetFiles(d, "addtap.bat", SearchOption.AllDirectories);
                    var x64 = f.FirstOrDefault(x => x.Contains("64", StringComparison.OrdinalIgnoreCase) && Environment.Is64BitOperatingSystem);
                    if (x64 != null) return x64;
                    if (f.Length > 0) return f[0];
                }
            }
            catch { }
        }
        return null;
    }
}
