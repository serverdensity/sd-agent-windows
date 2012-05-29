using System;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Threading;
using System.Security.Principal;
using BoxedIce.ServerDensity.Agent.Checks;
using BoxedIce.ServerDensity.Agent.PluginSupport;
using log4net;

namespace BoxedIce.ServerDensity.Agent
{
    /// <summary>
    /// The agent.
    /// </summary>
    public class Agent
    {
        /// <summary>
        /// Gets or sets the list of checks for the agent.
        /// </summary>
        public IList<ICheck> Checks
        {
            get { return _checks; }
            set { _checks = value; }
        }

        /// <summary>
        /// Gets or sets the network traffic check for the agent.
        /// </summary>
        /// <remarks>
        /// This is kept around in order to cache network usage over
        /// the lifetime of the agent process.
        /// </remarks>
        public NetworkTrafficCheck NetworkTrafficCheck
        {
            get { return _networkTrafficCheck; }
            set { _networkTrafficCheck = value; }
        }

        /// <summary>
        /// Main method; used when running the agent as a standalone console EXE.
        /// </summary>
        public static void Main()
        {
            var config = (AgentConfigurationSection)ConfigurationManager.GetSection("agent");
            var agent = new Agent(config);

            agent.Run();
            Console.ReadLine();
            agent.Stop();
        }

        /// <summary>
        /// Initialises a new instance of the <see cref="Agent"/>.
        /// </summary>
        /// <param name="config">The agent configuration.</param>
        public Agent(AgentConfigurationSection config)
        {
            _config = config;
            InitializeFlags();
            InitializeConfig();
            CheckEnvironment();
        }

        /// <summary>
        /// Runs the agent.
        /// </summary>
        public void Run()
        {
            _thread = new Thread(new ThreadStart(Start));
            _thread.Start();
            Log.Info("Thread started");
        }

        private void InitializeConfig()
        {

            try
            {

                Checks.Add(new SystemStatsCheck());

                _networkTrafficCheck = new NetworkTrafficCheck();

                Checks.Add(_networkTrafficCheck);
                Checks.Add(new DriveInfoBasedDiskUsageCheck());
                Checks.Add(new ProcessorCheck());
                Checks.Add(new ProcessCheck());
                Checks.Add(new PhysicalMemoryFreeCheck());
                Checks.Add(new PhysicalMemoryUsedCheck());
                Checks.Add(new PhysicalMemoryCachedCheck());
                Checks.Add(new SwapMemoryFreeCheck());
                Checks.Add(new SwapMemoryUsedCheck());

                if (_config.IISStatus)
                {
                    Checks.Add(new IISCheck());
                }

                if (_config.PluginDirectory != null && Directory.Exists(_config.PluginDirectory))
                {
                    Checks.Add(new PluginCheck(_config.PluginDirectory));
                }

                if (_config.SQLServerStatus)
                {
                    Checks.Add(new SQLServerCheck(_config.CustomPrefix));
                }

                if (!string.IsNullOrEmpty(_config.MongoDBConnectionString))
                {
                    Checks.Add(new ExtendedMongoDBCheck(_config.MongoDBConnectionString, _config.MongoDBReplSet, _config.MongoDBDBStats));
                }

                // flag check
                if (Agent.Flags.ContainsKey("FlagCheck"))
                {
                    Log.Warn("Flag check activated");
                }
            
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

        }

        private void InitializeFlags()
        {
            string flagsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flags.xml");
            if (File.Exists(flagsFile))
            {
                try
                {
                    XmlDocument flags = new XmlDocument();
                    flags.Load(flagsFile);
                    foreach (XmlNode flag in flags["flags"])
                    {
                        Flags.Add(flag.Attributes["name"].Value, flag.InnerText);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn("Error loading flags", ex);
                }
            }
        }

        private void Start()
        {
            while (!_isStopped)
            {
                DoChecks();
                RemoveSystemStatsCheck();
                Log.InfoFormat("Checks complete.  Sleeping for {0} seconds", _config.CheckInterval / 1000);
                Thread.Sleep(_config.CheckInterval);
            }
        }

        private void CheckEnvironment()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                Log.Warn("Service not running as Administrator.");
            }
        }

        private void RemoveSystemStatsCheck()
        {
            if (Checks.Count > 0 && Checks[0].GetType() == typeof(SystemStatsCheck))
            {
                Checks.RemoveAt(0);
                Log.Info("Removed system stats check after first run");
            }
        }

        /// <summary>
        /// Stops the agent.
        /// </summary>
        public void Stop()
        {
            _isStopped = true;
            _thread.Join(0);
            try
            {
                _thread.Abort();
            }
            catch (ThreadAbortException)
            {
            }
        }

        /// <summary>
        /// Performs all checks.
        /// </summary>
        private void DoChecks()
        {
            var results = new Dictionary<string, object>();

            foreach (var check in Checks)
            {
                Log.DebugFormat("{0}: start", check.GetType());
                try
                {
                    var result = check.DoCheck();
                    if (result != null)
                    {
                        results.Add(check.Key, result);
                        Log.DebugFormat("{0}: end", check.GetType());
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }

            // Post metrics to Server Density servers.
            try
            {
                var poster = new PayloadPoster(_config, results);
                poster.Post();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        private bool _isStopped;
        private Thread _thread;
        private IList<ICheck> _checks = new List<ICheck>();
        private readonly AgentConfigurationSection _config;
        private NetworkTrafficCheck _networkTrafficCheck;
        /// <summary>
        /// The list of all available <see cref="ICheck"/> instances 
        /// for the agent.
        /// </summary>
        private static ILog Log = LogManager.GetLogger(typeof(Agent));
        public static Hashtable Flags = new Hashtable();
    }
}
