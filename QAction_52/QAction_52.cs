using System.Linq;

using Skyline.DataMiner.Core.ConnectivityFramework.Protocol;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Interfaces;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Options;
using Skyline.DataMiner.Scripting;

public class QAction
{
	/// <summary>
	/// Remove fixed connection.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol connection.</param>
	public static void Run(SLProtocol protocol)
	{
		DcfMappingOptions opt = new DcfMappingOptions
		{
			HelperType = SyncOption.Custom,
			PIDcurrentConnections = Parameter.mapconnections_63998,
			PIDcurrentConnectionProperties = Parameter.mapconnectionproperties_63997,
		};

		using (DcfHelper dcf = new DcfHelper(protocol, Parameter.mapstartupelements_63993, opt))
		{
			var sourceInterface = dcf.GetInterface(new DcfInterfaceFilterSingle(9));

			// Fixed Properties and Fixed Connections = Remove Everything yourself
			if (sourceInterface != null)
			{
				var connections = sourceInterface.GetConnections(); // returns all connection where the given Interface is the SOURCE interface
				foreach (var connection in connections)
				{
					var properties = connection.Value.ConnectionProperties;
					dcf.RemoveConnectionProperties(connection.Value, false, properties.Keys.ToArray());
				}

				dcf.RemoveConnections(sourceInterface, true, false, connections.Keys.ToArray());
			}
		}
	}
}
