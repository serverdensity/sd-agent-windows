﻿using System;
using System.Collections.Generic;
using System.Text;
using log4net;
using MongoDB.Driver;
using MongoDB.Bson;
using BoxedIce.ServerDensity.Agent.PluginSupport;

namespace BoxedIce.ServerDensity.Agent.Checks
{
    public class ExtendedMongoDBCheck : ICheck
    {
        public string Key
        {
            get { return "mongoDB"; }
        }

        public ExtendedMongoDBCheck(string connectionString, bool isReplSet, bool dbStats)
        {
            _isReplSet = isReplSet;
            _dbStats = dbStats;

            if (connectionString.Contains("mongodb://"))
            {
                _mongoServer = MongoServer.Create(string.Format("{0}{1}?slaveok=true", connectionString, connectionString.EndsWith("/") ? "" : "/"));
            }
            else
            {
                MongoServerSettings settings = new MongoServerSettings();
                if (connectionString.Contains(":"))
                {
                    string[] bits = connectionString.Split(':');
                    settings.Server = new MongoServerAddress(bits[0], Convert.ToInt32(bits[1]));
                }
                else
                {
                    settings.Server = new MongoServerAddress(connectionString);
                }
                settings.SlaveOk = true;
                _mongoServer = MongoServer.Create(settings);
            }
        }

        public object DoCheck()
        {
            _mongoServer.Connect();

            MongoDatabase database = _mongoServer["admin"];
            CommandResult result = database.RunCommand("serverStatus");
            BsonDocument statusOutput = result.Response;
            IDictionary<string, object> status = new Dictionary<string, object>();

            FillVersion(statusOutput, status);
            FillGlobalLockStatistics(statusOutput, status);
            FillMemoryStatistics(statusOutput, status);
            FillConnectionStatistics(statusOutput, status);
            FillExtraInfoStatistics(statusOutput, status);
            FillBackgroundFlushingStatistics(statusOutput, status);
            FillBaseStatistics(statusOutput, status);
            FillCursorStatistics(statusOutput, status);

            if (_isReplSet)
            {
                FillReplicaSetStatistics(status);
            }

            if (_dbStats)
            {
                FillDatabaseStatistics(status);
            }

            _mongoServer.Disconnect();
            return status;
        }

        private int[] GetVersion(BsonDocument statusOutput)
        {
            if (statusOutput.Contains("version"))
            {
                var splits = statusOutput["version"].AsString.Split('.');

                var retVal = new List<int>();
                foreach (var split in splits)
                {
                    retVal.Add(int.Parse(split));
                }

                return retVal.ToArray();
            }
            return null;
        }

        private void FillVersion(BsonDocument statusOutput, IDictionary<string, object> status)
        {
            if (statusOutput.Contains("version"))
            {
                status.Add("version", statusOutput["version"]);
            }
        }

        private void FillGlobalLockStatistics(BsonDocument statusOutput, IDictionary<string, object> status)
        {
            if (statusOutput.Contains("globalLock"))
            {
                BsonDocument globalLockOutput = (BsonDocument)statusOutput["globalLock"];
                IDictionary<string, object> globalLock = new Dictionary<string, object>();
                status.Add("globalLock", globalLock);

                var version = this.GetVersion(statusOutput);
                // mongodb over 2.2.0 doesn't have a global lock, so we can't report on it
                if (version != null && version[0] >= 2 && version[1] >= 2)
                {
                    globalLock.Add("ratio", 0);
                }
                else
                {
                    globalLock.Add("ratio", globalLockOutput["ratio"]);
                }

                BsonDocument currentQueueOutput = (BsonDocument)globalLockOutput["currentQueue"];
                IDictionary<string, object> currentQueue = new Dictionary<string, object>();
                globalLock.Add("currentQueue", currentQueue);
                currentQueue.Add("total", currentQueueOutput["total"]);
                currentQueue.Add("readers", currentQueueOutput["readers"]);
                currentQueue.Add("writers", currentQueueOutput["writers"]);
            }
        }

        private void FillMemoryStatistics(BsonDocument statusOutput, IDictionary<string, object> status)
        {
            if (statusOutput.Contains("mem"))
            {
                BsonDocument memOutput = (BsonDocument)statusOutput["mem"];
                IDictionary<string, object> mem = new Dictionary<string, object>();
                status.Add("mem", mem);
                mem.Add("resident", memOutput["resident"]);
                mem.Add("virtual", memOutput["virtual"]);
                mem.Add("mapped", memOutput["mapped"]);
            }
        }

