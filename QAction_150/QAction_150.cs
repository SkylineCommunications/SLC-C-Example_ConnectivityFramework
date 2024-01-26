using System;
using System.Text;

using Skyline.DataMiner.Core.ConnectivityFramework.Protocol;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Filters;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Interfaces;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Options;
using Skyline.DataMiner.Scripting;

public class QAction
{
	/// <summary>
	/// DCF_Example_GetInterfaces.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void Run(SLProtocolExt protocol)
	{
		DcfMappingOptions opt = new DcfMappingOptions
		{
			HelperType = SyncOption.Custom,
		};

		StringBuilder sb = new StringBuilder();

		using (DcfHelper dcf = new DcfHelper(protocol, Parameter.mapstartupelements_63993, opt))
		{
			var results = (object[])protocol.GetParameters(new uint[] { Parameter.propertynamegetinterfaces_152, Parameter.propertyvaluegetinterfaces_153 });
			string name = Convert.ToString(results[0]);
			string value = Convert.ToString(results[1]);

			var allInterfaces = dcf.GetInterfaces(new DcfInterfaceFilterMulti(new DcfPropertyFilter(name, value)));
			foreach (var interf in allInterfaces)
			{
				sb.AppendLine("Interface found: " + interf.InterfaceId + " with name: " + interf.InterfaceName);
			}
		}

		protocol.Getinterfacesresult_151 = sb.ToString();
	}
}