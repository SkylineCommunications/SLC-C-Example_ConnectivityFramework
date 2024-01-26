using System.Text;

using Skyline.DataMiner.Core.ConnectivityFramework.Protocol;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Connections;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Interfaces;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Options;
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
	Depending on the Destination togglebutton the connection inside of the DVE will be different.
DCF Example - Matrix
    Displays a matrix that's connected to the DCF module in DataMiner.
    Every Input and Output is an interface and every connection you make on the matrix will automatically create a DCF connection in the background.
    The Log Matrix Interface button runs a small example code that will filter and show every interface made from the Matrix in the logging.
");
		sb.AppendLine();
		protocol.SetParameter(Parameter.help_1, sb.ToString());

		DcfMappingOptions opt = new DcfMappingOptions();
		opt.PIDcurrentConnections = Parameter.mapconnections_63998;
		opt.PIDcurrentConnectionProperties = Parameter.mapconnectionproperties_63997;
		opt.HelperType = SyncOption.Custom;

		// Setting a DCFHelper StartupCheckPID will perform startup checks for all defined elements if they haven't already been performed
		using (DcfHelper dcf = new DcfHelper(protocol, Parameter.mapstartupelements_63993, opt))
		{
			// Creating static connections from virtual A to out A1 and A2 and Virtual B to out B1 and B2. These will never be automatically cleared.
			// Static connections from virtual A(9) to A1(4) and A2 (5).
			DcfSaveConnectionRequest[] allConnections_A = new[]
			{
				new DcfSaveConnectionRequest(dcf, new DcfInterfaceFilterSingle(9), new DcfInterfaceFilterSingle(4),SaveConnectionType.Unique_Name,"Fixed A1",true),
				new DcfSaveConnectionRequest(dcf, new DcfInterfaceFilterSingle(9),new DcfInterfaceFilterSingle(5),SaveConnectionType.Unique_Name,"Fixed A2",true),
			};

			// By setting the fixedConnection boolean to true, these connections can only be cleaned up with a manual delete and not with EndOfPolling.
			var resultA = dcf.SaveConnections(allConnections_A);

			// Static connections from virtual B(10) to B1(6) and B2 (7)
			DcfSaveConnectionRequest[] allConnections_B = new[]
			{
				new DcfSaveConnectionRequest(dcf, new DcfInterfaceFilterSingle(10), new DcfInterfaceFilterSingle(6),SaveConnectionType.Unique_Name,"Fixed B1",true),
				new DcfSaveConnectionRequest(dcf,new DcfInterfaceFilterSingle(10),new DcfInterfaceFilterSingle(7),SaveConnectionType.Unique_Name,"Fixed B2",true),
			};

			// By setting the fixedConnection boolean to true, these connections can only be cleaned up with a manual delete and not with EndOfPolling
			var resultB = dcf.SaveConnections(allConnections_B);

			// Add some static Properties.
			foreach (var res in resultA)
			{
				if (res.SourceConnection != null)
				{
					var property = new ConnectivityConnectionProperty { ConnectionPropertyName = "Passive Component", ConnectionPropertyType = "generic", ConnectionPropertyValue = "Fixed" };
					DcfSaveConnectionPropertyRequest request = new DcfSaveConnectionPropertyRequest(property, true);
					dcf.SaveConnectionProperties(
						res.SourceConnection,
						request
						);
				}
			}

			foreach (var res in resultB)
			{
				if (res.SourceConnection != null)
				{
					var property = new ConnectivityConnectionProperty { ConnectionPropertyName = "Passive Component", ConnectionPropertyType = "generic", ConnectionPropertyValue = "Fixed" };
					var request = new DcfSaveConnectionPropertyRequest(property, true);
					dcf.SaveConnectionProperties(
						res.SourceConnection,
						request
						);
				}
			}
		}
	}
}
