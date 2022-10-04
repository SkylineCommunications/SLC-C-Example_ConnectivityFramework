using System;

using ProtocolDCF;

using Skyline.DataMiner.Scripting;

public class QAction
{
	/// <summary>
	/// DCF example interface properties.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void Run(SLProtocolExt protocol)
	{
		int trig = protocol.GetTriggerParameter();
		DCFMappingOptions opt = new DCFMappingOptions();
		opt.PIDcurrentInterfaceProperties = Parameter.map_interfaceproperties_63999;
		opt.HelperType = SyncOption.Custom;

		using (DCFHelper dcf = new DCFHelper(protocol, Parameter.map_startupelements_63993, opt))
		{
			string rowKey = protocol.RowKey();
			DriverinterfacesQActionRow row = protocol.driverinterfaces[rowKey];
			int parameterGroupID = Convert.ToInt32(row.Driverinterfacestype_113);
			string propertyType = String.Empty;

			switch (parameterGroupID)
			{
				case 100:
					propertyType = "Input";
					break;
				case 101:
					propertyType = "Ouptut";
					break;
				case 102:
					propertyType = "Generic";
					break;
				default:
					// Do nothing.
					break;
			}

			var foundInterface = dcf.GetInterfaces(new DCFDynamicLink(parameterGroupID, rowKey))[0].FirstInterface;
			string propertyName;
			string propertyValue;
			if (trig == 1115)
			{
				propertyName = "A";
			}
			else
			{
				propertyName = "B";
			}

			propertyValue = Convert.ToString(protocol.GetParameter(trig));
			dcf.SaveInterfaceProperties(foundInterface, false, new ConnectivityInterfaceProperty { InterfacePropertyName = propertyName, InterfacePropertyType = propertyType, InterfacePropertyValue = propertyValue });
		}
	}
}
