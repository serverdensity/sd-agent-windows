using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using System.Text;
using System.Configuration;
using BoxedIce.ServerDensity.Agent.Checks;
using BoxedIce.ServerDensity.Agent.PluginSupport;
using log4net;

namespace BoxedIce.ServerDensity.Agent.WindowsService
{
    public class AgentService : ServiceBase
    {

        protected override void OnStart(string[] args)
        {
            try
            {
                var config = (AgentConfigurationSection)ConfigurationManager.GetSection("agent");
                _agent = new Agent(config);
                _agent.Run();
                base.OnStart(args);
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

        }

        protected override void OnStop()
        {
            _agent.Stop();
            base.OnStop();
        }

        public void ConsoleStart()
        {
            OnStart(null);
        }

        public void ConsoleStop()
        {
            OnStop();
        }

        private Agent _agent;
        private static readonly ILog Log = LogManager.GetLogger(typeof(AgentService));
    }
}