        private void FillConnectionStatistics(BsonDocument statusOutput, IDictionary<string, object> status)
        {
            if (statusOutput.Contains("connections"))
            {
                BsonDocument connectionsOutput = (BsonDocument)statusOutput["connections"];
                IDictionary<string, object> connections = new Dictionary<string, object>();
                status.Add("connections", connections);
                connections.Add("current", connectionsOutput["current"]);
                connections.Add("available", connectionsOutput["available"]);
            }
        }

        private void FillExtraInfoStatistics(BsonDocument statusOutput, IDictionary<string, object> status)
        {
            if (statusOutput.Contains("extra_info"))
            {
                BsonDocument extraInfoOutput = (BsonDocument)statusOutput["extra_info"];
                IDictionary<string, object> extraInfo = new Dictionary<string, object>();
                status.Add("extraInfo", extraInfo);
                if (extraInfoOutput.Contains("heap_usage_bytes"))
                {
                    extraInfo.Add("heapUsage", extraInfoOutput["heap_usage_bytes"]);
                }
                if (extraInfoOutput.Contains("page_faults"))
                {
                    extraInfo.Add("pageFaults", extraInfoOutput["page_faults"]);
                }
            }
        }

        private void FillBackgroundFlushingStatistics(BsonDocument statusOutput, IDictionary<string, object> status)
        {
            if (statusOutput.Contains("backgroundFlushing"))
            {
                BsonDocument backgroundFlushingOutput = (BsonDocument)statusOutput["backgroundFlushing"];
                IDictionary<string, object> backgroundFlushing = new Dictionary<string, object>();
                status.Add("backgroundFlushing", backgroundFlushing);
                TimeSpan delta = DateTime.Now - (DateTime)backgroundFlushingOutput["last_finished"];
                backgroundFlushing.Add("secondsSinceLastFlush", delta.TotalSeconds);
                backgroundFlushing.Add("lastFlushLength", backgroundFlushingOutput["last_ms"]);
                backgroundFlushing.Add("flushLengthAvrg", backgroundFlushingOutput["average_ms"]);
            }
        }

