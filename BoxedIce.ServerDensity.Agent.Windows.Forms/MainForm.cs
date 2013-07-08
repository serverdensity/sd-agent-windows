using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using BoxedIce.ServerDensity.Agent;
using BoxedIce.ServerDensity.Agent.Downloader;

namespace BoxedIce.ServerDensity.Agent.Windows.Forms
{
    public partial class MainForm : Form
    {
        [DllImport("user32")]
        public static extern UInt32 SendMessage(IntPtr hWnd, UInt32 msg, UInt32 wParam, UInt32 lParam);

        internal const int BCM_FIRST = 0x1600; // Normal button

        internal const int BCM_SETSHIELD = (BCM_FIRST + 0x000C); // Elevated button

        public MainForm(bool runMinimized)
        {
            if (runMinimized)
            {
                WindowState = FormWindowState.Minimized;
            }

            InitializeComponent();

            try
            {
                _ok.FlatStyle = FlatStyle.System;
                SendMessage(_ok.Handle, BCM_SETSHIELD, 0, 0xFFFFFFFF);
            }
            catch
            {
            }
        }

        private bool CheckForDuplicateProcess()
        {
            Process[] processes = Process.GetProcessesByName("BoxedIce.ServerDensity.Agent.Windows.Forms");
            return processes.Length > 1;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            if (CheckForDuplicateProcess())
            {
                MessageBox.Show("Server Density is already running. Click its icon in the system tray to view your configuration.", "Server Density", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
                return;
            }

            AgentConfiguration config = AgentConfiguration.Load();
            _url.Text = config.Url;
            _agentKey.Text = config.AgentKey;
            _iis.Checked = config.IISChecks;
            _pluginDirectory.Text = config.PluginDirectory;
            _plugins.Checked = !string.IsNullOrEmpty(config.PluginDirectory);
            _mongoDBConnectionString.Text = config.MongoDBConnectionString;
            _mongoDB.Checked = !string.IsNullOrEmpty(config.MongoDBConnectionString);
            _dbStats.Checked = config.MongoDBDBStats;
            _replSet.Checked = config.MongoDBReplSet;
            _sqlServer.Checked = config.SQLServerChecks;
            _customPrefix.Checked = !string.IsNullOrEmpty(config.CustomPrefix);
            _customPrefixValue.Text = config.CustomPrefix;
            _eventViewer.Checked = config.EventViewer;

            // Initialise and start the background update checker.
            _updater = new Updater();
            _updater.UpdatesDetected += Updater_UpdatesDetected;
            _updater.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_updater == null)
            {
                return;
            }
            _updater.Stop();
        }

        private void Updater_UpdatesDetected(object sender, EventArgs e)
        {
            Invoke(new MethodInvoker(ShowUpdates));
        }

        private void ShowUpdates()
        {
            using (UpdatesForm updatesForm = new UpdatesForm())
            {
                if (updatesForm.ShowDialog(this) == DialogResult.OK)
                {
                    Invoke(new MethodInvoker(DownloadUpdates));
                }
                else if (updatesForm.DoNotShowAgain)
                {
                    _updater.Stop();
                }
            }
        }

