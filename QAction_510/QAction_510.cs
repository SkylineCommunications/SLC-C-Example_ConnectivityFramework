using System;

using ProtocolDCF;

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

			DCFMappingOptions opt = new DCFMappingOptions
			{
				PIDcurrentConnections = Parameter.map_connections_63998,
				PIDcurrentConnectionProperties = Parameter.map_connectionproperties_63997,
				HelperType = SyncOption.EndOfPolling
			};

			// This uses DVEs so they need to be checked for startup, define what the Column is holding the ;element data (Multiple columns can be given and checked).
			DVEColumn dveCol = new DVEColumn(Parameter.Dvetable.tablePid, Parameter.Dvetable.Idx.dvetablevirtualelementcolumn_504);

			using (DCFHelper dcf = new DCFHelper(protocol, Parameter.map_startupelements_63993, opt, dveCol))
			{
				for (int i = 0; i < keys.Length; i++)
				{
					string key = Convert.ToString(keys[i]);
					int connectionType = Convert.ToInt16(connectedToColumn[i]);
					string dveElement = Convert.ToString(elementColumn[i]);
					var sourceLink = new DCFDynamicLink(500, key, dveElement);
					DCFSaveConnectionResult[] result;

					switch (connectionType)
					{
						case 0:
							// Connect to 2/1.
							result = dcf.SaveConnections(new DCFSaveConnectionRequest(dcf, sourceLink, new DCFDynamicLink(501, key, dveElement), SaveConnectionType.Unique_Source));
							if (result[0].sourceConnection != null)
							{
								dcf.SaveConnectionProperties(
									result[0].sourceConnection,
									false,
									new ConnectivityConnectionProperty
									{
										ConnectionPropertyName = "Input Port Name",
										ConnectionPropertyType = "input",
										ConnectionPropertyValue = "Port 1/1"
									},
									new ConnectivityConnectionProperty
									{
										ConnectionPropertyName = "Output Port Name",
										ConnectionPropertyType = "output",
										ConnectionPropertyValue = "Port 2/1"
									});
							}

							break;
						case 1:
							// Connect to 2/2.
							result = dcf.SaveConnections(new DCFSaveConnectionRequest(dcf, sourceLink, new DCFDynamicLink(502, key, dveElement), SaveConnectionType.Unique_Source));
							if (result[0].sourceConnection != null)
							{
								dcf.SaveConnectionProperties(
									result[0].sourceConnection,
									false,
									new ConnectivityConnectionProperty
									{
										ConnectionPropertyName = "Input Port Name",
										ConnectionPropertyType = "input",
										ConnectionPropertyValue = "Port 1/1"
									},
									new ConnectivityConnectionProperty
									{
										ConnectionPropertyName = "Output Port Name",
										ConnectionPropertyType = "output",
										ConnectionPropertyValue = "Port 2/2"
									});
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
