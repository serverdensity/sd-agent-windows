﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Management.Instrumentation;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using BoxedIce.ServerDensity.Agent.PluginSupport;
using log4net;

namespace BoxedIce.ServerDensity.Agent.Checks
{
    /// <summary>
    /// Class for checking process running.
    /// </summary>
    public class ProcessCheck : ICheck
    {
        #region ICheck Members

        public string Key
        {
            get { return "processes"; }
        }

        public ProcessCheck()
        {
            _totalMemory = TotalMemory();
        }

        public virtual object DoCheck()
        {
            var processStats = ProcessStats();
            var results = new ArrayList();
            using (var query = new ManagementObjectSearcher("SELECT * FROM Win32_Process"))
            {
                foreach (ManagementObject process in query.Get())
                {
                    try
                    {
                        var processId = (uint)process.GetPropertyValue("ProcessId");
                        var imageName = (string)process.GetPropertyValue("Name");

                        // Ignore System Idle Process for now
                        if (imageName.ToString().ToLower() == "system idle process")
                        {
                            continue;
                        }

                        var fullUserName = string.Empty;
                        var outParameters = process.InvokeMethod("GetOwner", null, null);
                        if (outParameters["User"] != null)
                        {
                            fullUserName = string.Format(@"{0}\{1}", outParameters["Domain"], outParameters["User"]);
                        }

                        var stats = processStats[processId];
                        var cpuPercentage = stats[0];
                        ulong workingSet = stats[1];
                        decimal totalMemory = (decimal)_totalMemory;

                        decimal memoryPercentage = 0;
                        if (totalMemory > 0)
                        {
                            memoryPercentage = Decimal.Round(((decimal)workingSet / totalMemory * 100), 2);
                        }

                        results.Add(new object[] { processId, imageName, fullUserName, cpuPercentage, memoryPercentage, workingSet });

                        // flag check
                        if (Agent.Flags.ContainsKey("ProcessCheck"))
                        {
                            if (imageName == Agent.Flags["ProcessCheck"])
                            {
                                bool perf = PerformanceCounterBasedProcessCheck.IsProcessRunning(imageName);
                                if (!perf)
                                {
                                    Log.Error("Process Check: '" + Agent.Flags["ProcessCheck"] + "' process does not show in Perf Counters.");
                                }
                            }
                        }

                    }
                    catch (ManagementException ex)
                    {
                        // Process could have ended before reaching this point in the loop
                        if (ex.Message.ToLower() == "not found")
                        {
                            continue;
                        }
                    }
                }

                return results;
            }
        }

        #endregion

        private Dictionary<uint, ulong[]> ProcessStats()
        {
            var processStats = new Dictionary<uint, ulong[]>();
            // IDProcess is not necessarily the ProcessId of a process, but seems to work
            using (var query = new ManagementObjectSearcher("SELECT IDProcess, PercentProcessorTime, WorkingSet FROM Win32_PerfFormattedData_PerfProc_Process"))
            {
                foreach (ManagementObject obj in query.Get())
                {
                    using (obj)
                    {
                        var key = (uint)obj.GetPropertyValue("IDProcess");
                        if (processStats.ContainsKey(key))
                        {
                            processStats[key] = new ulong[] { (ulong)obj.GetPropertyValue("PercentProcessorTime"), (ulong)obj.GetPropertyValue("WorkingSet") };
                        }
                    }
                }
                return processStats;
            }
        }

        protected virtual ulong TotalMemory()
        {
            try
            {
                using (var query = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize FROM Win32_OperatingSystem"))
                {
                    foreach (var obj in query.Get())
                    {
                        using (obj)
                        {
                            return (ulong)obj.GetPropertyValue("TotalVisibleMemorySize") * 1024;
                        }
                    }
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            return 0;
        }

        protected readonly ulong _totalMemory;
        private static readonly ILog Log = LogManager.GetLogger(typeof(ProcessCheck));

        public static bool IsProcessRunning(string processName)
        {
            bool found = false;
            using (var query = new ManagementObjectSearcher("SELECT * FROM Win32_Process"))
            {
                foreach (ManagementObject process in query.Get())
                {
                    if (processName == (string)process.GetPropertyValue("Name"))
                        found = true;
                }
            }
            return found;
        }
    }
}
