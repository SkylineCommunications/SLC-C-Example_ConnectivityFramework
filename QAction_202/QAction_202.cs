using System;

using Skyline.DataMiner.Scripting;

public class QAction
{
	/// <summary>
	/// Connect to interface.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	public static void Run(SLProtocolExt protocol)
	{
		string key = protocol.RowKey();
		string value = Convert.ToString(protocol.GetParameter(protocol.GetTriggerParameter()));

		if (key == value)
		{
			return;
		}

		protocol.uniquesourceconnections[key, Parameter.Uniquesourceconnections.Idx.uniquesourceconnectionsdestinationinterface_202] = value;
	}
}