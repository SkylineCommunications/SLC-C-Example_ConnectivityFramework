using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Skyline.DataMiner.Scripting;

public class QAction
{
	/// <summary>
	/// FunctionName
	/// </summary>
	/// <param name="protocol">Link with Skyline Dataminer</param>
	public static void Run(SLProtocolExt protocol)
	{
		string key = protocol.RowKey();
		string value = Convert.ToString(protocol.GetParameter(protocol.GetTriggerParameter()));
		if (key == value) return;

		protocol.uniquesourceconnections[key, Parameter.Uniquesourceconnections.Idx.usDestinationInterface_202] = value;

	}

}
