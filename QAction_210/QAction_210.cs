using System;
using System.Collections.Generic;
using System.Linq;

using Skyline.DataMiner.Core.ConnectivityFramework.Protocol;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Connections;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Interfaces;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Options;
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
			// Grabbing data from driver.
			object[] columns = (object[])protocol.NotifyProtocol(321 /*NT_GT_TABLE_COLUMNS*/, Parameter.Uniquesourceconnections.tablePid/*200*/, new uint[] { 0, 1 });
			object[] sourceInterfacesO = (object[])columns[0];
			object[] destinationInterfacesO = (object[])columns[1];

			object[] columnsInterfaces = (object[])protocol.NotifyProtocol(321 /*NT_GT_TABLE_COLUMNS*/, Parameter.Driverinterfaces.tablePid/*100*/, new uint[] { 0, 2 });
			object[] interfaceKeys = (object[])columnsInterfaces[0];
			object[] interfacesTypes = (object[])columnsInterfaces[1];

			// Dictionary for quick index lookup.
			Dictionary<string, int> mapInterfaceType = (interfaceKeys != null) ? Enumerable.Range(0, interfaceKeys.Length).ToDictionary(
				i => Convert.ToString(interfaceKeys[i]),
				u => Convert.ToInt32(interfacesTypes[u])) : new Dictionary<string, int>();

			DcfMappingOptions opt = new DcfMappingOptions
			{
				HelperType = SyncOption.EndOfPolling,
				PIDcurrentConnections = Parameter.mapconnections_63998,
				PIDcurrentConnectionProperties = Parameter.mapconnectionproperties_63997,
			};

			using (DcfHelper dcf = new DcfHelper(protocol, Parameter.mapstartupelements_63993, opt))
			{
				List<DcfSaveConnectionRequest> allConnectionRequests = new List<DcfSaveConnectionRequest>();

				for (int i = 0; i < sourceInterfacesO.Length; i++)
				{
					string sourceKey = Convert.ToString(sourceInterfacesO[i]);
					string destinationKey = Convert.ToString(destinationInterfacesO[i]);
					int src_parameterGroupID;

					if (mapInterfaceType.TryGetValue(sourceKey, out src_parameterGroupID))
					{
						// SourceType tells us the ParameterGroupID for this Example Driver.
						// Get the Source Interface from DCF.
						ConnectivityInterface source = dcf.GetInterface(new DcfInterfaceFilterSingle(src_parameterGroupID, sourceKey));
						if (source == null)
						{
							protocol.Log("QA" + protocol.QActionID + "|SaveUniqueSource could not find Source:" + src_parameterGroupID + "/" + sourceKey, LogType.Error, LogLevel.NoLogging);
							continue; // Could not find the interface.
						}

						if (mapInterfaceType.TryGetValue(destinationKey, out int dst_parameterGroupID))
						{
							ConnectivityInterface destination = dcf.GetInterface(new DcfInterfaceFilterSingle(dst_parameterGroupID, destinationKey));
							if (destination == null)
							{
								protocol.Log("QA" + protocol.QActionID + "|SaveUniqueSource could not find destination:" + dst_parameterGroupID + "/" + destinationKey, LogType.Error, LogLevel.NoLogging);
								continue; // Could not find the interface.
							}

							allConnectionRequests.Add(new DcfSaveConnectionRequest(source, destination, SaveConnectionType.Unique_Source));

							/*
							 * SPECIFY UNIQUE SOURCE HERE => This allows the DCFHelper to find the old connection using the SOURCE interface as a Unique Key!
							 * This means only ONE connection should exist for each SOURCE interface!!!
							 */

							/*
							 * SaveConnection can also be called here without issues instead of adding to a List
							 * DCFHelper uses caching to limit the amount of NotifyCalls to DataMiner.
							 * */
							////dcf.SaveConnections(new DCFSaveConnectionRequest(source, destination, SaveConnectionType.Unique_Source));
						}
					}
				}

				DcfSaveConnectionResult[] results = dcf.SaveConnections(allConnectionRequests.ToArray());

				// Making some dummy properties.
				Random r = new Random();

				for (int i = 0; i < results.Length; i++)
				{
					DcfSaveConnectionResult result = results[i];
					if (result != null)
					{
						ConnectivityConnection sourceConnection = result.SourceConnection;

						var property = new ConnectivityConnectionProperty { ConnectionPropertyName = "Passive Component", ConnectionPropertyType = "generic", ConnectionPropertyValue = "Dummy Value " + r.Next(4) };
						var request = new DcfSaveConnectionPropertyRequest(property, false);

						if (sourceConnection != null)
						{
							dcf.SaveConnectionProperties(sourceConnection, request);
						}
					}
				}
			}
		}
		#region catch
		catch (Exception e)
		{
			protocol.Log(string.Format("QA{0}: (Exception) Value at {1} with Exception:{2}", protocol.QActionID, "Run Method", e), LogType.Error, LogLevel.NoLogging);
		}
		#endregion
	}
}
