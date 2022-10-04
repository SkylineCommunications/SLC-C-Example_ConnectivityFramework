namespace Skyline.DataMiner.Library.Common.Subscription.Monitors
{
    using Skyline.DataMiner.Library.Common.Selectors;
    using Skyline.DataMiner.Library.Common.SLNetHelper;
    using Skyline.DataMiner.Net;
    using Skyline.DataMiner.Net.Messages;

    using System;
    using System.Collections.Concurrent;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;

    internal class Monitor
    {
        internal Monitor(ICommunication connection, string sourceId)
        {
            Connection = connection;
            SourceIdentifier = sourceId;
        }

        internal Monitor(ICommunication connection, Element sourceElement) : this(connection, Convert.ToString(sourceElement, CultureInfo.InvariantCulture))
        {
            SourceElement = sourceElement;

            if (sourceElement != null)
            {
                ElementCleanupHandle = new SLNetWaitHandle
                {
                    SetId = SourceElement + "_CLP_ElementCleanup",
                    Type = WaitHandleType.Cleanup,
                    Flag = new System.Threading.AutoResetEvent(false),
                    TriggeredQueue = new ConcurrentQueue<object>()
                };
            }
        }

        internal ICommunication Connection { get; set; }

        internal SLNetWaitHandle ElementCleanupHandle { get; set; }

        internal Element SourceElement { get; set; }

        internal string SourceIdentifier { get; set; }

        internal void TryAddElementCleanup()
        {
            if (SourceElement != null)
            {
                ElementCleanupHandle.Handler = CreateElementCleanupHandle(ElementCleanupHandle.SetId);
                ElementCleanupHandle.Subscriptions = new SubscriptionFilter[] { new SubscriptionFilterElement(typeof(ElementStateEventMessage), SourceElement.AgentId, SourceElement.ElementId) };

                SubscriptionManager.CreateSubscription(SourceElement.ToString(), Connection, ElementCleanupHandle, false);
            }
        }

        protected void TryAddDestinationElementCleanup(int agentId, int elementId)
        {
            if (SourceIdentifier != null)
            {
                if (SourceElement != null && SourceElement.AgentId == agentId && SourceElement.ElementId == elementId) return;

                var handler = CreateDestinationElementCleanupHandle(agentId, elementId);

                SLNetWaitHandle destinationElementCleanupHandle = new SLNetWaitHandle
                {
                    SetId = SourceIdentifier + "-" + agentId + "/" + elementId + "_CLP_DestElemCleanup",
                    Flag = new System.Threading.AutoResetEvent(false),
                    Type = WaitHandleType.Cleanup,
                    Destination = agentId + "/" + elementId,
                    TriggeredQueue = new ConcurrentQueue<object>(),
                    Handler = handler,
                    Subscriptions = new SubscriptionFilter[] { new SubscriptionFilterElement(typeof(ElementStateEventMessage), agentId, elementId) }
                };

                SubscriptionManager.CreateSubscription(SourceIdentifier, Connection, destinationElementCleanupHandle, false);
            }
        }

        protected void TryAddDestinationServiceCleanup(int agentId, int serviceId)
        {
            if (SourceIdentifier != null)
            {
                if (SourceElement != null && SourceElement.AgentId == agentId && SourceElement.ElementId == serviceId) return;

                var handler = CreateDestinationServiceCleanupHandle(agentId, serviceId);

                SLNetWaitHandle destinationServiceCleanupHandle = new SLNetWaitHandle
                {
                    SetId = SourceIdentifier + "-" + agentId + "/" + serviceId + "_CLP_DestServCleanup",
                    Flag = new System.Threading.AutoResetEvent(false),
                    Type = WaitHandleType.Cleanup,
                    Destination = agentId + "/" + serviceId,
                    TriggeredQueue = new ConcurrentQueue<object>(),
                    Handler = handler,
                    Subscriptions = new SubscriptionFilter[] { new SubscriptionFilterElement(typeof(ServiceStateEventMessage), agentId, serviceId) }
                };

                SubscriptionManager.CreateSubscription(SourceIdentifier, Connection, destinationServiceCleanupHandle, false);
            }
        }

        private NewMessageEventHandler CreateElementCleanupHandle(string HandleGuid)
        {
            string myGuid = HandleGuid;

            return (sender, e) =>
            {
                try
                {
                    if (!e.FromSet(myGuid)) return;

                    var elementStateMessage = e.Message as ElementStateEventMessage;
                    if (elementStateMessage == null) return;

                    var senderConn = (Connection)sender;
                    System.Diagnostics.Debug.WriteLine("State Change:" + elementStateMessage.State);
                    string uniqueIdentifier = elementStateMessage.DataMinerID + "/" + elementStateMessage.ElementID;

                    // clear subscriptions if element is stopped or deleted
                    if (elementStateMessage.State == Net.Messages.ElementState.Deleted || elementStateMessage.State == Net.Messages.ElementState.Stopped)
                    {
                        System.Diagnostics.Debug.WriteLine("Deleted or Stopped: Need to clean subscriptions");
                        ICommunication com = new ConnectionCommunication(senderConn);

                        var cleanupConfig = SubscriptionManager.TryGetMonitorCleanupConfig(uniqueIdentifier);
                        int delay = elementStateMessage.State == Net.Messages.ElementState.Deleted ? cleanupConfig.SourceDeletedCleanupDelay : cleanupConfig.SourceStoppedCleanupDelay;
                        System.Diagnostics.Debug.WriteLine("Cleanup Delay: " + delay);
                        ElementCleanupExecutor(myGuid, elementStateMessage, uniqueIdentifier, com, delay);
                    }
                    else if (elementStateMessage.State == Net.Messages.ElementState.Active)
                    {
                        SubscriptionManager.CancelCleanupCancelationToken(myGuid);
                    }
                }
                catch (Exception ex)
                {
                    var message = "Monitor Error: Exception during Handle of Source CleanupHandle event (Class Library Side): " + myGuid + " -- " + e + " With exception: " + ex;
                    System.Diagnostics.Debug.WriteLine(message);
                    Logger.Log(message);
                }
            };
        }

        private void ElementCleanupExecutor(string myGuid, ElementStateEventMessage elementStateMessage, string uniqueIdentifier, ICommunication com, int delay)
        {
            if (delay > 0)
            {
                var cancelationTokenSource = SubscriptionManager.CreateAndSaveCleanupCancelationToken(myGuid);
                new Task(() =>
                {
                    try
                    {
                        SleepWhileNotCanceled(delay, cancelationTokenSource.Token);

                        var destinationElement = com.SendSingleResponseMessage(new GetElementByIDMessage(elementStateMessage.DataMinerID, elementStateMessage.ElementID));
                        if ((destinationElement as ElementInfoEventMessage).State == elementStateMessage.State)
                        {
                            System.Diagnostics.Debug.WriteLine("Removed After Delay triggered.");
                            SubscriptionManager.RemoveSubscriptions(uniqueIdentifier, com);
                        }
                    }
                    catch (OperationCanceledException _)
                    {
                        SubscriptionManager.RemoveCleanupCancelationToken(myGuid);
                        System.Diagnostics.Debug.WriteLine("Delayed Subscription Remove Cancelled.");
                        return;
                    }
                    catch (Exception)
                    {
                        SubscriptionManager.RemoveSubscriptions(uniqueIdentifier, com);
                    }

                    SubscriptionManager.RemoveCleanupCancelationToken(myGuid);

                }, cancelationTokenSource.Token).Start();
            }
            else
            {
                SubscriptionManager.RemoveSubscriptions(uniqueIdentifier, com);
            }
        }

        private NewMessageEventHandler CreateDestinationElementCleanupHandle(int agentId, int elementId)
        {
            string myGuid = SourceIdentifier + "-" + agentId + "/" + elementId + "_CLP_DestElemCleanup";

            return (sender, e) =>
            {
                try
                {
                    if (!e.FromSet(myGuid)) return;

                    var elementStateMessage = e.Message as ElementStateEventMessage;
                    if (elementStateMessage == null) return;

                    var senderConn = (Connection)sender;
                    System.Diagnostics.Debug.WriteLine("Destination Element State Change:" + elementStateMessage.State);

                    // Clear subscriptions if element is deleted.
                    if (elementStateMessage.State == Net.Messages.ElementState.Deleted)
                    {
                        System.Diagnostics.Debug.WriteLine("Deleted: Need to clean subscriptions for destination element");
                        ICommunication comm = new ConnectionCommunication(senderConn);


                        int delay = SubscriptionManager.TryGetMonitorCleanupConfig(SourceIdentifier).DestinationDeletedCleanupDelay;
                        DestinationElementCleanupExecutor(myGuid, elementStateMessage, comm, delay);
                    }
                    else if (elementStateMessage.State == Net.Messages.ElementState.Active)
                    {
                        SubscriptionManager.CancelCleanupCancelationToken(myGuid);
                    }
                }
                catch (Exception ex)
                {
                    var message = "Monitor Error: Exception during Handle of Destination CleanupHandle event (Class Library Side): " + myGuid + " -- " + e + " With exception: " + ex;
                    System.Diagnostics.Debug.WriteLine(message);
                    Logger.Log(message);
                }
            };
        }

        private void DestinationElementCleanupExecutor(string myGuid, ElementStateEventMessage elementStateMessage, ICommunication com, int delay)
        {
            string destinationIdentifier = elementStateMessage.DataMinerID + "/" + elementStateMessage.ElementID;

            if (delay > 0)
            {
                var cancelationTokenSource = SubscriptionManager.CreateAndSaveCleanupCancelationToken(myGuid);

                new Task(() =>
                {
                    try
                    {
                        SleepWhileNotCanceled(delay, cancelationTokenSource.Token);

                        var destinationElement = com.SendSingleResponseMessage(new GetElementByIDMessage(elementStateMessage.DataMinerID, elementStateMessage.ElementID));

                        if ((destinationElement as ElementInfoEventMessage).State == ElementState.Deleted)
                        {
                            SubscriptionManager.RemoveSubscriptions(SourceIdentifier, destinationIdentifier, com);
                        }
                    }
                    catch (OperationCanceledException _)
                    {
                        SubscriptionManager.RemoveCleanupCancelationToken(myGuid);
                        return;
                    }
                    catch (Exception)
                    {
                        SubscriptionManager.RemoveSubscriptions(SourceIdentifier, destinationIdentifier, com);
                    }

                    SubscriptionManager.RemoveCleanupCancelationToken(myGuid);
                }, cancelationTokenSource.Token).Start();
            }
            else
            {
                SubscriptionManager.RemoveSubscriptions(SourceIdentifier, destinationIdentifier, com);
            }
        }

        private NewMessageEventHandler CreateDestinationServiceCleanupHandle(int agentId, int serviceId)
        {
            string myGuid = SourceIdentifier + "-" + agentId + "/" + serviceId + "_CLP_DestServCleanup";

            return (sender, e) =>
            {
                try
                {
                    if (!e.FromSet(myGuid)) return;

                    var serviceStateMessage = e.Message as ServiceStateEventMessage;
                    if (serviceStateMessage == null) return;

                    var senderConn = (Connection)sender;
                    System.Diagnostics.Debug.WriteLine("Destination Service State Change. IsDeleted:" + serviceStateMessage.IsDeleted);

                    string destinationIdentifier = serviceStateMessage.DataMinerID + "/" + serviceStateMessage.ElementID;

                    // Clear subscriptions if service is deleted.
                    if (serviceStateMessage.IsDeleted)
                    {
                        System.Diagnostics.Debug.WriteLine("Deleted: Need to clean subscriptions for destination service");
                        ICommunication com = new ConnectionCommunication(senderConn);

                        int delay = SubscriptionManager.TryGetMonitorCleanupConfig(SourceIdentifier).DestinationDeletedCleanupDelay;
                        DestinationServiceCleanupExecutor(myGuid, serviceStateMessage, destinationIdentifier, com, delay);
                    }
                    else
                    {
                        SubscriptionManager.CancelCleanupCancelationToken(myGuid);
                    }
                }
                catch (Exception ex)
                {
                    var message = "Monitor Error: Exception during Handle of Destination CleanupHandle event (Class Library Side): " + myGuid + " -- " + e + " With exception: " + ex;
                    System.Diagnostics.Debug.WriteLine(message);
                    Logger.Log(message);
                }
            };
        }

        private void DestinationServiceCleanupExecutor(string myGuid, ServiceStateEventMessage serviceStateMessage, string destinationIdentifier, ICommunication com, int delay)
        {
            if (delay > 0)
            {
                var cancelationTokenSource = SubscriptionManager.CreateAndSaveCleanupCancelationToken(myGuid);
                new Task(() =>
                {
                    try
                    {
                        SleepWhileNotCanceled(delay, cancelationTokenSource.Token);

                        var destinationService = com.SendSingleResponseMessage(new GetServiceByIDMessage(serviceStateMessage.DataMinerID, serviceStateMessage.ElementID));
                        if ((destinationService as ServiceInfoEventMessage).IsDeleted)
                        {
                            SubscriptionManager.RemoveSubscriptions(SourceIdentifier, destinationIdentifier, com);
                        }
                    }
                    catch (OperationCanceledException _)
                    {
                        SubscriptionManager.RemoveCleanupCancelationToken(myGuid);
                        return;
                    }
                    catch (Exception)
                    {
                        SubscriptionManager.RemoveSubscriptions(SourceIdentifier, destinationIdentifier, com);
                    }

                    SubscriptionManager.RemoveCleanupCancelationToken(myGuid);
                }, cancelationTokenSource.Token).Start();
            }
            else
            {
                SubscriptionManager.RemoveSubscriptions(SourceIdentifier, destinationIdentifier, com);
            }
        }

        private static bool TryGetIterations(int delay, out int iterations, out int leftOverTime)
        {
            iterations = 0;
            leftOverTime = 0;
            iterations = delay / 2000;
            leftOverTime = delay - (iterations * 2000);

            return delay != 0;
        }

        private static void SleepWhileNotCanceled(int delay, CancellationToken token)
        {
            int iterations;
            int leftOver;
            if (TryGetIterations(delay, out iterations, out leftOver))
            {
                if (iterations > 0)
                {
                    int count = 0;
                    do
                    {
                        Thread.Sleep(2000);
                        count++;
                        if (token.IsCancellationRequested)
                        {
                            token.ThrowIfCancellationRequested();
                        }
                    } while (count <= iterations);
                }
            }

            if (leftOver > 0)
            {
                Thread.Sleep(leftOver);
                if (token.IsCancellationRequested)
                {
                    token.ThrowIfCancellationRequested();
                }
            }
        }
    }
}
