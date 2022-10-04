using System.Text;

using ProtocolDCF;

using Skyline.DataMiner.Scripting;

public class QAction
{
	/// <summary>
	/// After startup logic.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void Run(SLProtocolExt protocol)
	{
		StringBuilder sb = new StringBuilder();
		sb.AppendLine("The DCF Simple Example Driver");
		sb.AppendLine("--------------------------------------");
		sb.AppendLine("The QuickAction 1: DCFHelper contains the namespace ProtocolDCF that can be copy pasted into other drivers.");
		sb.AppendLine();
		sb.AppendLine(@"This driver contains 4 Pages.
The DCF Example - ... pages contain examples for specific DCF Situations.
Clicking Save on a specific page will remove any DCF Connection and Property Configuration you applied on the other pages.
An exception is the Fixed Connections made between virtual interfaces for DCF Example - Fixed Interfaces.

DCF Example - Fixed Interfaces
	This displays two parameters, depending on the toggled state of these two parameters different connections will be made between fixed interfaces.
DCF Example - Unique Source
	This uses the Interfaces setup in the Table Interfaces page.
	The Unique Source example allows you to add rows to the table to specify a connection where each source interface can only have a single connection.
DCF Example - DVE
	Allows a user to add DVE's to the driver. Each DVE has one Source interface and two destinations.
	Depending on the Destination togglebutton the connection inside of the DVE will be different.");
		sb.AppendLine();
		protocol.SetParameter(Parameter.help_1, sb.ToString());

		DCFMappingOptions opt = new DCFMappingOptions();
		opt.PIDcurrentConnections = Parameter.map_connections_63998;
		opt.PIDcurrentConnectionProperties = Parameter.map_connectionproperties_63997;
		opt.HelperType = SyncOption.Custom;

		// Setting a DCFHelper StartupCheckPID will perform startup checks for all defined elements if they haven't already been performed
		using (DCFHelper dcf = new DCFHelper(protocol, Parameter.map_startupelements_63993, opt))
		{
			// Creating static connections from virtual A to out A1 and A2 and Virtual B to out B1 and B2. These will never be automatically cleared.
			// Static connections from virtual A(9) to A1(4) and A2 (5).
			DCFSaveConnectionRequest[] allConnections_A = new DCFSaveConnectionRequest[]
			{
				new DCFSaveConnectionRequest(dcf, new DCFDynamicLink(9), new DCFDynamicLink(4),SaveConnectionType.Unique_Name,"Fixed A1",true),
				new DCFSaveConnectionRequest(dcf,new DCFDynamicLink(9),new DCFDynamicLink(5),SaveConnectionType.Unique_Name,"Fixed A2",true)
			};

			// By setting the fixedConnection boolean to true, these connections can only be cleaned up with a manual delete and not with EndOfPolling.
			var resultA = dcf.SaveConnections(allConnections_A);

			// Static connections from virtual B(10) to B1(6) and B2 (7)
			DCFSaveConnectionRequest[] allConnections_B = new DCFSaveConnectionRequest[]
			{
				new DCFSaveConnectionRequest(dcf, new DCFDynamicLink(10), new DCFDynamicLink(6),SaveConnectionType.Unique_Name,"Fixed B1",true),
				new DCFSaveConnectionRequest(dcf,new DCFDynamicLink(10),new DCFDynamicLink(7),SaveConnectionType.Unique_Name,"Fixed B2",true)
			};

			// By setting the fixedConnection boolean to true, these connections can only be cleaned up with a manual delete and not with EndOfPolling
			var resultB = dcf.SaveConnections(allConnections_B);

			// Add some static Properties.
			foreach (var res in resultA)
			{
				if (res.sourceConnection != null)
				{
					dcf.SaveConnectionProperties(res.sourceConnection, false, true, new ConnectivityConnectionProperty() {ConnectionPropertyName = "Passive Component",ConnectionPropertyType ="generic", ConnectionPropertyValue = "Fixed" });
				}
			}

			foreach (var res in resultB)
			{
				if (res.sourceConnection != null)
				{
					dcf.SaveConnectionProperties(res.sourceConnection, false, true, new ConnectivityConnectionProperty() { ConnectionPropertyName = "Passive Component", ConnectionPropertyType = "generic", ConnectionPropertyValue = "Fixed" });
				}
			}
		}
	}
}
