//#define DCFv1
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
	/// DCF_Example_GetInterfaces
	/// </summary>
	/// <param name="protocol">Link with Skyline Dataminer</param>
	public static void Run(SLProtocolExt protocol)
	{
#if DCFv1
		DCFMappingOptions opt = new DCFMappingOptions();
		opt.HelperType = SyncOption.Custom;
		StringBuilder sb = new StringBuilder();
		using (DCFHelper dcf = new DCFHelper(protocol, Parameter.map_startupelements_63993, opt))
		{
			string name = Convert.ToString(protocol.GetParameter(152));
			string value = Convert.ToString(protocol.GetParameter(153));
			var allInterfaces = dcf.GetInterfaces(new DCFDynamicLink(new PropertyFilter(name, value)))[0].allInterfaces;
			foreach(var interf in allInterfaces){

				sb.AppendLine("Interface Found: " + interf.InterfaceId + " with Name: " + interf.InterfaceName);
			}
		}
		protocol.GetInterfacesResult_151 = sb.ToString();
#endif
	}

}