        private void FillBaseStatistics(BsonDocument statusOutput, IDictionary<string, object> status)
        {
            BsonDocument indexCountersOutput = (BsonDocument)statusOutput["indexCounters"];
            IDictionary<string, object> indexCounters = new Dictionary<string, object>();
            status.Add("indexCounters", indexCounters);

            IDictionary<string, object> btree = new Dictionary<string, object>();
            indexCounters.Add("btree", btree);

            IDictionary<string, object> opCounters = new Dictionary<string, object>();
            status.Add("opcounters", opCounters);

            IDictionary<string, object> asserts = new Dictionary<string, object>();
            status.Add("asserts", asserts);

            BsonDocument btreeOutput = new BsonDocument();
            // Index counters are currently not supported on Windows.
            if (!indexCountersOutput.Contains("note") || indexCountersOutput["note"] == null)
            {
                btreeOutput = (BsonDocument)indexCountersOutput["btree"];
            }
            else
            {
                // We add a blank document, since the server is expecting
                // these btree index values to be present.
                btreeOutput.Add("accesses", 0);
                btreeOutput.Add("accessesPS", 0);
                btreeOutput.Add("hits", 0);
                btreeOutput.Add("hitsPS", 0);
                btreeOutput.Add("misses", 0);
                btreeOutput.Add("missesPS", 0);
                btreeOutput.Add("missRatio", 0D);
                btreeOutput.Add("missRatioPS", 0);
            }

            BsonDocument opCountersOutput = (BsonDocument)statusOutput["opcounters"];
            BsonDocument assertsOutput = (BsonDocument)statusOutput["asserts"];

            if (_mongoDBStore == null)
            {
                Log.Debug("No cached data, so storing for the first time.");

                btree.Add("accessesPS", 0);
                btree.Add("accesses", 0);
                btree.Add("hitsPS", 0);
                btree.Add("hits", 0);
                btree.Add("missesPS", 0);
                btree.Add("misses", 0);
                btree.Add("missRatioPS", 0);
                btree.Add("missRatio", 0D);

                opCounters.Add("insertPS", 0);
                opCounters.Add("insert", 0);
                opCounters.Add("queryPS", 0);
                opCounters.Add("query", 0);
                opCounters.Add("updatePS", 0);
                opCounters.Add("update", 0);
                opCounters.Add("deletePS", 0);
                opCounters.Add("delete", 0);
                opCounters.Add("getmorePS", 0);
                opCounters.Add("getmore", 0);
                opCounters.Add("commandPS", 0);
                opCounters.Add("command", 0);

                asserts.Add("regularPS", 0);
                asserts.Add("regular", 0);
                asserts.Add("warningPS", 0);
                asserts.Add("warning", 0);
                asserts.Add("msgPS", 0);
                asserts.Add("msg", 0);
                asserts.Add("userPS", 0);
                asserts.Add("user", 0);
                asserts.Add("rolloversPS", 0);
                asserts.Add("rollovers", 0);
            }
            else
            {
                Log.Debug("Cached data exists, so calculating per sec metrics.");

                IDictionary<string, object> cachedBtree = (IDictionary<string, object>)((IDictionary<string, object>)_mongoDBStore["indexCounters"])["btree"];
                IDictionary<string, object> cachedOpCounters = (IDictionary<string, object>)_mongoDBStore["opcounters"];
                IDictionary<string, object> cachedAsserts = (IDictionary<string, object>)_mongoDBStore["asserts"];

                btree.Add("accessesPS", (float)(((int)btreeOutput["accesses"] - (int)cachedBtree["accesses"]) / 60));
                btree.Add("accesses", btreeOutput["accesses"].RawValue);
                btree.Add("hitsPS", (float)(((int)btreeOutput["hits"] - (int)cachedBtree["hits"]) / 60));
                btree.Add("hits", btreeOutput["hits"].RawValue);
                btree.Add("missesPS", (float)(((int)btreeOutput["misses"] - (int)cachedBtree["misses"]) / 60));
                btree.Add("misses", btreeOutput["misses"].RawValue);
                btree.Add("missRatioPS", (float)(((double)btreeOutput["missRatio"] - (double)cachedBtree["missRatio"]) / 60));
                btree.Add("missRatio", btreeOutput["missRatio"].RawValue);

                opCounters.Add("insertPS", (float)(((int)opCountersOutput["insert"] - (int)cachedOpCounters["insert"]) / 60));
                opCounters.Add("insert", opCountersOutput["insert"].RawValue);
                opCounters.Add("queryPS", (float)(((int)opCountersOutput["query"] - (int)cachedOpCounters["query"]) / 60));
                opCounters.Add("query", opCountersOutput["query"].RawValue);
                opCounters.Add("updatePS", (float)(((int)opCountersOutput["update"] - (int)cachedOpCounters["update"]) / 60));
                opCounters.Add("update", opCountersOutput["update"].RawValue);
                opCounters.Add("deletePS", (float)(((int)opCountersOutput["delete"] - (int)cachedOpCounters["delete"]) / 60));
                opCounters.Add("delete", opCountersOutput["delete"].RawValue);
                opCounters.Add("getmorePS", (float)(((int)opCountersOutput["getmore"] - (int)cachedOpCounters["getmore"]) / 60));
                opCounters.Add("getmore", opCountersOutput["getmore"].RawValue);
                opCounters.Add("commandPS", (float)(((int)opCountersOutput["command"] - (int)cachedOpCounters["command"]) / 60));
                opCounters.Add("command", opCountersOutput["command"].RawValue);

                asserts.Add("regularPS", (float)(((int)assertsOutput["regular"] - (int)cachedAsserts["regular"]) / 60));
                asserts.Add("regular", assertsOutput["regular"].RawValue);
                asserts.Add("warningPS", (float)(((int)assertsOutput["warning"] - (int)cachedAsserts["warning"]) / 60));
                asserts.Add("warning", assertsOutput["warning"].RawValue);
                asserts.Add("msgPS", (float)(((int)assertsOutput["msg"] - (int)cachedAsserts["msg"]) / 60));
                asserts.Add("msg", assertsOutput["msg"].RawValue);
                asserts.Add("userPS", (float)(((int)assertsOutput["user"] - (int)cachedAsserts["user"]) / 60));
                asserts.Add("user", assertsOutput["user"].RawValue);
                asserts.Add("rolloversPS", (float)(((int)assertsOutput["rollovers"] - (int)cachedAsserts["rollovers"]) / 60));
                asserts.Add("rollovers", assertsOutput["rollovers"].RawValue);
            }

            _mongoDBStore = status;
        }

