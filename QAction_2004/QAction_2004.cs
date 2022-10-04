using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using ProtocolDCF;

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
		DCFMappingOptions opt = new DCFMappingOptions();
		opt.HelperType = SyncOption.Custom;
		using (DCFHelper dcf = new DCFHelper(protocol, false, opt))
		{
			DCFDynamicLinkResult[] result = dcf.GetInterfaces(new DCFDynamicLink(600, "*"));

			foreach (var res in result)
			{
				foreach (var itf in res.AllInterfaces)
				{
					protocol.Log("QA" + protocol.QActionID + "|itf|dynamicPK:" + itf.DynamicPK + "dynamicLink" + itf.DynamicLink, LogType.Error, LogLevel.NoLogging);
				}
			}
		}
	}
}