        private void DownloadUpdates()
        {
            ProcessStartInfo info = new ProcessStartInfo(DownloaderExe);
            info.UseShellExecute = true;
            info.Verb = "runas";
            try
            {
                Process p = new Process();
                p.StartInfo = info;
                p.Start();
                Close();
            }
            catch
            {
                MessageBox.Show("There was an error downloading updates.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Ok_Click(object sender, EventArgs e)
        {
            if (!IsURLValid())
            {
                MessageBox.Show("Your Server Density URL is incorrect. It needs to be in the form http://example.serverdensity.com or http://example.serverdensity.io (or using https).", "Invalid URL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!IsAgentKeyValid())
            {
                MessageBox.Show("Your agent key is incorrect. It needs to contain alphanumeric characters and no spaces.", "Invalid agent key", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!IsPluginDirectoryValid())
            {
                MessageBox.Show("Your plugin directory is not valid. It is either blank or doesn't exist.", "Invalid plugin directory", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!IsConnectionStringValid())
            {
                MessageBox.Show("Your MongoDB connection string is not valid.", "Invalid connection string", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Save();
            Hide();
        }

        private void Save()
        {
            ProcessStartInfo info = new ProcessStartInfo(ConfigWriterExe);
            info.UseShellExecute = true;
            info.Verb = "runas";
            info.Arguments = string.Format("{0} {1} {2} \"{3}\" \"{4}\" {5} {6} {7} \"{8}\" {9}",
                _url.Text,
                _agentKey.Text,
                _iis.Checked,
                _plugins.Checked ? _pluginDirectory.Text : string.Empty,
                _mongoDB.Checked ? _mongoDBConnectionString.Text : string.Empty,
                _dbStats.Checked,
                _replSet.Checked,
                _sqlServer.Checked,
                _customPrefix.Checked ? _customPrefixValue.Text : string.Empty,
                _eventViewer.Checked);
            try
            {
                Process p = new Process();
                p.StartInfo = info;
                p.Start();
                p.WaitForExit();
            }
            catch
            {
                MessageBox.Show("There was an error saving your agent configuration.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Cancel_Click(object sender, EventArgs e)
        {
            Hide();
        }

        private void Item_Clicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (e.ClickedItem == _preferences)
            {
                WindowState = FormWindowState.Normal;
                Show();
                BringToFront();
            }
            else
            {
                Close();
            }
        }

        private void NotifyIcon_Click(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            Show();
            BringToFront();
            Focus();
        }

        private void URL_Validating(object sender, CancelEventArgs e)
        {
            IsURLValid();
        }

        private void AgentKey_Validating(object sender, CancelEventArgs e)
        {
            IsAgentKeyValid();
        }

        private bool IsURLValid()
        {
            Match match = UrlRegex.Match(_url.Text);
            if (!match.Success)
            {
                return false;
            }
            return true;
        }

        private bool IsAgentKeyValid()
        {
            Match match = AgentKeyRegex.Match(_agentKey.Text);
            if (!match.Success)
            {
                return false;
            }
            return true;
        }

        private bool IsPluginDirectoryValid()
        {
            if (!_plugins.Checked)
            {
                return true;
            }
            if (_pluginDirectory.Text.Trim().Length == 0)
            {
                return false;
            }
            if (!Directory.Exists(_pluginDirectory.Text))
            {
                return false;
            }
            return true;
        }

        private bool IsConnectionStringValid()
        {
            if (!_mongoDB.Checked)
            {
                return true;
            }
            if (_mongoDBConnectionString.Text.Trim().Length == 0)
            {
                return false;
            }
            return true;
        }

        private void Browse_Click(object sender, EventArgs e)
        {
            if (_folderBrowser.ShowDialog() == DialogResult.OK)
            {
                _pluginDirectory.Text = _folderBrowser.SelectedPath;
            }
        }

        private void Plugins_CheckStateChanged(object sender, EventArgs e)
        {
            _browse.Enabled = _plugins.Checked;
        }

        private void MongoDB_CheckStateChanged(object sender, EventArgs e)
        {
            _mongoDBConnectionString.Enabled = _mongoDB.Checked;
            _dbStats.Enabled = _mongoDB.Checked;
            _replSet.Enabled = _mongoDB.Checked;
        }

        private void PluginDirectory_Validating(object sender, CancelEventArgs e)
        {
        }

        private void SqlServer_CheckStateChanged(object sender, EventArgs e)
        {
            _customPrefix.Enabled = _sqlServer.Checked;
        }

        private void CustomPrefix_CheckStateChanged(object sender, EventArgs e)
        {
            _customPrefixValue.Enabled = _customPrefix.Checked;

            if (!_customPrefix.Checked)
            {
                _customPrefixValue.Text = string.Empty;
            }
        }

        private void InstallPlugin_Click(object sender, EventArgs e)
        {
            _plugins.Checked = true;
            if (!IsPluginDirectoryValid())
            {
                MessageBox.Show("Your plugin directory is not valid. It is either blank or doesn't exist.\r\n\r\nPlease ensure this is configured before installing a plugin.", "Invalid plugin directory", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (PluginForm form = new PluginForm())
            {
                try
                {
                    form.OK.FlatStyle = FlatStyle.System;
                    SendMessage(form.OK.Handle, BCM_SETSHIELD, 0, 0xFFFFFFFF);
                }
                catch
                {
                }

                if (form.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                string agentKey = _agentKey.Text;
                string installKey = form.InstallKey.Text;
                ProcessStartInfo info = new ProcessStartInfo(PluginHelperExe);
                info.UseShellExecute = true;
                info.Verb = "runas";
                info.Arguments = string.Format("{0} \"{1}\" {2} \"{3}\" \"{4}\" {5} {6} {7} \"{8}\" {9} \"{10}\"",
                    _url.Text,
                    _agentKey.Text,
                    _iis.Checked,
                    _plugins.Checked ? _pluginDirectory.Text : string.Empty,
                    _mongoDB.Checked ? _mongoDBConnectionString.Text : string.Empty,
                    _dbStats.Checked,
                    _replSet.Checked,
                    _sqlServer.Checked,
                    _customPrefix.Checked ? _customPrefixValue.Text : string.Empty,
                    _eventViewer.Checked,
                    installKey);

                try
                {
                    Process p = new Process();
                    p.StartInfo = info;
                    p.Start();
                    Close();
                }
                catch
                {
                    MessageBox.Show("There was an error installing your plugin.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private Updater _updater;

        private readonly string ConfigWriterExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BoxedIce.ServerDensity.Agent.ConfigWriter.exe");
        private readonly string DownloaderExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BoxedIce.ServerDensity.Agent.Downloader.exe");
        private readonly string PluginHelperExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BoxedIce.ServerDensity.Agent.Plugins.Windows.Forms.exe");
        private readonly Regex UrlRegex = new Regex(@"http(s)?(\:\/\/)[a-zA-Z0-9_\-]+\.(serverdensity.com|testserverdensity.co.uk|serverdensity.io)$");
        private readonly Regex AgentKeyRegex = new Regex(@"^[a-zA-Z0-9]+$");
    }
}
