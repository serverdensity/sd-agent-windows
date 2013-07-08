using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Configuration;

namespace BoxedIce.ServerDensity.Agent
{
    /// <summary>
    /// Helper class for managing the configuration file for the monitoring agent
    /// Windows service.
    /// </summary>
    public class AgentConfiguration
    {
        /// <summary>
        /// Gets or sets the URL.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the agent key.
        /// </summary>
        public string AgentKey { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not IIS checks are to
        /// be performed.
        /// </summary>
        public bool IISChecks { get; set; }

        /// <summary>
        /// Gets or sets the directory to search in for plugins.
        /// </summary>
        public string PluginDirectory { get; set; }

        /// <summary>
        /// Gets or sets the connection string for MongoDB monitoring.
        /// </summary>
        public string MongoDBConnectionString { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not to get advanced
        /// database stats.
        /// </summary>
        public bool MongoDBDBStats { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not to get advanced
        /// replica set stats.
        /// </summary>
        public bool MongoDBReplSet { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not to perform 
        /// SQL Server checks.
        /// </summary>
        public bool SQLServerChecks { get; set; }

        /// <summary>
        /// Gets or sets the custom performance object prefix for SQL Server
        /// performance counters.
        /// </summary>
        public string CustomPrefix { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not to log errors to the 
        /// event viewer.
        /// </summary>
        public bool EventViewer { get; set; }

        /// <summary>
        /// Initialises a new instance of the <see cref="AgentConfiguration"/> class
        /// with the provided values.
        /// </summary>
        /// <param name="url">The URL.</param>
        /// <param name="agentKey">The agent key.</param>
        /// <param name="iisChecks">A value indicating whether or not to perform IIS checks.</param>
        /// <param name="pluginDirectory">A directory to search in for plugins.</param>
        /// <param name="mongoDBConnectionString">A connection string for MongoDB monitoring.</param>
        /// <param name="mongoDBDBStats"></param>
        /// <param name="mongoDBReplSet"></param>
        /// <param name="sqlServerChecks"></param>
        /// <param name="customPrefix"></param>
        /// <param name="eventViewer"></param>
        public AgentConfiguration(string url, string agentKey, bool iisChecks, string pluginDirectory, string mongoDBConnectionString, bool mongoDBDBStats, bool mongoDBReplSet, bool sqlServerChecks, string customPrefix, bool eventViewer)
        {
            Url = url;
            AgentKey = agentKey;
            IISChecks = iisChecks;
            PluginDirectory = pluginDirectory;
            MongoDBConnectionString = mongoDBConnectionString;
            MongoDBDBStats = mongoDBDBStats;
            MongoDBReplSet = mongoDBReplSet;
            SQLServerChecks = sqlServerChecks;
            CustomPrefix = customPrefix;
            EventViewer = eventViewer;
        }

        /// <summary>
        /// Writes the configuration file.
        /// </summary>
        public void Write()
        {
            string config = EventViewer ? DefaultConfig : ConfigWithoutEventViewer;
            XmlDocument xml = new XmlDocument();
            if (File.Exists(ConfigFile))
            {
                xml.Load(ConfigFile);
                xml["configuration"]["agent"].SetAttribute("url", Url);
                xml["configuration"]["agent"].SetAttribute("key", AgentKey);
                xml["configuration"]["agent"].SetAttribute("iisStatus", IISChecks.ToString());
                xml["configuration"]["agent"].SetAttribute("pluginDirectory", PluginDirectory);
                xml["configuration"]["agent"].SetAttribute("mongoDBConnectionString", MongoDBConnectionString);
                xml["configuration"]["agent"].SetAttribute("mongoDBDBStats", MongoDBDBStats.ToString());
                xml["configuration"]["agent"].SetAttribute("mongoDBReplSet", MongoDBReplSet.ToString());
                xml["configuration"]["agent"].SetAttribute("sqlServerStatus", SQLServerChecks.ToString());
                xml["configuration"]["agent"].SetAttribute("customPrefix", CustomPrefix);
                xml["configuration"]["agent"].SetAttribute("eventViewer", EventViewer.ToString());

                if (EventViewer)
                {
                    var node = xml["configuration"]["log4net"]["root"].SelectSingleNode("./appender-ref");
                    if (node != null)
                    {
                        try
                        {
                            node.Attributes["ref"].Value = "EventLog";
                        }
                        catch
                        {
                            var x = xml.CreateAttribute("ref");
                            x.Value = "EventLog";
                            node.Attributes.Append(x);
                        }
                    }
                    else
                    {
                        var nodeToAdd = xml.CreateNode(XmlNodeType.Element, "appender-ref", xml.NamespaceURI);
                        var refAttribute = xml.CreateAttribute("ref");
                        refAttribute.Value = "EventLog";
                        nodeToAdd.Attributes.Append(refAttribute);
                        xml["configuration"]["log4net"]["root"].AppendChild(nodeToAdd);
                    }
                }
                else
                {
                    var node = xml["configuration"]["log4net"]["root"].SelectSingleNode("./appender-ref");
                    if (node != null)
                    {
                        xml["configuration"]["log4net"]["root"].RemoveChild(node);
                    }
                }

                XmlWriterSettings xmlSettings = new XmlWriterSettings();
                xmlSettings.Indent = true;
                using (XmlWriter writer = XmlWriter.Create(ConfigFile, xmlSettings))
                {
                    xml.WriteTo(writer);
                }
            }
        }

        /// <summary>
        /// Reads the configuration file.
        /// </summary>
        public static AgentConfiguration Load()
        {
            XmlDocument xml = new XmlDocument();

            if (File.Exists(ConfigFile))
            {

                try
                {
                    xml.Load(ConfigFile);
                    XmlNode node = xml.SelectSingleNode("//agent");
                    string mongoDBConnectionString = null;
                    bool mongoDBDBStats = false;
                    bool mongoDBReplSet = false;
                    bool sqlServerStatus = false;
                    string customPrefix = null;
                    bool eventViewer = true;

                    if (node.Attributes["mongoDBConnectionString"] != null)
                    {
                        mongoDBConnectionString = node.Attributes["mongoDBConnectionString"].InnerText;
                    }

                    if (node.Attributes["mongoDBDBStats"] != null)
                    {
                        mongoDBDBStats = Convert.ToBoolean(node.Attributes["mongoDBDBStats"].InnerText);
                    }

                    if (node.Attributes["mongoDBReplSet"] != null)
                    {
                        mongoDBReplSet = Convert.ToBoolean(node.Attributes["mongoDBReplSet"].InnerText);
                    }

                    if (node.Attributes["sqlServerStatus"] != null)
                    {
                        sqlServerStatus = Convert.ToBoolean(node.Attributes["sqlServerStatus"].InnerText);
                    }

                    if (node.Attributes["customPrefix"] != null)
                    {
                        customPrefix = node.Attributes["customPrefix"].InnerText;
                    }

                    if (node.Attributes["eventViewer"] != null)
                    {
                        eventViewer = Convert.ToBoolean(node.Attributes["eventViewer"].InnerText);
                    }

                    return new AgentConfiguration(
                        node.Attributes["url"].InnerText,
                        node.Attributes["key"].InnerText,
                        Convert.ToBoolean(node.Attributes["iisStatus"].InnerText),
                        node.Attributes["pluginDirectory"].InnerText,
                        mongoDBConnectionString,
                        mongoDBDBStats,
                        mongoDBReplSet,
                        sqlServerStatus,
                        customPrefix,
                        eventViewer);
                }
                catch
                {
                }

            }

            return new AgentConfiguration("http://example.serverdensity.com", string.Empty, false, null, null, false, false, false, null, true);
        }

        private static readonly string ConfigFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BoxedIce.ServerDensity.Agent.WindowsService.exe.config");
        private readonly string DefaultConfig = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <configSections>
    <section name=""agent"" type=""BoxedIce.ServerDensity.Agent.AgentConfigurationSection, BoxedIce.ServerDensity.Agent"" />
    <section name=""log4net"" type=""log4net.Config.Log4NetConfigurationSectionHandler, log4net"" />
  </configSections>
  <agent
    url=""{0}""
    key=""{1}""
    iisStatus=""{2}""
    pluginDirectory=""{3}""
    mongoDBConnectionString=""{4}""
    mongoDBDBStats=""{5}""
    mongoDBReplSet=""{6}""
    sqlServerStatus=""{7}""
    customPrefix=""{8}""
    eventViewer=""{9}""
   />
   <log4net>
    <appender name=""EventLog"" type=""log4net.Appender.EventLogAppender"" >
      <param name=""ApplicationName"" value=""Server Density"" />
      <layout type=""log4net.Layout.PatternLayout"">
        <param name=""ConversionPattern"" value=""%m"" />
      </layout>
    </appender>
    <root>
      <level value=""WARN"" />
      <appender-ref ref=""EventLog"" />
    </root>
  </log4net>
</configuration>";
        private readonly string ConfigWithoutEventViewer = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>
  <configSections>
    <section name=""agent"" type=""BoxedIce.ServerDensity.Agent.AgentConfigurationSection, BoxedIce.ServerDensity.Agent"" />
    <section name=""log4net"" type=""log4net.Config.Log4NetConfigurationSectionHandler, log4net"" />
  </configSections>
  <agent
    url=""{0}""
    key=""{1}""
    iisStatus=""{2}""
    pluginDirectory=""{3}""
    mongoDBConnectionString=""{4}""
    mongoDBDBStats=""{5}""
    mongoDBReplSet=""{6}""
    sqlServerStatus=""{7}""
    customPrefix=""{8}""
    eventViewer=""{9}""
   />
   <log4net />
</configuration>";
    }
}
