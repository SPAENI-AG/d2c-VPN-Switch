using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VpnSwitch.Core;

namespace VpnSwitch.App;

public partial class MainWindow : Window
{
    private ScanResult? _last;

    public MainWindow()
    {
        InitializeComponent();

        Title = $"{AppInfo.Title}  v{AppInfo.Version.ToString(3)}";
        tb_FooterCompany.Text = AppInfo.Company;
        tb_FooterVersion.Text = AppInfo.VersionText;

        SettingsPage.LanguageSelected += (_, lang) => SetLanguage(lang);
        UpdateLanguageChecks();
        Loc.Instance.LanguageChanged += (_, _) =>
        {
            RefreshUpdateBanner();
            if (_last is not null) Fill(_last);   // in Code gesetzte Texte neu aufbauen
        };
    }

    // --- Start / Updates (GitHub Releases) ---------------------------------
    private ReleaseInfo? _available;      // neue Version verfügbar
    private Version? _updatedTo;          // seit dem letzten Start aktualisiert

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var s = App.Settings;
        var current = UpdateChecker.Normalize(AppInfo.Version);

        // Hinweis einmalig nach einem Update
        var last = UpdateChecker.ParseTag(s.LastRunVersion);
        if (last is not null && UpdateChecker.IsNewer(current, last)) _updatedTo = current;
        s.LastRunVersion = UpdateChecker.Format(current);
        TrySave();
        RefreshUpdateBanner();

        var scan = DoScan();

