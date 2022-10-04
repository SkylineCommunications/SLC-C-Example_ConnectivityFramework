using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skyline.DataMiner.Library.Common.Subscription.Monitors;
using Skyline.DataMiner.Scripting;

namespace Skyline.DataMiner.Library.Protocol.Subscription.Monitors
{
    /// <summary>
    /// Defines extension methods on <see cref="SLProtocol"/> for monitoring.
    /// </summary>
    public static class SLProtocolExtensions
    {
        /// <summary>
        ///  Setup the monitors automatic cleanup delays. This config will be used by any subscription made by this element.
        /// </summary>
        /// <param name="protocol">The SLProtocol object, which will be used to get the source element.</param>
        /// <param name="cleanupConfig">The desired cleanup configuration.</param>
        public static void SetupMonitorsCleanupConfig(this SLProtocol protocol, MonitorCleanupConfig cleanupConfig)
        {
            string uniqueIndentifier = String.Join("/", protocol.DataMinerID, protocol.ElementID);
            SubscriptionManager.SetupMonitorCleanupConfiguration(uniqueIndentifier, cleanupConfig);
        }
    }
}
