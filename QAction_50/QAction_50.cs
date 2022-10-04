using System;

using ProtocolDCF;

using Skyline.DataMiner.Scripting;

public class QAction
{
	/// <summary>
	/// Mode selection change.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void Run(SLProtocolExt protocol)
	{
		try
		{
			if (protocol.IsEmpty(50) || protocol.IsEmpty(51))
			{
				return;
			}

			string connectionFilter = Convert.ToString(protocol.Connectionfilter_53);
			int mode = Convert.ToInt32(protocol.Mode_50);
			int inputSel = Convert.ToInt32(protocol.Inputselection_51);

			DCFMappingOptions opt = new DCFMappingOptions
			{
				HelperType = SyncOption.EndOfPolling,
				PIDcurrentConnections = 63998,
				PIDcurrentConnectionProperties = 63997,
			};

			using (DCFHelper dcf = new DCFHelper(protocol, false, opt))
			{
				// All connection are using SaveConnectionType .destination because there will always be just a single connection connected to the Virtual Output.
				DCFSaveConnectionResult[] result;
				if (mode == 0)
				{
					if (inputSel == 0)
					{
						// Connect A to both Virtual out A and out B.
						result = dcf.SaveConnections(new DCFSaveConnectionRequest(dcf, new DCFDynamicLink(1), new DCFDynamicLink(9), SaveConnectionType.Unique_Destination, "A -> Virtual A", connectionFilter));
						if (result[0].sourceConnection != null)
						{
							dcf.SaveConnectionProperties(
								result[0].sourceConnection,
								full: false,
								new ConnectivityConnectionProperty { ConnectionPropertyName = "Passive Component", ConnectionPropertyType = "generic", ConnectionPropertyValue = "Dynamic:" + mode + "/" + inputSel });
						}

						result = dcf.SaveConnections(new DCFSaveConnectionRequest(dcf, new DCFDynamicLink(1), new DCFDynamicLink(10), SaveConnectionType.Unique_Destination, "A -> Virtual B", connectionFilter));
						if (result[0].sourceConnection != null)
						{
							dcf.SaveConnectionProperties(
								result[0].sourceConnection,
								full: false,
								new ConnectivityConnectionProperty { ConnectionPropertyName = "Passive Component", ConnectionPropertyType = "generic", ConnectionPropertyValue = "Dynamic:" + mode + "/" + inputSel });
						}
					}
					else
					{
						// Connect B to both Virtual out A and out B.
						result = dcf.SaveConnections(new DCFSaveConnectionRequest(dcf, new DCFDynamicLink(2), new DCFDynamicLink(9), SaveConnectionType.Unique_Destination, "B -> Virtual A", connectionFilter));
						if (result[0].sourceConnection != null)
						{
							dcf.SaveConnectionProperties(
								result[0].sourceConnection,
								full: false,
								new ConnectivityConnectionProperty { ConnectionPropertyName = "Passive Component", ConnectionPropertyType = "generic", ConnectionPropertyValue = "Dynamic:" + mode + "/" + inputSel });
						}

						result = dcf.SaveConnections(new DCFSaveConnectionRequest(dcf, new DCFDynamicLink(2), new DCFDynamicLink(10), SaveConnectionType.Unique_Destination, "B -> Virtual B", connectionFilter));
						if (result[0].sourceConnection != null)
						{
							dcf.SaveConnectionProperties(
								result[0].sourceConnection,
								full: false,
								new ConnectivityConnectionProperty { ConnectionPropertyName = "Passive Component", ConnectionPropertyType = "generic", ConnectionPropertyValue = "Dynamic:" + mode + "/" + inputSel });
						}
					}
				}
				else
				{
					if (inputSel == 0)
					{
						// Connect A to  Virtual out A
						// Connect B to Virtual out B
						result = dcf.SaveConnections(new DCFSaveConnectionRequest(dcf, new DCFDynamicLink(1), new DCFDynamicLink(9), SaveConnectionType.Unique_Destination, "A -> Virtual A", connectionFilter));
						if (result[0].sourceConnection != null)
							dcf.SaveConnectionProperties(result[0].sourceConnection, false, new ConnectivityConnectionProperty { ConnectionPropertyName = "Passive Component", ConnectionPropertyType = "generic", ConnectionPropertyValue = "Dynamic:" + mode + "/" + inputSel });

						result = dcf.SaveConnections(new DCFSaveConnectionRequest(dcf, new DCFDynamicLink(2), new DCFDynamicLink(10), SaveConnectionType.Unique_Destination, "B -> Virtual B", connectionFilter));
						if (result[0].sourceConnection != null)
							dcf.SaveConnectionProperties(result[0].sourceConnection, false, new ConnectivityConnectionProperty { ConnectionPropertyName = "Passive Component", ConnectionPropertyType = "generic", ConnectionPropertyValue = "Dynamic:" + mode + "/" + inputSel });
					}
					else
					{
						// Connect A to  Virtual out B
						// Connect B to Virtual out A
						result = dcf.SaveConnections(new DCFSaveConnectionRequest(dcf, new DCFDynamicLink(1), new DCFDynamicLink(10), SaveConnectionType.Unique_Destination, "A -> Virtual B", connectionFilter));
						if (result[0].sourceConnection != null)
						{
							dcf.SaveConnectionProperties(
								result[0].sourceConnection,
								full: false,
								new ConnectivityConnectionProperty { ConnectionPropertyName = "Passive Component", ConnectionPropertyType = "generic", ConnectionPropertyValue = "Dynamic:" + mode + "/" + inputSel });
						}

						result = dcf.SaveConnections(new DCFSaveConnectionRequest(dcf, new DCFDynamicLink(2), new DCFDynamicLink(9), SaveConnectionType.Unique_Destination, "B -> Virtual A", connectionFilter));
						if (result[0].sourceConnection != null)
						{
							dcf.SaveConnectionProperties(
								result[0].sourceConnection,
								full: false,
								new ConnectivityConnectionProperty { ConnectionPropertyName = "Passive Component", ConnectionPropertyType = "generic", ConnectionPropertyValue = "Dynamic:" + mode + "/" + inputSel });
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