using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Win32;

namespace VpnSwitch.Core;

/// <summary>Aktionen, die Dienste/Adapter/Prozesse verändern. Blockierend – im UI in Task.Run aufrufen. null = OK.</summary>
public static class Ops
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(20);

    private static string Msg(Exception ex) => ex.InnerException?.Message ?? ex.Message;

    /// <summary>OpenVPNServiceInteractive auf die openvpnserv.exe des Providers umstellen.</summary>
    public static string? ActivateOpenVpn(Provider p)
    {
        try
        {
            using var sc = new ServiceController(Scanner.InteractiveSvc);
            sc.Refresh();
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, Wait);
            }
            using (var k = Registry.LocalMachine.OpenSubKey(Scanner.InteractiveKey, true))
            {
                if (k == null) return Loc.T("Err_ServiceNotInstalled");
                k.SetValue("ImagePath", "\"" + p.ServPath + "\"", RegistryValueKind.ExpandString);
            }
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, Wait);
            return null;
        }
        catch (Exception ex) { return Msg(ex); }
    }

    /// <summary>Talk2M-Dienst starten.</summary>
    public static string? StartService(string name)
    {
        try
        {
            using var sc = new ServiceController(name);
            if (sc.Status != ServiceControllerStatus.Running)
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, Wait);
            }
            return null;
        }
        catch (Exception ex) { return Msg(ex); }
    }

    /// <summary>action: "start" | "stop" | "restart".</summary>
    public static string? ServiceAction(string name, string action)
    {
        try
        {
            using var sc = new ServiceController(name);
            if (action != "start" && sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, Wait);
            }
            if (action != "stop")
            {
                sc.Refresh();
                if (sc.Status != ServiceControllerStatus.Running)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, Wait);
                }
            }
            return null;
        }
        catch (Exception ex) { return Msg(ex); }
    }

    public static string? KillOpenVpn()
    {
        foreach (var pr in Process.GetProcessesByName("openvpn"))
        {
            try { pr.Kill(); pr.WaitForExit(5000); }
            catch (Exception ex) { return ex.Message; }
        }
        return null;
    }

    /// <summary>Reparatur-Assistent: eCatcher-Adapter von OpenVPN trennen (siehe docs/HINTERGRUND.md).</summary>
    public static string? RepairEcatcher(Action<string> log)
    {
        try
        {
            var t2m = ServiceController.GetServices()
                .Where(x => Scanner.Talk2mRx.IsMatch(x.ServiceName + " " + x.DisplayName)).ToList();
            foreach (var sc in t2m)
            {
                if (sc.Status != ServiceControllerStatus.Stopped)
                {
                    log(Loc.T("Log_Stopping", sc.DisplayName));
                    sc.Stop(); sc.WaitForStatus(ServiceControllerStatus.Stopped, Wait);
                }
            }

            var taps = Tools.TapAdapters();
            var ec = taps.FirstOrDefault(a => a.Name.Equals(Tools.EcName, StringComparison.OrdinalIgnoreCase));
            var ovpnExists = taps.Any(a => a.Name.Equals(Tools.OvpnName, StringComparison.OrdinalIgnoreCase));

            if (ec != null && !ovpnExists)
            {
                log(Loc.T("Log_ReturnAdapter", Tools.EcName, ec.Guid));
                var e1 = Tools.RenameAdapter(ec.Name, Tools.OvpnName);
                if (e1 != null) return Loc.T("Err_RenameFailed", e1);
                ec = null;
            }
            else if (ec != null)
            {
                log(Loc.T("Log_EcOwnAdapter", ec.Guid));
            }

            if (ec == null)
            {
                var e2 = Tools.CreateTap(Tools.EcName, log);
                if (e2 != null) return e2;
            }

            foreach (var sc in t2m)
            {
                log(Loc.T("Log_Starting", sc.DisplayName));
                sc.Refresh();
                if (sc.Status != ServiceControllerStatus.Running)
                {
                    sc.Start(); sc.WaitForStatus(ServiceControllerStatus.Running, Wait);
                }
            }
            return null;
        }
        catch (Exception ex) { return Msg(ex); }
    }
}
