using System;

using Skyline.DataMiner.Core.ConnectivityFramework.Protocol;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Columns;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Connections;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Interfaces;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Options;
using Skyline.DataMiner.Scripting;

public class QAction
{
	/// <summary>
	/// DCF example save DVE.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void Run(SLProtocol protocol)
	{
		try
		{
			object[] columns = (object[])protocol.NotifyProtocol(321 /*NT_GT_TABLE_COLUMNS*/, 500, new uint[] { 0, 2, 3 });
			object[] keys = (object[])columns[0];
			object[] connectedToColumn = (object[])columns[1];
			object[] elementColumn = (object[])columns[2];

			DcfMappingOptions opt = new DcfMappingOptions
			{
				PIDcurrentConnections = Parameter.mapconnections_63998,
				PIDcurrentConnectionProperties = Parameter.mapconnectionproperties_63997,
				HelperType = SyncOption.EndOfPolling,
			};

			using (DcfHelper dcf = new DcfHelper(protocol, Parameter.mapstartupelements_63993, opt))
			{
				for (int i = 0; i < keys.Length; i++)
				{
					string key = Convert.ToString(keys[i]);
					int connectionType = Convert.ToInt16(connectedToColumn[i]);
					string dveElement = Convert.ToString(elementColumn[i]);
					var sourceLink = new DcfInterfaceFilterSingle(500, key, dveElement);
					DcfSaveConnectionResult[] result;

					switch (connectionType)
					{
						case 0:
							// Connect to 2/1.
							result = dcf.SaveConnections(new DcfSaveConnectionRequest(dcf, sourceLink, new DcfInterfaceFilterSingle(501, key, dveElement), SaveConnectionType.Unique_Source));
							if (result[0].SourceConnection != null)
							{
								dcf.SaveConnectionProperties(
									result[0].SourceConnection,
									new DcfSaveConnectionPropertyRequest(new ConnectivityConnectionProperty
									 {
										 ConnectionPropertyName = "Input Port Name",
										 ConnectionPropertyType = "input",
										 ConnectionPropertyValue = "Port 1/1",
									 }),
									new DcfSaveConnectionPropertyRequest(new ConnectivityConnectionProperty
									 {
										 ConnectionPropertyName = "Output Port Name",
										 ConnectionPropertyType = "output",
										 ConnectionPropertyValue = "Port 2/1",
									 }));
							}

							break;
						case 1:
							// Connect to 2/2.
							result = dcf.SaveConnections(new DcfSaveConnectionRequest(dcf, sourceLink, new DcfInterfaceFilterSingle(502, key, dveElement), SaveConnectionType.Unique_Source));
							if (result[0].SourceConnection != null)
							{
								dcf.SaveConnectionProperties(
									result[0].SourceConnection,
									new DcfSaveConnectionPropertyRequest(new ConnectivityConnectionProperty
									 {
										 ConnectionPropertyName = "Input Port Name",
										 ConnectionPropertyType = "input",
										 ConnectionPropertyValue = "Port 1/1",
									 }),
									new DcfSaveConnectionPropertyRequest(new ConnectivityConnectionProperty
									 {
										 ConnectionPropertyName = "Output Port Name",
										 ConnectionPropertyType = "output",
										 ConnectionPropertyValue = "Port 2/2",
									 }));
							}

							break;
						default:
							// Do nothing.
							break;
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
