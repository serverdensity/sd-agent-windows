using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using log4net;
using Newtonsoft.Json;
using System.Reflection;

namespace BoxedIce.ServerDensity.Agent
{
    /// <summary>
    /// Class to POST the agent payload data to the Server Density servers.
    /// </summary>
    public class PayloadPoster
    {
        /// <summary>
        /// Initialises a new instance of the PayloadPoster class with the 
        /// provided values.
        /// </summary>
        /// <param name="results">The payload dictionary.</param>
        public PayloadPoster(AgentConfigurationSection config, IDictionary<string, object> results)
        {
            _config = config;
            _results = results;
            _results.Add("os", "windows");
            _results.Add("agentKey", _config.AgentKey);

            try
            {
                _results.Add("internalHostname", Environment.MachineName);
            }
            catch (InvalidOperationException)
            {
            }

            if (_version == null)
            {
                Assembly asm = Assembly.Load(File.ReadAllBytes(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BoxedIce.ServerDensity.Agent.exe")));
                Version installedVersion = asm.GetName().Version;
                _version = installedVersion.ToString();
            }

            _results.Add("agentVersion", _version);
        }

        /// <summary>
        /// Creates and sends the HTTP POST.
        /// </summary>
        public void Post()
        {
            var payload = JsonConvert.SerializeObject(_results);
            var hash = MD5Hash(payload);

            // TODO: this is for quick testing; we'll need to add proxy 
            //       settings, read the response, etc.
            using (var client = new WebClient())
            {
                var data = new NameValueCollection();
                data.Add("payload", payload);
                Log.Debug(payload);
                data.Add("hash", hash);
                var url = string.Format("{0}{1}postback/", _config.ServerDensityUrl, _config.ServerDensityUrl.EndsWith("/") ? "" : "/");
                Log.InfoFormat("Posting to {0}", url);

                if (HttpWebRequest.DefaultWebProxy != null)
                {
                    client.Proxy = HttpWebRequest.DefaultWebProxy;
                }

                byte[] response = client.UploadValues(url, "POST", data);
                string responseText = Encoding.ASCII.GetString(response);

                if (responseText != "OK" && responseText != "\"OK\"")
                {
                    Log.ErrorFormat("URL {0} returned: {1}", url, responseText);
                }

                Log.Debug(responseText);
            }
        }

        private static string MD5Hash(string input)
        {
            MD5CryptoServiceProvider x = new MD5CryptoServiceProvider();
            byte[] bs = Encoding.UTF8.GetBytes(input);
            bs = x.ComputeHash(bs);
            StringBuilder s = new StringBuilder();

            foreach (byte b in bs)
            {
                s.Append(b.ToString("x2").ToLower());
            }

            return s.ToString();
        }

        private IDictionary<string, object> _results;
        private readonly AgentConfigurationSection _config;
        private static string _version;
        private readonly static ILog Log = LogManager.GetLogger(typeof(PayloadPoster));
    }
}
