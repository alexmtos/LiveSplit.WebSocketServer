using LiveSplit.WsServer.Commands;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.UI.Components
{
    public partial class Settings : UserControl, IServerOptions
    {
        public const ushort DefaultPort = 15721;

        public bool AutoStart { get; set; }

        public ushort Port { get; set; }

        public bool ReadOnly { get; set; }

        public bool AllowFileCommands { get; set; }

        public const int DefaultRefreshInterval = 15;

        /// <summary>
        ///     Seconds between "refresh" broadcasts of the full state. 0 disables them.
        /// </summary>
        public int RefreshInterval { get; set; }

        public string LocalIP { get; set; }

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

        public string PortString
        {
            get { return Port.ToString(); }
            set
            {
                // Ignore invalid input instead of throwing from the settings dialog.
                if (ushort.TryParse(value, out ushort port) && port > 0)
                {
                    Port = port;
                }
            }
        }

        public Settings()
        {
            InitializeComponent();
            AutoStart = false;
            Port = DefaultPort;
            ReadOnly = false;
            RefreshInterval = DefaultRefreshInterval;
            LocalIP = GetIP();
            label3.Text = LocalIP;

            chkAutoStart.DataBindings.Add("Checked", this, "AutoStart", false, DataSourceUpdateMode.OnPropertyChanged);
            txtPort.DataBindings.Add("Text", this, "PortString", false, DataSourceUpdateMode.OnPropertyChanged);
            chkReadOnly.DataBindings.Add("Checked", this, "ReadOnly", false, DataSourceUpdateMode.OnPropertyChanged);
        }

        public XmlNode GetSettings(XmlDocument document)
        {
            var parent = document.CreateElement("Settings");
            CreateSettingsNode(document, parent);
            return parent;
        }

        public int GetSettingsHashCode()
        {
            return CreateSettingsNode(null, null);
        }

        private int CreateSettingsNode(XmlDocument document, XmlElement parent)
        {
            return SettingsHelper.CreateSetting(document, parent, "AutoStart", AutoStart) ^
                SettingsHelper.CreateSetting(document, parent, "Port", PortString) ^
                SettingsHelper.CreateSetting(document, parent, "ReadOnly", ReadOnly) ^
                SettingsHelper.CreateSetting(document, parent, "RefreshInterval", RefreshInterval);
        }

        public void SetSettings(XmlNode settings)
        {
            AutoStart = SettingsHelper.ParseBool(settings["AutoStart"], false);
            PortString = SettingsHelper.ParseString(settings["Port"], DefaultPort.ToString());
            ReadOnly = SettingsHelper.ParseBool(settings["ReadOnly"], false);
            RefreshInterval = Math.Max(0, SettingsHelper.ParseInt(settings["RefreshInterval"], DefaultRefreshInterval));
        }
    }
}
