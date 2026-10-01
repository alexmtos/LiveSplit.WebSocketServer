using LiveSplit.WsServer.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.UI.Components;

/// <summary>
///     Which network interfaces the server listens on.
/// </summary>
public enum BindMode
{
    /// <summary>Only programs on this computer can connect.</summary>
    Localhost,

    /// <summary>Other devices on the network can connect too.</summary>
    AllInterfaces,
}

public partial class Settings : UserControl, IServerOptions
{
    public const ushort DefaultPort = 15721;
    public const int DefaultRefreshInterval = 15;

    public bool AutoStart { get; set; }

    public ushort Port { get; set; }

    public BindMode BindMode { get; set; }

    public bool ReadOnly { get; set; }

    public bool AllowFileCommands { get; set; }

    /// <summary>
    ///     When not empty, clients must connect with <c>?token=...</c>.
    /// </summary>
    public string AuthToken { get; set; }

    /// <summary>
    ///     Origins (for example <c>https://example.com</c>) that browsers may connect from, separated
    ///     by commas or new lines. Empty allows every origin.
    /// </summary>
    public string AllowedOrigins { get; set; }

    /// <summary>
    ///     Seconds between "refresh" broadcasts of the full state. 0 disables them.
    /// </summary>
    public int RefreshInterval { get; set; }

    public string LocalIP { get; set; }

    public string PortString
    {
        get => Port.ToString();
        set
        {
            // Ignore invalid input instead of throwing from the settings dialog.
            if (ushort.TryParse(value, out ushort port) && port > 0)
            {
                Port = port;
            }
        }
    }

    public IPAddress BindAddress => BindMode == BindMode.AllInterfaces ? IPAddress.Any : IPAddress.Loopback;

    public IReadOnlyCollection<string> AllowedOriginList => (AllowedOrigins ?? "")
        .Split([',', '\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim().TrimEnd('/'))
        .Where(x => x.Length > 0)
        .ToList();

    public Settings()
    {
        InitializeComponent();

        AutoStart = false;
        Port = DefaultPort;
        // New components only accept local connections. Layouts saved before this setting
        // existed keep accepting network connections (see SetSettings).
        BindMode = BindMode.Localhost;
        ReadOnly = false;
        AllowFileCommands = false;
        AuthToken = "";
        AllowedOrigins = "";
        RefreshInterval = DefaultRefreshInterval;
        LocalIP = GetIP();
        lblLocalIP.Text = LocalIP;

        cmbBindMode.Items.AddRange(["This computer only", "Other devices on the network too"]);

        chkAutoStart.DataBindings.Add("Checked", this, nameof(AutoStart), false, DataSourceUpdateMode.OnPropertyChanged);
        txtPort.DataBindings.Add("Text", this, nameof(PortString), false, DataSourceUpdateMode.OnPropertyChanged);
        chkReadOnly.DataBindings.Add("Checked", this, nameof(ReadOnly), false, DataSourceUpdateMode.OnPropertyChanged);
        chkAllowFileCommands.DataBindings.Add("Checked", this, nameof(AllowFileCommands), false, DataSourceUpdateMode.OnPropertyChanged);
        txtToken.DataBindings.Add("Text", this, nameof(AuthToken), false, DataSourceUpdateMode.OnPropertyChanged);
        txtOrigins.DataBindings.Add("Text", this, nameof(AllowedOrigins), false, DataSourceUpdateMode.OnPropertyChanged);
        numRefreshInterval.DataBindings.Add("Value", this, nameof(RefreshInterval), false, DataSourceUpdateMode.OnPropertyChanged);

        Load += (s, e) => RefreshControls();
        cmbBindMode.SelectedIndexChanged += (s, e) =>
        {
            BindMode = cmbBindMode.SelectedIndex == 1 ? BindMode.AllInterfaces : BindMode.Localhost;
            UpdateConnectionUrl();
        };
        txtPort.TextChanged += (s, e) => UpdateConnectionUrl();
        txtToken.TextChanged += (s, e) => UpdateConnectionUrl();
        btnGenerateToken.Click += (s, e) => txtToken.Text = GenerateToken();
    }

    private void RefreshControls()
    {
        cmbBindMode.SelectedIndex = BindMode == BindMode.AllInterfaces ? 1 : 0;
        UpdateConnectionUrl();
    }

    private void UpdateConnectionUrl()
    {
        string host = BindMode == BindMode.AllInterfaces && LocalIP != "Unknown"
            ? LocalIP.Split(',')[0].Trim()
            : "127.0.0.1";
        string url = $"ws://{host}:{Port}/?protocol=2";
        if (!string.IsNullOrEmpty(AuthToken))
        {
            url += "&token=" + Uri.EscapeDataString(AuthToken);
        }

        txtConnectionUrl.Text = url;
    }

    public static string GenerateToken()
    {
        var bytes = new byte[18];
        using (var random = System.Security.Cryptography.RandomNumberGenerator.Create())
        {
            random.GetBytes(bytes);
        }

        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_');
    }

    public static string GetIP()
    {
        try
        {
            string[] addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                .Select(a => a.ToString())
                .ToArray();
            return addresses.Length > 0 ? string.Join(", ", addresses) : "Unknown";
        }
        catch (Exception)
        {
            return "Unknown";
        }
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Settings");
        CreateSettingsNode(document, parent);
        return parent;
    }

    public int GetSettingsHashCode()
    {
        return CreateSettingsNode(null, null);
    }

    private int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        return SettingsHelper.CreateSetting(document, parent, "Version", "2.0") ^
            SettingsHelper.CreateSetting(document, parent, "AutoStart", AutoStart) ^
            SettingsHelper.CreateSetting(document, parent, "Port", PortString) ^
            SettingsHelper.CreateSetting(document, parent, "BindMode", BindMode) ^
            SettingsHelper.CreateSetting(document, parent, "ReadOnly", ReadOnly) ^
            SettingsHelper.CreateSetting(document, parent, "AllowFileCommands", AllowFileCommands) ^
            SettingsHelper.CreateSetting(document, parent, "AuthToken", AuthToken) ^
            SettingsHelper.CreateSetting(document, parent, "AllowedOrigins", AllowedOrigins) ^
            SettingsHelper.CreateSetting(document, parent, "RefreshInterval", RefreshInterval);
    }

    public void SetSettings(XmlNode node)
    {
        var settings = (XmlElement)node;
        AutoStart = SettingsHelper.ParseBool(settings["AutoStart"], false);
        PortString = SettingsHelper.ParseString(settings["Port"], DefaultPort.ToString());
        // Before 2.0 the server always listened on every interface.
        BindMode = SettingsHelper.ParseEnum(settings["BindMode"], BindMode.AllInterfaces);
        ReadOnly = SettingsHelper.ParseBool(settings["ReadOnly"], false);
        AllowFileCommands = SettingsHelper.ParseBool(settings["AllowFileCommands"], false);
        AuthToken = SettingsHelper.ParseString(settings["AuthToken"], "");
        AllowedOrigins = SettingsHelper.ParseString(settings["AllowedOrigins"], "");
        RefreshInterval = Math.Min(3600, Math.Max(0, SettingsHelper.ParseInt(settings["RefreshInterval"], DefaultRefreshInterval)));
        RefreshControls();
    }
}
