using System;
using System.Linq;

using Skyline.DataMiner.Scripting;

public class QAction
{
	/// <summary>
	/// Context menu.
	/// </summary>
	/// <param name="protocol">Link with SLProtocol process.</param>
	/// <param name="extraData">Context menu data.</param>
	public static void Run(SLProtocolExt protocol, object extraData)
	{
		int iTrigger = protocol.GetTriggerParameter();
		int tableID = iTrigger - 1000;

		// sa[0] = unique client id
		// sa[1] = command (= value of discreet defined on this parameter)
		// sa[n] = optional arguments depending on command
		var sa = extraData as string[];

		if (sa == null || sa.Length < 2)
			return;

		switch (sa[1])
		{
			case "add inp":
				AddInputInterface(protocol,tableID, sa);
				break;
			case "add out":
				AddOutputInterface(protocol, tableID, sa);
				break;
			case "add virt":
				AddVirtualInterface(protocol, tableID, sa);
				break;
			case "delete Int":
				DeleteInterfaces(protocol, tableID, sa);
				break;
			case "clear Int":
				protocol.ClearAllKeys(tableID);
				protocol.SetParameter(Parameter.alloutputs_999, String.Empty);
				protocol.SetParameter(Parameter.allinputs_998, String.Empty);
				break;
			case "add connection":
				AddConnection(protocol, tableID, sa);
				break;
			case "delete connection":
				DeleteRows(protocol, tableID, sa);
				break;
			case "add dve":
				string tableKey = sa[2].Trim();
				if (!protocol.Exists(tableID, tableKey))
				{
					protocol.AddRow(tableID, tableKey);
					protocol.SetParameterIndexByKey(tableID, tableKey, 2, "Name "+tableKey);
				}

				break;
			case "delete":
				string[] sDelete = sa.Skip(2).ToArray();
				protocol.DeleteRow(tableID, sDelete);
				break;
			case "clear":
				protocol.ClearAllKeys(tableID);
				break;
			default:
				// Do nothing.
				break;
		}
	}

	private static void AddConnection(SLProtocolExt protocol, int tableID, string[] sa)
	{
		string tableKeyO = sa[2].Trim();
		if (!protocol.Exists(tableID, tableKeyO))
			protocol.AddRow(tableID, tableKeyO);
	}

	private static void DeleteInterfaces(SLProtocolExt protocol, int tableID, string[] sa)
	{
		string[] sDelete = DeleteRows(protocol, tableID, sa);

		for (int i = 998; i <= 999; i++)
		{
			string allDiscreets = Convert.ToString(protocol.GetParameter(i));
			string[] allDiscreetsA = allDiscreets.Split(';');
			var newDiscreets = allDiscreetsA.Except(sDelete);
			string newBuffer = String.Join(";", newDiscreets);
			protocol.SetParameter(i, newBuffer);
		}
	}

	private static string[] DeleteRows(SLProtocolExt protocol, int tableID, string[] sa)
	{
		string[] sDelete = sa.Skip(2).ToArray();
		protocol.DeleteRow(tableID, sDelete);

		return sDelete;
	}

	private static void AddInputInterface(SLProtocolExt protocol,int tableID, string[] sa)
	{
		string tableKeyO = sa[2].Trim();
		tableKeyO = "I_" + tableKeyO;
		string tableKey = Convert.ToString(GetFirstAvailableIntegerIndex(protocol, 100));

		if (!protocol.Exists(tableID, tableKey))
		{
			protocol.AddRow(tableID, new DriverinterfacesQActionRow { Driverinterfaceskey_111 = tableKey, Driverinterfacesdescription_112 = tableKeyO,Driverinterfacestype_113 = 100 });
			string stringValue = Convert.ToString(protocol.GetParameter(Parameter.allinputs_998));
			stringValue += (String.IsNullOrEmpty(stringValue) ? String.Empty : ";") + tableKey;
			protocol.SetParameter(Parameter.allinputs_998, stringValue);
		}
	}

	private static void AddOutputInterface(SLProtocolExt protocol, int tableID, string[] sa)
	{
		string tableKeyO = sa[2].Trim();
		tableKeyO = "O_" + tableKeyO;
		string tableKey = Convert.ToString(GetFirstAvailableIntegerIndex(protocol, 100));

		if (!protocol.Exists(tableID, tableKey))
		{
			protocol.AddRow(tableID, new DriverinterfacesQActionRow { Driverinterfaceskey_111 = tableKey, Driverinterfacesdescription_112 = tableKeyO, Driverinterfacestype_113 = 101 });
			string stringValue = Convert.ToString(protocol.GetParameter(Parameter.alloutputs_999));
			stringValue += (String.IsNullOrEmpty(stringValue) ? String.Empty : ";") + tableKey;
			protocol.SetParameter(Parameter.alloutputs_999, stringValue);
		}
	}

	private static void AddVirtualInterface(SLProtocolExt protocol, int tableID, string[] sa)
	{
		string tableKeyO = sa[2].Trim();
		tableKeyO = "V_" + tableKeyO;
		string tableKey = Convert.ToString(GetFirstAvailableIntegerIndex(protocol, 100));
		if (!protocol.Exists(tableID, tableKey))
		{
			protocol.AddRow(tableID, new DriverinterfacesQActionRow { Driverinterfaceskey_111 = tableKey, Driverinterfacesdescription_112 = tableKeyO, Driverinterfacestype_113=102 });

			for (int i = 998; i <= 999; i++)
			{
				string stringValue = Convert.ToString(protocol.GetParameter(i));
				stringValue += (String.IsNullOrEmpty(stringValue) ? String.Empty : ";") + tableKey;
				protocol.SetParameter(i, stringValue);
			}
		}
	}

	private static int GetFirstAvailableIntegerIndex(SLProtocol protocol, int tableId)
	{
		object[] columns = (object[])protocol.NotifyProtocol(321 /*NT_GT_TABLE_COLUMNS*/, tableId, new UInt32[] { 0 });
		object[] instance = (object[])columns[0];

		int newId = 1;
		if (instance.Length != 0)
		{
			int[] primaryKeys = Array.ConvertAll<object, int>(instance, new Converter<object, int>(Convert.ToInt32));
			int? firstAvailable = Enumerable.Range(1, int.MaxValue)
								.Except(primaryKeys)
								.FirstOrDefault();

			if (firstAvailable != null)
				newId = Convert.ToInt32(firstAvailable);
		}

		return newId;
	}
}