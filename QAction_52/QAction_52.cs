//#define DCFv1
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Skyline.DataMiner.Scripting;
using ProtocolDCF;
using System.Linq;
public class QAction
{
	/// <summary>
	/// DCF_Example_RemoveFixedConnection
	/// </summary>
	/// <param name="protocol">Link with Skyline Dataminer</param>
	public static void Run(SLProtocol protocol)
	{
#if DCFv1
		DCFMappingOptions opt = new DCFMappingOptions();
		opt.HelperType = SyncOption.Custom;
		opt.PIDcurrentConnections = Parameter.map_connections_63998;
		opt.PIDcurrentConnectionProperties = Parameter.map_connectionproperties_63997;
		using (DCFHelper dcf = new DCFHelper(protocol, Parameter.map_startupelements_63993, opt))
		{
			var sourceInterfaces = dcf.GetInterfaces(new DCFDynamicLink(9));
			//Fixed Properties and Fixed Connections = Remove Everything yourself
			ConnectivityInterface sourceInterface = sourceInterfaces[0].firstInterface;
			if(sourceInterface != null){
			var connections = sourceInterface.GetConnections();//returns all connection where the given Interface is the SOURCE interface
			foreach (var con in connections)
			{
				var properties = con.Value.ConnectionProperties;
				dcf.RemoveConnectionProperties(con.Value, false, properties.Keys.ToArray());
			}
			dcf.RemoveConnections(sourceInterface, true, false, connections.Keys.ToArray());
			}
		}
#endif
	}
}
