using System;
using System.Collections.Generic;
using System.Text;
using MongoDB.Driver;
using MongoDB.Bson;
using log4net;
using BoxedIce.ServerDensity.Agent.PluginSupport;

namespace BoxedIce.ServerDensity.Agent.Checks
{
    public class MongoDBCheck : ICheck
    {
        #region ICheck Members

        public string Key
        {
            get { return "mongoDB"; }
        }

        public virtual object DoCheck()
        {

            // connection to mongo db
            if (_connectionString.Contains("mongodb://"))
            {
                mongo = MongoServer.Create(string.Format("{0}{1}?slaveok=true", _connectionString, _connectionString.EndsWith("/") ? "" : "/"));
            }
            else
            {
                MongoServerSettings settings = new MongoServerSettings();
                if (_connectionString.Contains(":"))
                {
                    string[] bits = _connectionString.Split(':');
                    settings.Server = new MongoServerAddress(bits[0], Convert.ToInt32(bits[1]));
                }
                else
                {
                    settings.Server = new MongoServerAddress(_connectionString);
                }
                settings.SlaveOk = true;
                mongo = MongoServer.Create(settings);
            }

            // vars to contain result of status check
            CommandResult commandResults = null;
            BsonDocument serverStatus;
            
            // make a status check call
            try
            {
                mongo.Connect();
                MongoDatabase database = mongo[DatabaseName];
                commandResults = database.RunCommand("serverStatus");
                serverStatus = commandResults.Response;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return null;
            }
            finally
            {
                // disconnect
                if (mongo != null)
                {
                    mongo.Disconnect();
                }

            }

            if (commandResults == null)
            {
                Log.Warn("MongoDB returned no results for serverStatus command.");
                Log.Warn("This is possible on older versions of MongoDB.");
                return null;
            }

            BsonDocument indexCounters = serverStatus["indexCounters"].AsBsonDocument;
            BsonDocument btree = null;

            // Index counters are currently not supported on Windows.
            if (indexCounters["note"] == null)
            {
                btree = indexCounters["btree"].AsBsonDocument;
            }
            else
            {
                // We add a blank document, since the server is expecting
                // these btree index values to be present.
                btree = new BsonDocument();
                indexCounters.Add("btree", btree);
                btree.Add("accesses", 0);
                btree.Add("accessesPS", 0);
                btree.Add("hits", 0);
                btree.Add("hitsPS", 0);
                btree.Add("misses", 0);
                btree.Add("missesPS", 0);
                btree.Add("missRatio", 0D);
                btree.Add("missRatioPS", 0);
            }

            BsonDocument opCounters = serverStatus["opcounters"].AsBsonDocument;
            BsonDocument asserts = serverStatus["asserts"].AsBsonDocument;

            if (_mongoDBStore == null)
            {
                Log.Debug("No cached data, so storing for the first time.");

                btree["accessesPS"] = 0;
                btree["hitsPS"] = 0;
                btree["missesPS"] = 0;
                btree["missRatioPS"] = 0;

                opCounters.Add("insertPS", 0);
                opCounters.Add("queryPS", 0);
                opCounters.Add("updatePS", 0);
                opCounters.Add("deletePS", 0);
                opCounters.Add("getmorePS", 0);
                opCounters.Add("commandPS", 0);

                asserts.Add("regularPS", 0);
                asserts.Add("warningPS", 0);
                asserts.Add("msgPS", 0);
                asserts.Add("userPS", 0);
                asserts.Add("rolloversPS", 0);
            }
            else
            {
                Log.Debug("Cached data exists, so calculating per sec metrics.");

                BsonDocument cachedBtree = (BsonDocument)((BsonDocument)_mongoDBStore["indexCounters"])["btree"];
                BsonDocument cachedOpCounters = (BsonDocument)_mongoDBStore["opcounters"];
                BsonDocument cachedAsserts = serverStatus["asserts"].AsBsonDocument;

                btree["accessesPS"] = (float)(((int)btree["accesses"] - (int)cachedBtree["accesses"]) / 60);
                btree["hitsPS"] = (float)(((int)btree["hits"] - (int)cachedBtree["hits"]) / 60);
                btree["missesPS"] = (float)(((int)btree["misses"] - (int)cachedBtree["misses"]) / 60);
                btree["missRatioPS"] = (float)(((double)btree["missRatio"] - (double)cachedBtree["missRatio"]) / 60);

                opCounters.Add("insertPS", (float)(((int)opCounters["insert"] - (int)cachedOpCounters["insert"]) / 60));
                opCounters.Add("queryPS", (float)(((int)opCounters["query"] - (int)cachedOpCounters["query"]) / 60));
                opCounters.Add("updatePS", (float)(((int)opCounters["update"] - (int)cachedOpCounters["update"]) / 60));
                opCounters.Add("deletePS", (float)(((int)opCounters["delete"] - (int)cachedOpCounters["delete"]) / 60));
                opCounters.Add("getmorePS", (float)(((int)opCounters["getmore"] - (int)cachedOpCounters["getmore"]) / 60));
                opCounters.Add("commandPS", (float)(((int)opCounters["command"] - (int)cachedOpCounters["command"]) / 60));

                asserts.Add("regularPS", (float)(((int)asserts["regular"] - (int)cachedAsserts["regular"]) / 60));
                asserts.Add("warningPS", (float)(((int)asserts["warning"] - (int)cachedAsserts["warning"]) / 60));
                asserts.Add("msgPS", (float)(((int)asserts["msg"] - (int)cachedAsserts["msg"]) / 60));
                asserts.Add("userPS", (float)(((int)asserts["user"] - (int)cachedAsserts["user"]) / 60));
                asserts.Add("rolloversPS", (float)(((int)asserts["rollovers"] - (int)cachedAsserts["rollovers"]) / 60));
            }

            _mongoDBStore = serverStatus.ToDictionary();

            return _mongoDBStore;
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initialises a new instance of the <see cref="MongoDBCheck"/> class with the provided values.
        /// </summary>
        /// <param name="connectionString">The connection string of the MongoDB instance to check.</param>
        public MongoDBCheck(string connectionString)
        {
            _connectionString = connectionString;
        }

        #endregion

        protected readonly string _connectionString;
        private MongoServer mongo;
        private IDictionary<string, object> _mongoDBStore;
        private const string DatabaseName = "local";
        private readonly static ILog Log = LogManager.GetLogger(typeof(MongoDBCheck));
    }
}
