//#define DCFv1
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Skyline.DataMiner.Scripting;
using ProtocolDCF;
public class QAction
{
	/// <summary>
	/// DCF_Example_SaveDVE
	/// </summary>
	/// <param name="protocol">Link with Skyline Dataminer</param>
	public static void Run(SLProtocol protocol)
	{
		try
		{
#if DCFv1
			object[] columns = (Object[])protocol.NotifyProtocol(321 /*NT_GT_TABLE_COLUMNS*/, 500, new UInt32[] { 0, 2, 3 });
			object[] keys = (object[])columns[0];
			object[] connectedToColumn = (object[])columns[1];
			object[] elementColumn = (object[])columns[2];
			DCFMappingOptions opt = new DCFMappingOptions();
			opt.PIDcurrentConnections = Parameter.map_connections_63998;
			opt.PIDcurrentConnectionProperties = Parameter.map_connectionproperties_63997;
			opt.HelperType = SyncOption.EndOfPolling;

			//This uses DVE's so they need to be checked for startup, define what the Column is holding the ;element data (Multiple columns can be given and checked)
			DVEColumn dveCol = new DVEColumn(Parameter.DveTable.tablePid, Parameter.DveTable.Idx.virtualElementEleColumn_504);
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
						case 0://Connect to 2/1
							result = dcf.SaveConnections(new DCFSaveConnectionRequest(dcf, sourceLink, new DCFDynamicLink(501, key, dveElement), SaveConnectionType.Unique_Source));
							if (result[0].sourceConnection != null)
							{
								dcf.SaveConnectionProperties(result[0].sourceConnection, false,
								new ConnectivityConnectionProperty()
								{
									ConnectionPropertyName = "Input Port Name",
									ConnectionPropertyType = "input",
									ConnectionPropertyValue = "Port 1/1"
								},
								new ConnectivityConnectionProperty()
								{
									ConnectionPropertyName = "Output Port Name",
									ConnectionPropertyType = "output",
									ConnectionPropertyValue = "Port 2/1"
								}
									);
							}
							break;
						case 1://Connect to 2/2
							result = dcf.SaveConnections(new DCFSaveConnectionRequest(dcf, sourceLink, new DCFDynamicLink(502, key, dveElement), SaveConnectionType.Unique_Source));
							if (result[0].sourceConnection != null)
							{
								dcf.SaveConnectionProperties(result[0].sourceConnection, false,
								new ConnectivityConnectionProperty()
								{
									ConnectionPropertyName = "Input Port Name",
									ConnectionPropertyType = "input",
									ConnectionPropertyValue = "Port 1/1"
								},
								new ConnectivityConnectionProperty()
								{
									ConnectionPropertyName = "Output Port Name",
									ConnectionPropertyType = "output",
									ConnectionPropertyValue = "Port 2/2"
								}
									);
							}
							break;
					}


				}
			}
#endif
		}
		#region catch
		catch (Exception e)
		{ protocol.Log(string.Format("QA{0}: (Exception) Value at {1} with Exception:{2}", protocol.QActionID, "Run Method", e.ToString()), LogType.Error, LogLevel.NoLogging); }
		#endregion
	}
}
