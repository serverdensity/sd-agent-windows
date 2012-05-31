using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using BoxedIce.ServerDensity.Agent;
using BoxedIce.ServerDensity.Agent.Checks;

namespace AgentTests
{
    [TestClass]
    public class NetworkCalculations
    {
        [TestMethod]
        public void StandardNetworkTraffic()
        {
            NetworkTrafficCheck check = new NetworkTrafficCheck();

            var store = new Dictionary<string, long>();
            store["recv_bytes"] = 10;

            var result = check.CheckForOverflow("recv", store, 100);
            Assert.AreEqual(result[0], 90);
            Assert.AreEqual(result[1], 100);
        }

        [TestMethod]
        public void HigherStandardNetworkTraffic()
        {
            NetworkTrafficCheck check = new NetworkTrafficCheck();

            var store = new Dictionary<string, long>();
            store["recv_bytes"] = 30000000;

            var result = check.CheckForOverflow("recv", store, 30001000);
            Assert.AreEqual(result[0], 1000);
            Assert.AreEqual(result[1], 30001000);
        }

        [TestMethod]
        public void StandardNetworkTrafficWithOverFlow()
        {
            NetworkTrafficCheck check = new NetworkTrafficCheck();

            var store = new Dictionary<string, long>();
            store["recv_bytes"] = UInt32.MaxValue - 100;

            var result = check.CheckForOverflow("recv", store, 100);
            Assert.AreEqual(200, result[0]);
            Assert.AreEqual(100, result[1]);
        }

        [TestMethod]
        public void DoubleStandardNetworkTraffic()
        {
            NetworkTrafficCheck check = new NetworkTrafficCheck();

            var store = new Dictionary<string, long>();
            store["recv_bytes"] = 1216986405;

            var result = check.CheckForOverflow("recv", store, 1217007129);
            store["recv_bytes"] = result[1];

            var second_result = check.CheckForOverflow("recv", store, 1217010727);
            Assert.AreEqual(3598, second_result[0]);
            Assert.AreEqual(1217010727, second_result[1]);
        }

        [TestMethod]
        public void AfterOverFlow()
        {
            NetworkTrafficCheck check = new NetworkTrafficCheck();

            var store = new Dictionary<string, long>();
            store["recv_bytes"] = UInt32.MaxValue - 100;

            long target = UInt32.MaxValue;
            target += 100;

            var result = check.CheckForOverflow("recv", store, 100);
            store["recv_bytes"] = result[0];

            var second_result = check.CheckForOverflow("recv", store, 100 + result[0]);
            Assert.AreEqual(100, second_result[0]);
        }
    }
}
