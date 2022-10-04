
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Skyline.DataMiner.Scripting;

public class QAction
{
	/// <summary>
	/// ContextMenu
	/// </summary>
	/// <param name="protocol">Link with Skyline Dataminer</param>
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
				protocol.SetParameter(999, "");
				protocol.SetParameter(998, "");
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
			protocol.AddRow(tableID, new DriverInterfacesQActionRow() { DifKey_111 = tableKey, DifDescriptionIDX_112 = tableKeyO,DifType_113 = 100 });
			string stringValue = Convert.ToString(protocol.GetParameter(998));
			stringValue += (String.IsNullOrEmpty(stringValue) ? "" : ";") + tableKey;
			protocol.SetParameter(998, stringValue);
		}
	}
	private static void AddOutputInterface(SLProtocolExt protocol, int tableID, string[] sa)
	{
		string tableKeyO = sa[2].Trim();
		tableKeyO = "O_" + tableKeyO;
		string tableKey = Convert.ToString(GetFirstAvailableIntegerIndex(protocol, 100));
		if (!protocol.Exists(tableID, tableKey))
		{
			protocol.AddRow(tableID, new DriverInterfacesQActionRow() { DifKey_111 = tableKey, DifDescriptionIDX_112 = tableKeyO, DifType_113 = 101 });
			string stringValue = Convert.ToString(protocol.GetParameter(999));
			stringValue += (String.IsNullOrEmpty(stringValue) ? "" : ";") + tableKey;
			protocol.SetParameter(999, stringValue);
		}
	}
	private static void AddVirtualInterface(SLProtocolExt protocol, int tableID, string[] sa)
	{
		string tableKeyO = sa[2].Trim();
		tableKeyO = "V_" + tableKeyO;
		string tableKey = Convert.ToString(GetFirstAvailableIntegerIndex(protocol, 100));
		if (!protocol.Exists(tableID, tableKey))
		{

			protocol.AddRow(tableID, new DriverInterfacesQActionRow() { DifKey_111 = tableKey, DifDescriptionIDX_112 = tableKeyO, DifType_113=102 });

			for (int i = 998; i <= 999; i++)
			{
				string stringValue = Convert.ToString(protocol.GetParameter(i));
				stringValue += (String.IsNullOrEmpty(stringValue) ? "" : ";") + tableKey;
				protocol.SetParameter(i, stringValue);
			}
		}
	}

	public static int GetFirstAvailableIntegerIndex(SLProtocol protocol, int TableId)
	{
		Object[] columns = (Object[])protocol.NotifyProtocol(321 /*NT_GT_TABLE_COLUMNS*/, TableId, new UInt32[] { 0 });
		Object[] instance = (Object[])columns[0];
		int NewId = 1;
		if (instance.Length != 0)
		{
			Int32[] PrimaryKeys = Array.ConvertAll<Object, Int32>(instance, new Converter<Object, Int32>(Convert.ToInt32));
			int? firstAvailable = Enumerable.Range(1, int.MaxValue)
								.Except(PrimaryKeys)
								.FirstOrDefault();
			if (firstAvailable != null)
				NewId = Convert.ToInt32(firstAvailable);
		}

		return NewId;
	}
	public static int[] GetFirstAvailableIntegerIndex(SLProtocol protocol, int TableId, int allKeys)
	{
		Object[] columns = (Object[])protocol.NotifyProtocol(321 /*NT_GT_TABLE_COLUMNS*/, TableId, new UInt32[] { 0 });
		Object[] instance = (Object[])columns[0];
		if (instance.Length != 0)
		{
			Int32[] PrimaryKeys = Array.ConvertAll<Object, Int32>(instance, new Converter<Object, Int32>(Convert.ToInt32));
			var firstAvailable = Enumerable.Range(1, int.MaxValue)
								.Except(PrimaryKeys)
								.Take(allKeys);
			return firstAvailable.ToArray();
		}
		else
		{
			return Enumerable.Range(1, allKeys).ToArray();
		}

	}
}
             