        private void FillCursorStatistics(BsonDocument statusOutput, IDictionary<string, object> status)
        {
            if (statusOutput.Contains("cursors"))
            {
                BsonDocument cursorsOutput = (BsonDocument)statusOutput["cursors"];
                IDictionary<string, object> cursors = new Dictionary<string, object>();
                status.Add("cursors", cursors);
                cursors.Add("totalOpen", cursorsOutput["totalOpen"]);
            }
        }

        private void FillReplicaSetStatistics(IDictionary<string, object> status)
        {
            MongoDatabase database = _mongoServer["admin"];
            CommandResult result = database.RunCommand("isMaster");
            BsonDocument isMasterOutput = result.Response;
            IDictionary<string, object> replSet = new Dictionary<string, object>();
            status.Add("replSet", replSet);

            replSet.Add("setName", isMasterOutput["setName"]);
            replSet.Add("isMaster", isMasterOutput["ismaster"]);
            replSet.Add("isSecondary", isMasterOutput["secondary"]);

            if (isMasterOutput.Contains("arbiterOnly"))
            {
                replSet.Add("isArbiter", isMasterOutput["arbiterOnly"]);
            }

            database = _mongoServer["admin"];
            result = database.RunCommand("replSetGetStatus");
            BsonDocument replSetOutput = result.Response;

            replSet.Add("myState", replSetOutput["myState"]);
            IDictionary<string, object> members = new Dictionary<string, object>();
            replSet.Add("members", members);

            foreach (BsonDocument memberOutput in replSetOutput["members"] as BsonArray)
            {
                IDictionary<string, object> member = new Dictionary<string, object>();
                members.Add(memberOutput["_id"].ToString(), member);

                member.Add("name", memberOutput["name"]);
                member.Add("state", memberOutput["state"]);

                if (memberOutput.Contains("optimeDate"))
                {
                    TimeSpan delta = DateTime.UtcNow - (DateTime)memberOutput["optimeDate"];
                    member.Add("optimeDate", (int)delta.TotalSeconds);
                }

                if (memberOutput.Contains("self"))
                {
                    replSet.Add("myId", memberOutput["_id"].ToString());
                }
                else
                {
                    if (memberOutput.Contains("lastHeartbeat"))
                    {
                        TimeSpan delta = DateTime.UtcNow - (DateTime)memberOutput["lastHeartbeat"];
                        member.Add("lastHeartbeat", (int)delta.TotalSeconds);
                    }
                }

                if (memberOutput.Contains("errmsg"))
                {
                    member.Add("error", memberOutput["errmsg"]);
                }
            }
        }

        private void FillDatabaseStatistics(IDictionary<string, object> status)
        {
            IDictionary<string, IDictionary<string, object>> dbStats = new Dictionary<string, IDictionary<string, object>>();
            status.Add("dbStats", dbStats);

            foreach (string dbName in _mongoServer.GetDatabaseNames())
            {
                if (dbName == "config" || dbName == "local" || dbName == "admin" || dbName == "test")
                {
                    // Ignore these database names.
                    continue;
                }

                MongoDatabase database = _mongoServer[dbName];
                CommandResult result = database.RunCommand("dbstats");
                BsonDocument dbStatsOutput = (BsonDocument)result.Response;
                MongoCollection collection = database["system.namespaces"];

                dbStats.Add(dbName, new Dictionary<string, object>());
                foreach (BsonElement element in dbStatsOutput.Elements)
                {
                    dbStats[dbName].Add(element.Name, element.Value);
                }

                try
                {
                    long namespaces = collection.Count();
                    dbStats[dbName].Add("namespaces", namespaces);
                }
                catch (MongoCommandException)
                {
                    dbStats[dbName].Add("namespaces", "");
                }
            }
        }

        private MongoServer _mongoServer;
        private IDictionary<string, object> _mongoDBStore;
        private readonly bool _isReplSet;
        private readonly bool _dbStats;
        private static readonly ILog Log = LogManager.GetLogger(typeof(ExtendedMongoDBCheck));
    }
}
