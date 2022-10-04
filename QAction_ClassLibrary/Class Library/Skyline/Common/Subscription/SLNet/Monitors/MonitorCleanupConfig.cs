namespace Skyline.DataMiner.Library.Common.Subscription.Monitors
{
    /// <summary>
    /// This class allows to customize the automatic subscription cleaup mechanism.
    /// </summary>
    public class MonitorCleanupConfig
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MonitorCleanupConfig"/> class.
        /// </summary>
        /// <param name="destinationDeletedCleanupDelay">Delay in miliseconds before cleannig up the subscriptions when the destination is deleted.</param>
        /// <param name="sourceDeletedCleanupDelay">Delay in miliseconds before cleannig up the subscriptions when the source is deleted.</param>
        /// <param name="sourceStoppedCleanupDelay">Delay in miliseconds before cleannig up the subscriptions when the source is stopped.</param>
        public MonitorCleanupConfig(int destinationDeletedCleanupDelay, int sourceDeletedCleanupDelay, int sourceStoppedCleanupDelay)
        {
            DestinationDeletedCleanupDelay = destinationDeletedCleanupDelay;
            SourceDeletedCleanupDelay = sourceDeletedCleanupDelay;
            SourceStoppedCleanupDelay = sourceStoppedCleanupDelay;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MonitorCleanupConfig"/> class.
        /// </summary>
        public MonitorCleanupConfig() : this(0, 0, 0)
        {
        }
               
        internal int DestinationDeletedCleanupDelay { get; set; }

        internal int SourceDeletedCleanupDelay { get; set; }

        internal int SourceStoppedCleanupDelay { get; set; }
    }
}
