using System;
using System.Collections.Generic;
using System.Linq;

using ProtocolDCF;

using Skyline.DataMiner.Scripting;

public class QAction
{
	/// <summary>
	/// DCF_Example_SaveUniqueSource.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void Run(SLProtocolExt protocol)
	{
		try
		{
			// Grabbing Data from driver
			object[] columns = (object[])protocol.NotifyProtocol(321 /*NT_GT_TABLE_COLUMNS*/, Parameter.Uniquesourceconnections.tablePid/*200*/, new uint[] { 0, 1 });
			object[] sourceInterfacesO = (object[])columns[0];
			object[] destinationInterfacesO = (object[])columns[1];

			object[] columnsInterfaces = (object[])protocol.NotifyProtocol(321 /*NT_GT_TABLE_COLUMNS*/, Parameter.Driverinterfaces.tablePid/*100*/, new uint[] { 0, 2 });
			object[] interfaceKeys = (object[])columnsInterfaces[0];
			object[] interfacesTypes = (object[])columnsInterfaces[1];

			//Dictionary for Quick Index Lookup
			Dictionary<string, int> mapInterfaceType = (interfaceKeys != null) ? Enumerable.Range(0, interfaceKeys.Length).ToDictionary(
				i => Convert.ToString(interfaceKeys[i]),
				u => Convert.ToInt32(interfacesTypes[u])) : new Dictionary<string, int>();

			DCFMappingOptions opt = new DCFMappingOptions();
			opt.HelperType = SyncOption.EndOfPolling;
			opt.PIDcurrentConnections = Parameter.map_connections_63998;
			opt.PIDcurrentConnectionProperties = Parameter.map_connectionproperties_63997;
			using (DCFHelper dcf = new DCFHelper(protocol, Parameter.map_startupelements_63993, opt))
			{
				List<DCFSaveConnectionRequest> allConnectionRequests = new List<DCFSaveConnectionRequest>();
				for (int i = 0; i < sourceInterfacesO.Length; i++)
				{
					string sourceKey = Convert.ToString(sourceInterfacesO[i]);
					string destinationKey = Convert.ToString(destinationInterfacesO[i]);
					int src_parameterGroupID;
					if (mapInterfaceType.TryGetValue(sourceKey, out src_parameterGroupID))
					{
						//SourceType tells us the ParameterGroupID for this Example Driver
						//Get the Source Interface from DCF

						ConnectivityInterface source = dcf.GetInterfaces(new DCFDynamicLink(src_parameterGroupID, sourceKey))[0].FirstInterface;
						if (source == null)
						{
							protocol.Log("QA" + protocol.QActionID + "|SaveUniqueSource could not find Source:" + src_parameterGroupID + "/" + sourceKey, LogType.Error, LogLevel.NoLogging);
							continue;
							//Could not find the interface
						}

						int dst_parameterGroupID;

						if (mapInterfaceType.TryGetValue(destinationKey, out dst_parameterGroupID))
						{
							ConnectivityInterface destination = dcf.GetInterfaces(new DCFDynamicLink(dst_parameterGroupID, destinationKey))[0].FirstInterface;
							if (destination == null)
							{
								protocol.Log("QA" + protocol.QActionID + "|SaveUniqueSource could not find destination:" + dst_parameterGroupID + "/" + destinationKey, LogType.Error, LogLevel.NoLogging);
								continue;//Could not find the interface
							}

							allConnectionRequests.Add(new DCFSaveConnectionRequest(source, destination, SaveConnectionType.Unique_Source));

							// SPECIFY UNIQUE SOURCE HERE => This allows the DCFHelper to find the old connection using the SOURCE interface as a Unique Key! This means only ONE connection should exist for each SOURCE interface!!!
							/*SaveConnection can also be called here without issues instead of adding to a List
							 DCFHelper uses caching to limit the amount of NotifyCalls to Dataminer.
							dcf.SaveConnections(new DCFSaveConnectionRequest(source, destination, SaveConnectionType.Unique_Source));
							 * */
						}
					}
				}

				DCFSaveConnectionResult[] results = dcf.SaveConnections(allConnectionRequests.ToArray());

				//Making some Dummy Properties
				Random r = new Random();
				for (int i = 0; i < results.Length; i++)
				{
					DCFSaveConnectionResult result = results[i];
					if (result != null)
					{
						ConnectivityConnection sourceConnection = result.sourceConnection;
						if (sourceConnection != null)
						{
							dcf.SaveConnectionProperties(sourceConnection, false, false, false, true, new ConnectivityConnectionProperty() { ConnectionPropertyName = "Passive Component", ConnectionPropertyType = "generic", ConnectionPropertyValue = "Dummy Value " + r.Next(4) });
						}
					}
				}
			}
		}
		#region catch
		catch (Exception e)
		{
			protocol.Log(string.Format("QA{0}: (Exception) Value at {1} with Exception:{2}", protocol.QActionID, "Run Method", e.ToString()), LogType.Error, LogLevel.NoLogging);
		}
		#endregion
	}
}
