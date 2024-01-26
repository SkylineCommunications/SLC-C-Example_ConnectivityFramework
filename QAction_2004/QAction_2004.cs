using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Skyline.DataMiner.Core.ConnectivityFramework.Protocol;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Interfaces;
using Skyline.DataMiner.Core.ConnectivityFramework.Protocol.Options;
using Skyline.DataMiner.Scripting;

/// <summary>
/// DataMiner QAction Class: MatrixFind.
/// </summary>
public class QAction
{
	/// <summary>
	/// The QAction entry point.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void Run(SLProtocol protocol)
	{
		DcfRemovalOptionsManual opt = new DcfRemovalOptionsManual
		{
			HelperType = SyncOption.Custom,
		};

		using (DcfHelper dcf = new DcfHelper(protocol, false, opt))
		{
			var result = dcf.GetInterfaces(new DcfInterfaceFilterMulti(600, "*"));
			foreach (var itf in result)
			{
				protocol.Log("QA" + protocol.QActionID + "|itf|dynamicPK:" + itf.DynamicPK + "dynamicLink" + itf.DynamicLink, LogType.Error, LogLevel.NoLogging);
			}
		}
	}
}