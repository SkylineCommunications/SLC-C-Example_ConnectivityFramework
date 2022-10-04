using System;
using System.Text;

using ProtocolDCF;

using Skyline.DataMiner.Scripting;

public class QAction
{
	/// <summary>
	/// DCF_Example_GetInterfaces.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void Run(SLProtocolExt protocol)
	{
		DCFMappingOptions opt = new DCFMappingOptions();
		opt.HelperType = SyncOption.Custom;

		StringBuilder sb = new StringBuilder();

		using (DCFHelper dcf = new DCFHelper(protocol, Parameter.map_startupelements_63993, opt))
		{
			var results = (object[]) protocol.GetParameters(new uint[] { Parameter.propertynamegetinterfaces_152, Parameter.propertyvaluegetinterfaces_153 });

			string name = Convert.ToString(results[0]);
			string value = Convert.ToString(results[1]);

			var allInterfaces = dcf.GetInterfaces(new DCFDynamicLink(new PropertyFilter(name, value)))[0].AllInterfaces;

			foreach(var interf in allInterfaces)
			{
				sb.AppendLine("Interface found: " + interf.InterfaceId + " with name: " + interf.InterfaceName);
			}
		}

		protocol.Getinterfacesresult_151 = sb.ToString();
	}
}
