using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BoxedIce.ServerDensity.Agent.Downloader
{
    /// <summary>
    /// Class for checking for updates to the agent.
    /// </summary>
    public class Updater
    {
        #region Properties
        /// <summary>
        /// Gets the collection of files available to be updated.
        /// </summary>
        public IDictionary<string, string> Files
        {
            get { return _files; }
        }

        /// <summary>
        /// Gets a value indicating whether or not the updater is running.
        /// </summary>
        public bool IsRunning
        {
            get { return _isRunning; }
        }
        #endregion

        #region Constructor
        /// <summary>
        /// Initialises a new instance of the <see cref="Updater"/> class.
        /// </summary>
        public Updater()
        {
        }
        #endregion

        /// <summary>
        /// Starts the background updater process.
        /// </summary>
        public void Start()
        {
            _isRunning = true;
            _thread = new Thread(new ThreadStart(Run));
            _thread.Name = "Server Density Agent Updater";
            _thread.Start();
        }

        /// <summary>
        /// Stops the background updater process.
        /// </summary>
        public void Stop()
        {
            try
            {
                _isRunning = false;
                _thread.Join(0);
                _thread.Abort();
            }
            catch (ThreadAbortException)
            {
            }
        }

        private void Run()
        {
            while (_isRunning)
            {
                try
                {
                    if (CheckMinRunTimeVersion())
                    {
                        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(DefaultUrl);
                        Log.DebugFormat("Retrieving response from {0}...", DefaultUrl);
                        HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                        Log.Debug("done.");
                        using (Stream stream = response.GetResponseStream())
                        {
                            using (StreamReader reader = new StreamReader(stream))
                            {
                                string data = reader.ReadToEnd();
                                Log.Debug(data);
                                Log.DebugFormat("Converting JSON...");
                                Dictionary<string, object> json = (Dictionary<string, object>)JsonConvert.DeserializeObject(data, typeof(Dictionary<string, object>));
                                Log.Debug("done.");
                                IDictionary<string, string> files = ProcessUpdates(json);
                                if (files != null)
                                {
                                    _files = files;
                                    OnUpdatesDetected(EventArgs.Empty);
                                }
                                else
                                {
                                    OnNoUpdatesDetected(EventArgs.Empty);
                                }
                            }
                        }
                        response.Close();
                    }
                    OnComplete(EventArgs.Empty);
                }
                catch (ThreadAbortException)
                {
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    OnError(EventArgs.Empty);
                }
                Thread.Sleep(UpdateInterval);
            }
        }

        private IDictionary<string, string> ProcessUpdates(Dictionary<string, object> updates)
        {
            IDictionary<string, string> files = new Dictionary<string, string>();
            Version newVersion = new Version((string)updates["version"]);
            Version installedVersion = GetInstalledVersion();
            if (newVersion <= installedVersion)
            {
                // No updates.
                return null;
            }
            // reset installed version
            _installedVersion = null;
            JContainer container = (JContainer)updates["files"];
            JToken file = container.First;
            while (file != null)
            {
                string fileName = (string)file.First.Last;
                string md5 = (string)file.Last.Last;
                files.Add(fileName, md5);
                file = file.Next;
            }
            return files;
        }

        private Version _installedVersion = null;
        private Version GetInstalledVersion()
        {
            if (_installedVersion == null)
            {
                // We must use ReadAllBytes due to the fact that LoadFile locks the assembly,
                // which causes problems when overwriting it later.
                byte[] file = File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BoxedIce.ServerDensity.Agent.exe"));
                Assembly asm = Assembly.Load(file);
                _installedVersion = asm.GetName().Version;
                file = null;
                asm = null;
                GC.Collect();
            }
            return _installedVersion;
        }

        private bool CheckMinRunTimeVersion()
        {
            string versions = "";
            try
            {
                Microsoft.Win32.RegistryKey key;
                key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Internet Settings\5.0\User Agent\Post Platform");
                string[] keys = key.GetValueNames();
                key.Close();
                versions = "";
                foreach (string k in keys)
                {
                    if (k.ToLower().StartsWith(".net"))
                    {
                        versions += k + ",";
                    }
                }
                versions = versions.TrimEnd(new char[] { ',' });
            }
            catch (Exception)
            {
                // do nothing
            }
            if (versions.Contains("3.5") || versions.Contains("4.0"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        #region Event-raising methods
        private void OnError(EventArgs e)
        {
            if (Error == null)
            {
                return;
            }
            Error(this, e);
        }

        private void OnComplete(EventArgs e)
        {
            if (Complete == null)
            {
                return;
            }
            Complete(this, e);
        }

        private void OnUpdatesDetected(EventArgs e)
        {
            if (UpdatesDetected == null)
            {
                return;
            }
            UpdatesDetected(this, e);
        }

        private void OnNoUpdatesDetected(EventArgs e)
        {
            if (NoUpdatesDetected == null)
            {
                return;
            }
            NoUpdatesDetected(this, e);
        }


        #endregion

        private readonly static ILog Log = LogManager.GetLogger(typeof(Updater));
        private const string DefaultUrl = "http://www.serverdensity.com/agentupdatewindows/?mongodb";
        private const int UpdateInterval = 5 * 60 * 1000; // Every five minutes in milliseconds.

        private Thread _thread;
        private bool _isRunning = false;
        private IDictionary<string, string> _files;

        public event EventHandler Error;
        public event EventHandler Complete;
        public event EventHandler UpdatesDetected;
        public event EventHandler NoUpdatesDetected;
    }
}
