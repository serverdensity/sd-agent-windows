using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Text;
using log4net;
using BoxedIce.ServerDensity.Agent.PluginSupport;

namespace BoxedIce.ServerDensity.Agent.Checks
{
    public class PluginCheck : ICheck
    {
        #region ICheck Members

        public string Key
        {
            get { return "plugins"; }
        }

        public object DoCheck()
        {
            IDictionary<string, object> values = new Dictionary<string, object>();
            foreach (ICheck check in _pluginChecks)
            {
                values.Add(check.Key, check.DoCheck());
            }
            return values;
        }

        #endregion

        public PluginCheck(string directory)
        {
            _pluginChecks = LoadPlugins(directory);
        }

        private ICheck[] LoadPlugins(string directory)
        {
            ArrayList checks = new ArrayList();
            string[] assemblies = null;
            try
            {
                assemblies = Directory.GetFiles(directory, "*.dll");
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }

            foreach (string file in assemblies)
            {
                Log.DebugFormat("Loading {0}...", file);
                Assembly asm = Assembly.LoadFrom(file);
                Log.Debug("done.");
                foreach (Type type in asm.GetTypes())
                {
                    if (type.GetInterface("ICheck") != null)
                    {
                        ICheck check = Activator.CreateInstance(type) as ICheck;
                        if (check == null)
                        {
                            continue;
                        }

                        Log.DebugFormat("Adding check type {0}...", check);
                        checks.Add(check);
                        Log.Debug("done.");
                    }
                }
            }
            return (ICheck[])checks.ToArray(typeof(ICheck));
        }

        private ICheck[] _pluginChecks;
        private readonly static ILog Log = LogManager.GetLogger(typeof(PluginCheck));
    }
}
