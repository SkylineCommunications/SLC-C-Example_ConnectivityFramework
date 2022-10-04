using System.Linq;

using ProtocolDCF;

using Skyline.DataMiner.Scripting;

public class QAction
{
	/// <summary>
	/// Remove fixed connection.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol connection.</param>
	public static void Run(SLProtocol protocol)
	{
		DCFMappingOptions opt = new DCFMappingOptions
		{
			HelperType = SyncOption.Custom,
			PIDcurrentConnections = Parameter.map_connections_63998,
			PIDcurrentConnectionProperties = Parameter.map_connectionproperties_63997,
		};

		using (DCFHelper dcf = new DCFHelper(protocol, Parameter.map_startupelements_63993, opt))
		{
			var sourceInterfaces = dcf.GetInterfaces(new DCFDynamicLink(9));

			// Fixed Properties and Fixed Connections = Remove Everything yourself
			ConnectivityInterface sourceInterface = sourceInterfaces[0].FirstInterface;

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
