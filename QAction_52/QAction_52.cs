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
		DCFMappingOptions opt = new DCFMappingOptions();
		opt.HelperType = SyncOption.Custom;
		opt.PIDcurrentConnections = Parameter.map_connections_63998;
		opt.PIDcurrentConnectionProperties = Parameter.map_connectionproperties_63997;

		using (DCFHelper dcf = new DCFHelper(protocol, Parameter.map_startupelements_63993, opt))
		{
			var sourceInterfaces = dcf.GetInterfaces(new DCFDynamicLink(9));

			// Fixed Properties and Fixed Connections = Remove Everything yourself
			ConnectivityInterface sourceInterface = sourceInterfaces[0].FirstInterface;
			if (sourceInterface != null)
			{
				var connections = sourceInterface.GetConnections(); // returns all connection where the given Interface is the SOURCE interface
				foreach (var con in connections)
				{
					var properties = con.Value.ConnectionProperties;
					dcf.RemoveConnectionProperties(con.Value, false, properties.Keys.ToArray());
				}

				dcf.RemoveConnections(sourceInterface, true, false, connections.Keys.ToArray());
			}
		}
	}
}
