using System;
using System.Linq;

using Skyline.DataMiner.Core.ConnectivityFramework.Protocol;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Interfaces;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Options;
using Skyline.DataMiner.Scripting;

public class QAction
{
	/// <summary>
	/// DCF example interface properties.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void Run(SLProtocolExt protocol)
	{
		int triggerPid = protocol.GetTriggerParameter();
		DcfMappingOptions opt = new DcfMappingOptions
		{
			PIDcurrentInterfaceProperties = Parameter.mapinterfaceproperties_63999,
			HelperType = SyncOption.Custom,
		};

		using (DcfHelper dcf = new DcfHelper(protocol, Parameter.mapstartupelements_63993, opt))
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
					propertyType = "Output";
					break;
				case 102:
					propertyType = "Generic";
					break;
				default:
					// Do nothing.
					break;
			}

			var foundInterface = dcf.GetInterface(new DcfInterfaceFilterSingle(parameterGroupID, rowKey));

			string propertyName;
			string propertyValue;
			if (triggerPid == 1115)
			{
				propertyName = "A";
			}
			else
			{
				propertyName = "B";
			}

			propertyValue = Convert.ToString(protocol.GetParameter(triggerPid));
			var propertyRequest = new DcfSaveInterfacePropertyRequest(propertyName, propertyType, propertyValue);

			dcf.SaveInterfaceProperties(foundInterface, propertyRequest);
		}
	}
}