        // automatische Prüfung (höchstens alle 20 h, still bei Fehlern)
        if (UpdateChecker.IsDue(s, DateTime.UtcNow))
        {
            try
            {
                var latest = await UpdateChecker.GetLatestAsync(current);
                s.LastUpdateCheckUtc = DateTime.UtcNow;
                TrySave();
                if (latest is not null && UpdateChecker.IsNewer(latest.Version, current)
                    && UpdateChecker.Format(latest.Version) != s.SkippedVersion)
                {
                    _available = latest;
                    RefreshUpdateBanner();
                }
            }
            catch (Exception)
            {
                // offline / Proxy / GitHub nicht erreichbar: beim nächsten Start erneut
            }
        }
        await scan;
    }

    private async void mi_CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (!UpdateChecker.IsConfigured)
        {
            MessageBox.Show(Loc.T("Upd_NotConfigured"), AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var current = UpdateChecker.Normalize(AppInfo.Version);
        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var latest = await UpdateChecker.GetLatestAsync(current);
            App.Settings.LastUpdateCheckUtc = DateTime.UtcNow;
            TrySave();
            if (latest is not null && UpdateChecker.IsNewer(latest.Version, current))
            {
                _available = latest;      // manuell: auch eine übersprungene Version anzeigen
                RefreshUpdateBanner();
            }
            else
            {
                MessageBox.Show(Loc.T("Upd_UpToDate", UpdateChecker.Format(current)), AppInfo.Title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(Loc.T("Upd_Failed", ex.Message), AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private void RefreshUpdateBanner()
    {
        if (_available is not null)
        {
            tb_UpdateText.Text = Loc.T("Upd_Available", UpdateChecker.Format(_available.Version), UpdateChecker.Format(AppInfo.Version));
            bt_UpdateDownload.Visibility = Visibility.Visible;
            bt_UpdateSkip.Visibility = Visibility.Visible;
            bt_UpdateNotes.Visibility = Visibility.Visible;
            bd_Update.Visibility = Visibility.Visible;
        }
        else if (_updatedTo is not null)
        {
            tb_UpdateText.Text = Loc.T("Upd_Updated", UpdateChecker.Format(_updatedTo));
            bt_UpdateDownload.Visibility = Visibility.Collapsed;
            bt_UpdateSkip.Visibility = Visibility.Collapsed;
            bt_UpdateNotes.Visibility = UpdateChecker.IsConfigured ? Visibility.Visible : Visibility.Collapsed;
            bd_Update.Visibility = Visibility.Visible;
        }
        else
        {
            bd_Update.Visibility = Visibility.Collapsed;
        }
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void bt_UpdateDownload_Click(object sender, RoutedEventArgs e)
    {
        if (_available is not null) OpenUrl(_available.DownloadUrl ?? _available.PageUrl);
    }

    private void bt_UpdateNotes_Click(object sender, RoutedEventArgs e)
    {
        if (_available is not null) OpenUrl(_available.PageUrl);
        else if (_updatedTo is not null) OpenUrl(UpdateChecker.ReleasePage(_updatedTo));
    }

    private void bt_UpdateSkip_Click(object sender, RoutedEventArgs e)
    {
        if (_available is null) return;
        App.Settings.SkippedVersion = UpdateChecker.Format(_available.Version);
        TrySave();
        _available = null;
        RefreshUpdateBanner();
    }

    private void bt_UpdateClose_Click(object sender, RoutedEventArgs e)
    {
        if (_available is not null) _available = null;   // bis zum nächsten Start ausblenden
        else _updatedTo = null;
        RefreshUpdateBanner();
    }

    // --- Sprache ---------------------------------------------------------
    private void SetLanguage(string lang)
    {
        if (lang == Loc.Instance.Language) { UpdateLanguageChecks(); return; }
        Loc.Instance.Language = lang;          // alle {l:Tr …}-Bindings aktualisieren sich sofort
        App.Settings.Language = lang;
        TrySave();
        UpdateLanguageChecks();
        SettingsPage.LoadFromSettings();       // Sprach-Combo synchron halten
    }

    private void UpdateLanguageChecks()
    {
        mi_LangDe.IsChecked = Loc.Instance.Language == "de";
        mi_LangEn.IsChecked = Loc.Instance.Language == "en";
    }

    private void mi_LangDe_Click(object sender, RoutedEventArgs e) => SetLanguage("de");
    private void mi_LangEn_Click(object sender, RoutedEventArgs e) => SetLanguage("en");

    // --- Menü / Hilfe ----------------------------------------------------
    private void mi_Exit_Click(object sender, RoutedEventArgs e) => Close();
    private void mi_Settings_Click(object sender, RoutedEventArgs e) => tc_Main.SelectedItem = ti_Settings;
    private void mi_Help_Click(object sender, RoutedEventArgs e) => ShowHelp("");

    private void mi_HelpTopic_Click(object sender, RoutedEventArgs e) =>
        ShowHelp((sender as FrameworkElement)?.Tag as string ?? "");

    private void Help_Click(object sender, RoutedEventArgs e) =>
        ShowHelp((sender as FrameworkElement)?.Tag as string ?? "");

    public static void ShowHelp(string anchor)
    {
        var error = HelpServer.Open(anchor);
        if (error is not null) MessageBox.Show(error, AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void mi_SettingsFolder_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(AppSettings.Folder) { UseShellExecute = true });

    private void mi_About_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e) => TrySave();

    public static void TrySave()
    {
        try { App.Settings.Save(); }
        catch (Exception) { /* nicht kritisch */ }
    }

    // --- Log / Busy ------------------------------------------------------
    private void Log(string msg)
    {
        tb_Log.AppendText($"{DateTime.Now:HH:mm:ss}  {msg}{Environment.NewLine}");
        tb_Log.ScrollToEnd();
    }

    /// <summary>Für Aufrufe aus Hintergrund-Tasks.</summary>
    private void Log2(string msg) => Dispatcher.BeginInvoke(() => Log(msg));

    private void Busy(bool b)
    {
        Mouse.OverrideCursor = b ? Cursors.Wait : null;
        tc_Main.IsEnabled = bt_Scan.IsEnabled = !b;
        pb_Busy.Visibility = b ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task RunLogged(string title, Func<string?> work, string okText = "")
    {
        Busy(true);
        Log(title + " …");
        var err = await Task.Run(work);
        Log(err is null ? (okText.Length > 0 ? okText : Loc.T("Log_Ok")) : Loc.T("Log_Fail", err));
        Busy(false);
        await DoScan();
    }

    // --- Suche -----------------------------------------------------------
    private async void bt_Scan_Click(object sender, RoutedEventArgs e) => await DoScan();

    private async Task DoScan()
    {
        Busy(true);
        Log(Loc.T("Log_Scanning"));
        ScanResult r;
        try { r = await Task.Run(Scanner.Scan); }
        catch (Exception ex) { Log(Loc.T("Log_ScanFailed", ex.Message)); Busy(false); return; }
        _last = r;
        Fill(r);
        Log(Loc.T("Log_ScanFound", r.Providers.Count, r.Services.Count, r.Adapters.Count, r.Apps.Count));
        foreach (var pr in Process.GetProcessesByName("openvpn"))
        {
            var path = "";
            try { path = pr.MainModule?.FileName ?? ""; } catch { }
            Log(Loc.T("Log_OvpnRunning", pr.Id, path));
        }
        Busy(false);
    }

    private void Fill(ScanResult r)
    {
        var cur = Scanner.Norm(r.InteractivePath);
        string? activeLabel = null;

        var prov = new List<ProviderRow>();
        foreach (var p in r.Providers.OrderBy(x => x.Kind == "ewon" ? 1 : 0).ThenBy(x => x.Label))
        {
            if (p.Kind == "ewon")
            {
                // eigener Dienst, läuft parallel zum gemeinsamen Dienst: Status statt „aktiv“-Punkt
                var running = p.State == St.Running;
                prov.Add(new ProviderRow(running ? Loc.StateText(St.Running) : "", p.Label, p.OpenVpnVersion, p.ServPath, running ? "ok" : "", p));
                continue;
            }
            var active = Scanner.Norm(p.ServPath) == cur;
            if (active) activeLabel = p.Label;
            prov.Add(new ProviderRow(active ? "●" : "", p.Label, p.OpenVpnVersion, p.ServPath, active ? "active" : "", p));
        }
        dg_Prov.ItemsSource = prov;

        if (!r.Providers.Any(x => x.Kind == "ovpn"))
        {
            tb_Active.Text = Loc.T("Hdr_NoServ");
            tb_Active.Foreground = System.Windows.Media.Brushes.DarkRed;
        }
        else
        {
            tb_Active.Text = Loc.T("Hdr_Active", Scanner.InteractiveSvc, activeLabel ?? Loc.T("Hdr_Unknown"), Loc.StateText(r.InteractiveState));
            tb_Active.Foreground = activeLabel is null ? System.Windows.Media.Brushes.DarkRed : System.Windows.Media.Brushes.DarkGreen;
        }

        dg_Svc.ItemsSource = r.Services.OrderBy(x => x.Name).Select(s => new ServiceRow(
            s.Name, s.Display, Loc.StateText(s.State), s.StartMode, s.Path,
            s.State != St.Running && s.StartMode == "Auto" ? "bad" : "", s)).ToList();

        dg_Ad.ItemsSource = r.Adapters.OrderBy(x => x.Name).Select(a => new AdapterRow(
            a.Name, a.Description,
            Loc.StateText(a.Status, a.StatusCode) + (a.ProblemCode != 0 ? " · " + Loc.T("Ad_Problem", a.ProblemCode) : ""),
            a.DriverVersion, a.Guid,
            a.ProblemCode != 0 ? "bad" : a.Status == St.Disabled ? "off" : a.Status == St.Connected ? "ok" : "", a)).ToList();

        FillDriverLine(r);

        dg_Apps.ItemsSource = r.Apps.OrderBy(x => x.Name).Select(a => new SoftwareRow(a.Name, a.Version, a.Publisher)).ToList();

        // eWON / eCatcher
        var svc = r.Services.FirstOrDefault(s => Scanner.Talk2mRx.IsMatch(s.Name + s.Display));
        var installed = r.Providers.Any(x => x.Kind == "ewon") || r.Apps.Any(a => Scanner.EwonRx.IsMatch(a.Name)) || svc != null;
        if (!installed)
        {
            tb_Ewon.Text = Loc.T("Ew_NotInstalled");
            tb_Ewon.Foreground = System.Windows.Media.Brushes.Gray;
        }
        else
        {
            var tap = r.Adapters.Any(a => a.Name.Equals(Scanner.EcatcherAdapterName, StringComparison.OrdinalIgnoreCase));
            tb_Ewon.Text = Loc.T("Ew_Status", Loc.StateText(svc?.State ?? St.ServiceMissing), Scanner.EcatcherAdapterName,
                Loc.T(tap ? "Ew_AdapterPresent" : "Ew_AdapterMissing"));
            tb_Ewon.Foreground = svc is { State: St.Running } && tap
                ? System.Windows.Media.Brushes.DarkGreen : System.Windows.Media.Brushes.DarkRed;
        }
    }

    /// <summary>TAP-Treiber-Diagnose: verschiedene Versionen oder Geräteprobleme führen zu „CreateFile failed on tap-windows6 device“.</summary>
    private void FillDriverLine(ScanResult r)
    {
        var sys = r.TapSysVersion.Length > 0 ? Loc.T("Drv_Sys", r.TapSysVersion) : "";
        var lines = new List<string>();
        if (r.TapVersions.Count > 1) lines.Add(Loc.T("Drv_Mismatch", string.Join(", ", r.TapVersions)));
        if (r.TapProblems.Count > 0) lines.Add(Loc.T("Drv_Problem", string.Join(", ", r.TapProblems)));

        if (lines.Count > 0)
        {
            tb_Driver.Text = string.Join("  ", lines) + sys;
            tb_Driver.Foreground = System.Windows.Media.Brushes.DarkRed;
        }
        else if (r.TapVersions.Count == 1)
        {
            tb_Driver.Text = Loc.T("Drv_Ok", r.TapVersions[0]) + sys;
            tb_Driver.Foreground = System.Windows.Media.Brushes.DarkGreen;
        }
        else
        {
            tb_Driver.Text = Loc.T("Drv_None");
            tb_Driver.Foreground = System.Windows.Media.Brushes.Gray;
        }
    }

    // --- Umschalten ------------------------------------------------------
    private void dg_Prov_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (dg_Prov.SelectedItem is not null) bt_Activate_Click(sender, e);
    }

    private async void bt_Activate_Click(object sender, RoutedEventArgs e)
    {
        if (dg_Prov.SelectedItem is not ProviderRow row) { Log(Loc.T("Log_SelectEntry")); return; }
        var p = row.Source;
        if (p.Kind == "ewon") { await ActivateEwon(p); return; }
        if (_last is not null && Scanner.Norm(p.ServPath) == Scanner.Norm(_last.InteractivePath))
        {
            Log(Loc.T("Log_AlreadyActive", p.Label));
            return;
        }
        await RunLogged(Loc.T("Log_Switching", Scanner.InteractiveSvc, p.Label),
            () => Ops.ActivateOpenVpn(p), Loc.T("Log_SwitchOk", p.Label));
    }

    private async Task ActivateEwon(Provider p)
    {
        if (string.IsNullOrEmpty(p.ServiceName)) { Log(Loc.T("Log_EcNoService")); return; }
        await RunLogged(Loc.T("Log_EcStarting", p.ServiceName), () => Ops.StartService(p.ServiceName), Loc.T("Log_EcOk"));
        if (_last is not null && !_last.Adapters.Any(a => a.Name.Equals(Scanner.EcatcherAdapterName, StringComparison.OrdinalIgnoreCase)))
            Log(Loc.T("Log_EcAdapterMissing", Scanner.EcatcherAdapterName));
    }

    private async void bt_Repair_Click(object sender, RoutedEventArgs e)
    {
        if (Process.GetProcessesByName("eCatcher").Length > 0)
        {
            MessageBox.Show(this, Loc.T("Msg_EcRunning"), Loc.T("Msg_EcRunningTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show(this, Loc.T("Msg_RepairConfirm"), Loc.T("Btn_RepairEc"), MessageBoxButton.OKCancel, MessageBoxImage.Question)
            != MessageBoxResult.OK) return;

        await RunLogged(Loc.T("Btn_RepairEc"), () => Ops.RepairEcatcher(Log2), Loc.T("Log_RepairDone"));
    }

    // --- Dienste ---------------------------------------------------------
    private async Task ServiceAction(string action)
    {
        if (dg_Svc.SelectedItem is not ServiceRow row) { Log(Loc.T("Log_SelectService")); return; }
        var name = row.Source.Name;
        await RunLogged(Loc.T("Log_ServiceAction", name, Loc.T("Act_" + action)), () => Ops.ServiceAction(name, action));
    }

    private async void bt_SvcStart_Click(object sender, RoutedEventArgs e) => await ServiceAction("start");
    private async void bt_SvcStop_Click(object sender, RoutedEventArgs e) => await ServiceAction("stop");
    private async void bt_SvcRestart_Click(object sender, RoutedEventArgs e) => await ServiceAction("restart");

    private async void bt_Kill_Click(object sender, RoutedEventArgs e)
    {
        var count = Process.GetProcessesByName("openvpn").Length;
        if (count == 0) { Log(Loc.T("Log_NoOvpn")); return; }
        if (MessageBox.Show(this, Loc.T("Msg_KillConfirm", count), Loc.T("Msg_KillTitle"), MessageBoxButton.OKCancel, MessageBoxImage.Warning)
            != MessageBoxResult.OK) return;
        await RunLogged(Loc.T("Log_Killing"), Ops.KillOpenVpn);
    }

    // --- Adapter ---------------------------------------------------------
    private AdapterInfo? SelectedAdapter()
    {
        if (dg_Ad.SelectedItem is AdapterRow row) return row.Source;
        Log(Loc.T("Log_SelectAdapter"));
        return null;
    }

    private async void bt_AdRename_Click(object sender, RoutedEventArgs e)
    {
        var a = SelectedAdapter(); if (a is null) return;
        var n = PromptWindow.Ask(this, Loc.T("Dlg_RenameTitle"), Loc.T("Dlg_RenamePrompt", a.Name), a.Name);
        if (string.IsNullOrWhiteSpace(n) || n == a.Name) return;
        n = n.Trim();
        await RunLogged(Loc.T("Log_Renaming", a.Name, n), () => Tools.RenameAdapter(a.Name, n));
    }

    private async Task AdapterEnable(bool on)
    {
        var a = SelectedAdapter(); if (a is null) return;
        await RunLogged(Loc.T(on ? "Log_Enabling" : "Log_Disabling", a.Name), () => Tools.EnableAdapter(a.Name, on));
    }

    private async void bt_AdEnable_Click(object sender, RoutedEventArgs e) => await AdapterEnable(true);
    private async void bt_AdDisable_Click(object sender, RoutedEventArgs e) => await AdapterEnable(false);

    private async void bt_AdNew_Click(object sender, RoutedEventArgs e)
    {
        var n = PromptWindow.Ask(this, Loc.T("Dlg_NewTapTitle"), Loc.T("Dlg_NewTapPrompt"), "");
        if (string.IsNullOrWhiteSpace(n)) return;
        n = n.Trim();
        await RunLogged(Loc.T("Log_CreatingTap", n), () => Tools.CreateTap(n, Log2));
    }
}
