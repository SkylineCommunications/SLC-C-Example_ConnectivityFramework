#define DCFv1
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
	/// DCF_Example_InterfaceProperties
	/// </summary>
	/// <param name="protocol">Link with Skyline Dataminer</param>
	public static void Run(SLProtocolExt protocol)
	{
#if DCFv1
		int trig = protocol.GetTriggerParameter();
		DCFMappingOptions opt = new DCFMappingOptions();
		opt.PIDcurrentInterfaceProperties = Parameter.map_interfaceproperties_63999;
		opt.HelperType = SyncOption.Custom;
		using (DCFHelper dcf = new DCFHelper(protocol, Parameter.map_startupelements_63993, opt))
		{
			string rowKey = protocol.RowKey();
			DriverInterfacesQActionRow row = protocol.driverInterfaces[rowKey];
			int parameterGroupID = Convert.ToInt32(row.DifType_113);
			string propertyType = "";

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
			}

			var foundInterface = dcf.GetInterfaces(new DCFDynamicLink(parameterGroupID, rowKey))[0].firstInterface;
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
			dcf.SaveInterfaceProperties(foundInterface, false, new ConnectivityInterfaceProperty() { InterfacePropertyName = propertyName, InterfacePropertyType = propertyType, InterfacePropertyValue = propertyValue });
		}
#endif
	}
}
