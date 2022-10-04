#define debug

namespace ProtocolDCF
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Text;

	using Interop.SLDms;

	using Skyline.DataMiner.Net.Messages;
	using Skyline.DataMiner.Scripting;

	/*
* 13/02/2015	1.0.0.1		JST, Skyline	Initial Version
* 04/03/2015	1.0.0.2		JST, Skyline	DCFHelper Fix: EndOfPolling wasn't cleaning up connections unless it also detected different Interfaces.
* 02/08/2015	1.0.0.3		JST, Skyline	New Features: New GetInterfaces  new SaveConnections, Saving Fixed Connections,  General Fixes and Efficiency improvements
* 23/02/2016	1.0.0.4		JST, Skyline	Fixed issues with String.Format in exceptionLogging
* 23/09/2016   1.0.0.5     JST, Skyline    *External Elements and DVE's that are detected as 'not active' will no longer cause the DCFHelper to stop. They are instead added to an unloadedElements list.
*											*Adding a Property to an external connection adds that property to both connections
* 04/10/2016	1.0.0.6		JST, Skyline	Added Support for Connection Filters (Client-Side Filtering using Filter column)
* 21/10/2016	1.0.0.7		JST, Skyline	New Features: Find Interface based on a Property it has.
* 02/12/2016	1.0.0.8		JST, Skyline	DCFHelper Fix: Requesting Stand-Alone interfaces failed with GetInterfaces
* 13/02/2018	1.0.0.9		JST, Skyline	Refactor to match new Code Conventions
* 12/10/2018	1.0.0.10	JST, Skyline	Fix: Adding Properties to external connections tried to always add on the return connection regardless of provided option.
* 
* Important Note: StyleCop rules SA1307 and SA1401 concerning the usage of public fields cannot be fixed. This is due to reverse compatibility issue. 
*/

	#region Enumerations

	/// <summary>
	/// SaveConnectionType indicates how to find previous connections and figure out if a new connection needs to update an old one or simply get added.
	/// </summary>
	public enum SaveConnectionType
	{
		/// <summary>
		/// Indicates that the Source Interface can only have a single Connection.
		/// </summary>
		Unique_Source,

		/// <summary>
		/// Indicates that the Destination Interface can only have a single Connection.
		/// </summary>
		Unique_Destination,

		/// <summary>
		/// Indicates that the name of a connection is Unique.
		/// </summary>
		Unique_Name,

		/// <summary>
		/// Indicates that there can only be a single connection between a single source and destination interface.
		/// </summary>
		Unique_SourceAndDestination,

		///// <summary>
		///// Indicates that there can only be a single connection that has a specific property assigned to it.
		///// </summary>
		/////Unique_Property
	}

	/// <summary>
	/// HelperType option is by default Custom.
	/// </summary>
	public enum SyncOption
	{
		/// <summary>
		/// This will update the currentMapping every time a remove or add is performed.
		/// </summary>
		Custom,

		/// <summary>
		/// Can be used when you received part of the whole DCF structure from a device and wish to keep track of it until the end of a buffer.
		/// </summary>
		PollingSync,

		/// <summary>
		///  Can be used when you have received and parsed all the data from a device and wish to automatically remove data you didn't  receiving during the refresh.
		/// </summary>
		EndOfPolling
	}

	#endregion Enumerations

	#region Structs

	public struct DCFSaveConnectionProperty
	{
		private readonly bool async;
		private readonly bool fixedProperty;
		private readonly bool full;
		private readonly string name;

		private readonly bool addToExternalConnection;

		private readonly string type;

		private readonly string value;

		public DCFSaveConnectionProperty(string name, string type, string value, bool full = false, bool fixedProperty = false, bool addToExternalConnection = true, bool async = true)
		{
			this.name = name;
			this.type = type;
			this.value = value;
			this.full = full;
			this.fixedProperty = fixedProperty;
			this.addToExternalConnection = addToExternalConnection;
			this.async = async;
		}

		public DCFSaveConnectionProperty(ConnectivityConnectionProperty property, bool full = false, bool fixedProperty = false, bool addToExternalConnection = true, bool async = true)
			: this(property.ConnectionPropertyName, property.ConnectionPropertyType, property.ConnectionPropertyValue, full, fixedProperty, addToExternalConnection, async)
		{
		}

		public bool Async
		{
			get { return async; }
		}

		public bool FixedProperty
		{
			get { return fixedProperty; }
		}

		public bool Full
		{
			get { return full; }
		}

		public string Name
		{
			get { return name; }
		}

		public bool OnBothConnections
		{
			get { return addToExternalConnection; }
		}

		public string Type
		{
			get { return type; }
		}

		public string Value
		{
			get { return this.value; }
		}
	}

	#endregion Structs

	#region Classes

	/// <summary>
	/// Objects of this class represent a unique Interface, specified by its ParameterGroupID. In case of a table, the Key of the table must also be specified. In case of external element, the elementKey (DmaID/EleID) must also be specified.
	/// </summary>
	public class DCFDynamicLink
	{
		private readonly bool custom;
		private readonly string elementKey;
		private readonly bool getAll = false;
		private readonly string interfaceName;
		private readonly int parameterGroupID;
		private PropertyFilter propertyFilter = null;
		private readonly string tableKey;

		// gets all the interfaces
		public DCFDynamicLink(string elementKey, PropertyFilter propertyFilter = null)
		{
			this.elementKey = elementKey;
			this.getAll = true;
			this.PropertyFilter = propertyFilter;
		}

		// PropertyFilter
		public DCFDynamicLink(PropertyFilter propertyFilter = null) : this("local", propertyFilter)
		{
		}

		// Gets interfaces based on the name
		public DCFDynamicLink(string interfaceName, string elementKey, bool customName = false, PropertyFilter propertyFilter = null)
		{
			this.interfaceName = interfaceName;
			this.elementKey = elementKey;
			this.custom = customName;
			this.PropertyFilter = propertyFilter;
		}

		public DCFDynamicLink(string interfaceName, bool customName = false, PropertyFilter propertyFilter = null) : this(interfaceName, "local", customName, propertyFilter)
		{
		}

		public DCFDynamicLink(string interfaceName, int dmaID, int eleID, bool customName = false, PropertyFilter propertyFilter = null) : this(interfaceName, dmaID + "/" + eleID, customName, propertyFilter)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DCFDynamicLink"/> class. Creates a DCFDynamicLink object that links to a single Interface on the DataMiner System.
		/// </summary>
		/// <param name="parameterGroupID">ParameterGroup that creates the Interface.</param>
		/// <param name="propertyFilter">Allows an additional filter.</param>
		public DCFDynamicLink(int parameterGroupID, PropertyFilter propertyFilter = null) : this(parameterGroupID, null, "local", propertyFilter)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DCFDynamicLink"/> class. It links to a single Interface on the DataMiner System (for tables, see the constructor with tableKey).
		/// </summary>
		/// <param name="parameterGroupID">ParameterGroup that creates the Interface.</param>
		/// <param name="dmaID">DMAId of the Element Containing the Interface.</param>
		/// <param name="eleID">EleId of the Element Containing the Interface.</param>
		/// <param name="propertyFilter">Allows an additional filter.</param>
		public DCFDynamicLink(int parameterGroupID, int dmaID, int eleID, PropertyFilter propertyFilter = null) : this(parameterGroupID, null, dmaID + "/" + eleID, propertyFilter)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DCFDynamicLink"/> class. Itt links to a single Interface on the DataMiner System.
		/// </summary>
		/// <param name="parameterGroupID">ParameterGroup that creates the Interface.</param>
		/// <param name="tableKey">Key of the row that creates an Interface, enter * to retrieve the whole table.</param>
		/// <param name="propertyFilter">Allows an additional filter.</param>
		public DCFDynamicLink(int parameterGroupID, string tableKey, PropertyFilter propertyFilter = null) : this(parameterGroupID, tableKey, "local", propertyFilter)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DCFDynamicLink"/> class. It links to a single Interface on the DataMiner System.
		/// </summary>
		/// <param name="parameterGroupID">ParameterGroup that creates the Interface.</param>
		/// <param name="tableKey">Key of the row that creates an Interface, enter * to retrieve the whole table.</param>
		/// <param name="elementKey">DmaID/EleID of the element where the Interface is found.</param>
		/// <param name="propertyFilter">Allows an additional filter.</param>
		public DCFDynamicLink(int parameterGroupID, string tableKey, string elementKey, PropertyFilter propertyFilter = null)
		{
			this.parameterGroupID = parameterGroupID;
			this.tableKey = tableKey;
			this.elementKey = elementKey;
			this.PropertyFilter = propertyFilter;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DCFDynamicLink"/> class. It links to a single Interface on the DataMiner System.
		/// </summary>
		/// <param name="parameterGroupID">ParameterGroup that creates the Interface.</param>
		/// <param name="tableKey">Key of the row that creates an Interface, enter * to retrieve the whole table.</param>
		/// <param name="dmaID">DMAId of the Element Containing the Interface.</param>
		/// <param name="eleID">EleId of the Element Containing the Interface.</param>
		/// <param name="propertyFilter">Allows an additional filter.</param>
		public DCFDynamicLink(int parameterGroupID, string tableKey, int dmaID, int eleID, PropertyFilter propertyFilter = null) : this(parameterGroupID, tableKey, dmaID + "/" + eleID, propertyFilter)
		{
		}

		public bool Custom
		{
			get { return custom; }
		}

		public string ElementKey
		{
			get { return elementKey; }
		}

		public bool GetAll
		{
			get { return getAll; }
		}

		public string InterfaceName
		{
			get { return interfaceName; }
		}

		public int ParameterGroupID
		{
			get { return parameterGroupID; }
		}

		public PropertyFilter PropertyFilter
		{
			get { return propertyFilter; }
			set { propertyFilter = value; }
		}

		public string TableKey
		{
			get { return tableKey; }
		}
	}

	/// <summary>
	/// Contains The Result from a GetInterfaces Query. If a specific DCFDynamicLink was not found then this object will be null.
	/// </summary>
	[SuppressMessage("Microsoft.StyleCop.CSharp.NamingRules", "*", Justification = "Reverse Compatibility")]
	[SuppressMessage("Microsoft.StyleCop.CSharp.MaintainabilityRules", "*", Justification = "Reverse Compatibility")]
	public class DCFDynamicLinkResult
	{
		public ConnectivityInterface[] AllInterfaces { get; }
		public ConnectivityInterface FirstInterface { get; }
		public DCFDynamicLink link;

		public DCFDynamicLinkResult(DCFDynamicLink link, ConnectivityInterface[] allInterfaces)
		{
			this.link = link;
			if (allInterfaces != null && allInterfaces.Length > 0)
			{
				AllInterfaces = allInterfaces;
				this.FirstInterface = allInterfaces[0];
			}
			else
			{
				AllInterfaces = new ConnectivityInterface[0];
				this.FirstInterface = null;
			}
		}
	}

	[SuppressMessage("Microsoft.StyleCop.CSharp.NamingRules", "*", Justification = "Reverse Compatibility")]
	[SuppressMessage("Microsoft.StyleCop.CSharp.MaintainabilityRules", "*", Justification = "Reverse Compatibility")]
	public class DCFHelper : IDisposable
	{
		#region Fields

		// Key = connectivityConnection.ConnectionId + "-" + connectivityConnection.SourceDataMinerId + "/" + connectivityConnection.SourceElementId;
		public Dictionary<string, Dictionary<string, ConnectivityConnectionProperty>> connectionProperties = new Dictionary<string, Dictionary<string, ConnectivityConnectionProperty>>();

		// Key = input.InterfaceId + "-" + input.DataMinerId + "/" + input.ElementId;
		public Dictionary<string, Dictionary<string, ConnectivityConnection>> connections = new Dictionary<string, Dictionary<string, ConnectivityConnection>>();

		// Key = connectivityInterface.InterfaceId + "-" + connectivityInterface.ElementKey
		public Dictionary<string, Dictionary<string, ConnectivityInterfaceProperty>> interfaceProperties = new Dictionary<string, Dictionary<string, ConnectivityInterfaceProperty>>();

		public Dictionary<string, FastCollection<ConnectivityInterface>> interfaces = new Dictionary<string, FastCollection<ConnectivityInterface>>();

		// These methods all use a custom column to be defined in all Interface Generating Tables. This Custom Column should contain a unique value across the whole driver that can then be used to quickly get the Interface Object or create connections without needing to use the Name Description or Key
		// Internal Mapping for SearchValue
		public Dictionary<string, ConnectivityInterface> interfacesSV = new Dictionary<string, ConnectivityInterface>();

		private HashSet<string> cachedTables = new HashSet<string>();
		private int cConnectionPropPID = -1;
		private int cConnectionsPID = -1;
		private int cInterfacePropPID = -1;
		private Dictionary<string, HashSet<int>> currentConnectionProperties = new Dictionary<string, HashSet<int>>();
		private Dictionary<string, HashSet<int>> currentConnections = new Dictionary<string, HashSet<int>>();
		private Dictionary<string, HashSet<int>> currentInterfaceProperties = new Dictionary<string, HashSet<int>>();
		private SyncOption helperType;
		private Dictionary<string, FastCollection<ConnectivityInterfaceProperty>> interfacePropertiesPerElement = new Dictionary<string, FastCollection<ConnectivityInterfaceProperty>>();
		private int localDMAID;
		private int localEleID;
		private string localElementKey;

		// Key: dmaid/eleid  Value: FastCollection of ConnectivityConnections
		private Dictionary<string, FastCollection<ConnectivityConnection>> map_AllConnections = new Dictionary<string, FastCollection<ConnectivityConnection>>();

		private int newConnectionPropPID = -1;
		private int newConnectionsPID = -1;
		private Dictionary<string, HashSet<int>> newConnectionProperties = new Dictionary<string, HashSet<int>>();
		private Dictionary<string, HashSet<int>> newConnections = new Dictionary<string, HashSet<int>>();
		private Dictionary<string, HashSet<int>> newInterfaceProperties = new Dictionary<string, HashSet<int>>();
		private int newInterfacePropID = -1;
		private SLProtocol protocol;
		private HashSet<string> unloadedElements = new HashSet<string>();

		#endregion Fields

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="DCFHelper"/> class. It allows Manipulation of DCF Connections and Properties. Please use this inside of a 'using'-statement.
		/// </summary>
		/// <param name="protocol">The SLProtocol Object.</param>
		/// <param name="startupCheckPID">Indicates a Parameter (saved="false") ID that will hold a mapping to indicate if a StartupCheck was already performed for a specific element (main/DVE or External).</param>
		/// <param name="options">DCFMappingOptions: Indicates what PIDs are to be used for mapping. A lighter Object with limited functionality can be created by only providing certain PIDs. Example: Only providing the CurrentConnectionsPID will allow Adding and Removing of connections but not Properties.</param>
		/// <param name="dves">An array of DVEColumn objects identifying all 'element' columns of DVE Tables that also export Interfaces that will be used by the DCFHelper.</param>
		/// <param name="externalElements">An array of External Elements that will be used by the DCFHelper and need a Startup check.</param>
		public DCFHelper(SLProtocol protocol, int startupCheckPID, DCFMappingOptions options, DVEColumn[] dves, ExternalElement[] externalElements)
		{
			this.protocol = protocol;
			helperType = options.HelperType;
			localDMAID = protocol.DataMinerID;
			localEleID = protocol.ElementID;
			localElementKey = localDMAID + "/" + localEleID;

			if (startupCheckPID == -1)
			{
				// StartupCheck == TRUE
				// wait on SLElement to finish starting up.
#if debug
				protocol.Log("QA" + protocol.QActionID + "|DCF STARTUP|Checking Startup: Main Element", LogType.Allways, LogLevel.NoLogging);
#endif
				if (!IsElementStarted(protocol, localDMAID, localEleID, 360))
				{
					protocol.Log(string.Format("QA{0}: |ERR: DCF Startup|(ElementStartupCheck) Value {1} at DCFHelper with ERROR:{2}", protocol.QActionID, "Main Element", "Element Start Check returned False"), LogType.Error, LogLevel.NoLogging);
					throw new Exception("DCFHelper Failed to Initialize: Main Element Not Started");
				}

				if (externalElements != null)
				{
					// Wait on all external Elements to startup
					foreach (var externalElement in externalElements)
					{
#if debug
						protocol.Log("QA" + protocol.QActionID + "|DCF STARTUP|Checking Startup: External Element " + externalElement.elementKey, LogType.Allways, LogLevel.NoLogging);
#endif
						if (!IsElementStarted(protocol, externalElement.dmaID, externalElement.eleID, externalElement.timeoutTime))
						{
							protocol.Log(string.Format("QA{0}: |ERR: DCF Startup|(ElementStartupCheck) Value {1} at DCFHelper for External Element:{2} with ERROR:{3}", protocol.QActionID, "External Element", externalElement.elementKey, "Element Start Check returned False"), LogType.Error, LogLevel.NoLogging);
							unloadedElements.Add(externalElement.elementKey);
						}
					}
				}

				if (dves != null)
				{
					// Wait on all dves to startup
					foreach (var dveColumn in dves)
					{
						object[] columns = (Object[])protocol.NotifyProtocol(321 /*NT_GT_TABLE_COLUMNS*/, dveColumn.tablePID, new UInt32[] { Convert.ToUInt32(dveColumn.columnIDX) });
						object[] elementKeys = (object[])columns[0];
						for (int i = 0; i < elementKeys.Length; i++)
						{
							string eleK = Convert.ToString(elementKeys[i]);
							if (!string.IsNullOrEmpty(eleK))
							{
								ExternalElement ele = new ExternalElement(eleK);
#if debug
								protocol.Log("QA" + protocol.QActionID + "|DCF STARTUP|Checking Startup: DVE Element " + eleK, LogType.Allways, LogLevel.NoLogging);
#endif
								if (!IsElementStarted(protocol, ele.dmaID, ele.eleID, dveColumn.timeoutTime))
								{
									protocol.Log(string.Format("QA{0}: |ERR: DCF Startup|(ElementStartupCheck) Value {1} at DCFHelper for DVE:{2} with ERROR:{3}", protocol.QActionID, "test", eleK, "Element Start Check returned False"), LogType.Error, LogLevel.NoLogging);
									unloadedElements.Add(ele.elementKey);
								}
							}
						}
					}
				}
			}
			else if (startupCheckPID == -2)
			{
				// StartupCheck == FALSE
			}
			else
			{
				string currentStartupMap = Convert.ToString(protocol.GetParameter(startupCheckPID));

				HashSet<string> currentStartupMapA = new HashSet<string>(currentStartupMap.Split(';'));

				if (!currentStartupMapA.Contains(localDMAID + "/" + localEleID))
				{
#if debug
					protocol.Log("QA" + protocol.QActionID + "|DCF STARTUP|Checking Startup: Main Element", LogType.Allways, LogLevel.NoLogging);
#endif
					if (!IsElementStarted(protocol, localDMAID, localEleID, 360))
					{
						protocol.Log(string.Format("QA{0}: |ERR: DCF Startup|(ElementStartupCheck) Value {1} at DCFHelper with ERROR:{2}", protocol.QActionID, "Main Element", "Element Start Check returned False"), LogType.Error, LogLevel.NoLogging);
						throw new Exception("DCFHelper Failed to Initialize: Main Element Not Started");
					}
					else
					{
						currentStartupMapA.Add(localDMAID + "/" + localEleID);
					}
				}

				if (externalElements != null)
				{
					// Wait on all external Elements to startup
					foreach (var externalElement in externalElements)
					{
						if (!currentStartupMapA.Contains(externalElement.elementKey))
						{
#if debug
							protocol.Log("QA" + protocol.QActionID + "|DCF STARTUP|Checking Startup: External Element " + externalElement.elementKey, LogType.Allways, LogLevel.NoLogging);
#endif
							if (!IsElementStarted(protocol, externalElement.dmaID, externalElement.eleID, externalElement.timeoutTime))
							{
								protocol.Log(string.Format("QA{0}: |ERR: DCF Startup|(ElementStartupCheck) Value {1} at DCFHelper for External Element:{2} with ERROR:{3}", protocol.QActionID, "test", externalElement.elementKey, "Element Start Check returned False"), LogType.Error, LogLevel.NoLogging);
								unloadedElements.Add(externalElement.elementKey);
							}
							else
							{
								currentStartupMapA.Add(externalElement.elementKey);
							}
						}
					}
				}

				if (dves != null)
				{
					// Wait on all dves to startup
					foreach (var dveColumn in dves)
					{
						object[] columns = (Object[])protocol.NotifyProtocol(321 /*NT_GT_TABLE_COLUMNS*/, dveColumn.tablePID, new UInt32[] { Convert.ToUInt32(dveColumn.columnIDX) });
						object[] elementKeys = (object[])columns[0];
						for (int i = 0; i < elementKeys.Length; i++)
						{
							string eleK = Convert.ToString(elementKeys[i]);
							if (!String.IsNullOrEmpty(eleK))
							{
								if (!currentStartupMapA.Contains(eleK))
								{
									ExternalElement externalElement = new ExternalElement(eleK);
#if debug
									protocol.Log("QA" + protocol.QActionID + "|DCF STARTUP|Checking Startup: DVE Element " + eleK, LogType.Allways, LogLevel.NoLogging);
#endif
									if (!IsElementStarted(protocol, externalElement.dmaID, externalElement.eleID, dveColumn.timeoutTime))
									{
										protocol.Log(string.Format("QA{0}: |ERR: DCF Startup|(ElementStartupCheck) at DCFHelper for DVE:{1} with ERROR:{2}", protocol.QActionID, eleK, "Element Start Check returned False"), LogType.Error, LogLevel.NoLogging);
										unloadedElements.Add(externalElement.elementKey);
									}
									else
									{
										currentStartupMapA.Add(eleK);
									}
								}
							}
						}
					}
				}

				string newMap = String.Join(";", currentStartupMapA.ToArray());
				protocol.SetParameter(startupCheckPID, newMap);
			}

			if (options.PIDcurrentInterfaceProperties != -1)
			{
				cInterfacePropPID = options.PIDcurrentInterfaceProperties;
				PropertiesBufferToDictionary(options.PIDcurrentInterfaceProperties, currentInterfaceProperties);
			}

			if (options.PIDcurrentConnectionProperties != -1)
			{
				cConnectionPropPID = options.PIDcurrentConnectionProperties;
				PropertiesBufferToDictionary(options.PIDcurrentConnectionProperties, currentConnectionProperties);
			}

			if (options.PIDcurrentConnections != -1)
			{
				cConnectionsPID = options.PIDcurrentConnections;
				PropertiesBufferToDictionary(options.PIDcurrentConnections, currentConnections);
			}

			if (options.PIDnewInterfaceProperties != -1)
			{
				newInterfacePropID = options.PIDnewInterfaceProperties;
				PropertiesBufferToDictionary(options.PIDnewInterfaceProperties, newInterfaceProperties);
			}

			if (options.PIDnewConnectionProperties != -1)
			{
				newConnectionPropPID = options.PIDnewConnectionProperties;
				PropertiesBufferToDictionary(options.PIDnewConnectionProperties, newConnectionProperties);
			}

			if (options.PIDnewConnections != -1)
			{
				newConnectionsPID = options.PIDnewConnections;
				PropertiesBufferToDictionary(options.PIDnewConnections, newConnections);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DCFHelper"/> class. It allows Manipulation of DCF Connections and Properties. Please use this inside of a 'using'-statement.
		/// </summary>
		/// <param name="protocol">The SLProtocol object.</param>
		/// <param name="startupCheckPID">Indicates a Parameter (saved="false") ID that will hold a mapping to indicate if a StartupCheck was already performed for a specific element (main/DVE or External).</param>
		/// <param name="options">DCFMappingOptions: Indicates what PIDs are to be used for mapping. A lighter Object with limited functionality can be created by only providing certain PIDs. Example: Only providing the CurrentConnectionsPID will allow Adding and Removing of connections but not Properties.</param>
		public DCFHelper(SLProtocol protocol, int startupCheckPID, DCFMappingOptions options)
			: this(protocol, startupCheckPID, options, default(DVEColumn[]), default(ExternalElement[]))
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DCFHelper"/> class. It allows Manipulation of DCF Connections and Properties. Please use this inside of a 'using'-statement.
		/// </summary>
		/// <param name="protocol">The SLProtocol Object.</param>
		/// <param name="startupCheckPID">Indicates a Parameter (saved="false") ID that will hold a mapping to indicate if a StartupCheck was already performed for a specific element (main/DVE or External).</param>
		/// <param name="options">DCFMappingOptions: Indicates what PIDs are to be used for mapping. A lighter Object with limited functionality can be created by only providing certain PIDs. Example: Only providing the CurrentConnectionsPID will allow Adding and Removing of connections but not Properties.</param>
		/// <param name="dves">One or more DVEColumn objects identifying all 'element' columns of DVE Tables that also export Interfaces that will be used by the DCFHelper.</param>
		public DCFHelper(SLProtocol protocol, int startupCheckPID, DCFMappingOptions options, params DVEColumn[] dves)
			: this(protocol, startupCheckPID, options, dves, default(ExternalElement[]))
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DCFHelper"/> class. It allows Manipulation of DCF Connections and Properties. Please use this inside of a 'using'-statement.
		/// </summary>
		/// <param name="protocol">The SLProtocol Object.</param>
		/// <param name="startupCheckPID">Indicates a Parameter (saved="false") ID that will hold a mapping to indicate if a StartupCheck was already performed for a specific element (main/DVE or External).</param>
		/// <param name="options">DCFMappingOptions: Indicates what PIDs are to be used for mapping. A lighter Object with limited functionality can be created by only providing certain PIDs. Example: Only providing the CurrentConnectionsPID will allow Adding and Removing of connections but not Properties.</param>
		/// <param name="externalElements">One or more External Elements that will be used by the DCFHelper and need a startup check.</param>
		public DCFHelper(SLProtocol protocol, int startupCheckPID, DCFMappingOptions options, params ExternalElement[] externalElements)
			: this(protocol, startupCheckPID, options, default(DVEColumn[]), externalElements)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DCFHelper"/> class. It allows Manipulation of DCF Connections and Properties. Please use this inside of a 'using'-statement.
		/// </summary>
		/// <param name="protocol">The SLProtocol Object.</param>
		/// <param name="startupCheck">Indicates if Element startup checks need to be forcibly performed.</param>
		/// <param name="options">DCFMappingOptions: Indicates what PIDs are to be used for mapping. A lighter Object with limited functionality can be created by only providing certain PIDs. Example: Only providing the CurrentConnectionsPID will allow Adding and Removing of connections but not Properties.</param>
		public DCFHelper(SLProtocol protocol, bool startupCheck, DCFMappingOptions options)
			: this(protocol, startupCheck ? -1 : -2, options, default(DVEColumn[]), default(ExternalElement[]))
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DCFHelper"/> class. It allows Manipulation of DCF Connections and Properties. Please use this inside of a 'using'-statement.
		/// </summary>
		/// <param name="protocol">The SLProtocol Object.</param>
		/// <param name="startupCheck">Indicates if Element startup checks need to be forcibly performed.</param>
		/// <param name="options">DCFMappingOptions: Indicates what PIDs are to be used for mapping. A lighter Object with limited functionality can be created by only providing certain PIDs. Example: Only providing the CurrentConnectionsPID will allow Adding and Removing of connections but not Properties.</param>
		/// <param name="dves">One or more DVEColumn objects identifying all 'element' columns of DVE Tables that also export Interfaces.</param>
		public DCFHelper(SLProtocol protocol, bool startupCheck, DCFMappingOptions options, params DVEColumn[] dves)
			: this(protocol, startupCheck ? -1 : -2, options, dves, default(ExternalElement[]))
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DCFHelper"/> class. It allows Manipulation of DCF Connections and Properties. Please use this inside of a 'using'-statement.
		/// </summary>
		/// <param name="protocol">The SLProtocol Object.</param>
		/// <param name="startupCheck">Indicates if Element startup checks need to be forcibly performed.</param>
		/// <param name="options">DCFMappingOptions: Indicates what PIDs are to be used for mapping. A lighter Object with limited functionality can be created by only providing certain PIDs. Example: Only providing the CurrentConnectionsPID will allow Adding and Removing of connections but not Properties.</param>
		/// <param name="externalElements">One or more External Elements that will be used by DCFHelper and need a startup check.</param>
		public DCFHelper(SLProtocol protocol, bool startupCheck, DCFMappingOptions options, params ExternalElement[] externalElements)
			: this(protocol, startupCheck ? -1 : -2, options, default(DVEColumn[]), externalElements)
		{
		}

		/// <summary>
		///  Initializes a new instance of the <see cref="DCFHelper"/> class. It allows Manipulation of DCF Connections and Properties. Please use this inside of a 'using'-statement.
		/// </summary>
		/// <param name="protocol">The SLProtocol Object.</param>
		/// <param name="startupCheck">Indicates if Element startup checks need to be forcibly performed.</param>
		/// <param name="options">DCFMappingOptions: Indicates what PIDs are to be used for mapping. A lighter Object with limited functionality can be created by only providing certain PIDs. Example: Only providing the CurrentConnectionsPID will allow Adding and Removing of connections but not Properties.</param>
		/// <param name="dves">An array of DVEColumn objects identifying all 'element' columns of DVE Tables that also export Interfaces that will be used by the DCFHelper.</param>
		/// <param name="externalElements">An array of External Elements that will be used by the DCFHelper and need a Startup check.</param>
		public DCFHelper(SLProtocol protocol, bool startupCheck, DCFMappingOptions options, DVEColumn[] dves, ExternalElement[] externalElements)
			: this(protocol, startupCheck ? -1 : -2, options, dves, externalElements)
		{
		}

		#endregion Constructors

		#region Methods

		public HashSet<string> UnloadedElements
		{
			get { return unloadedElements; }
			private set { unloadedElements = value; }
		}

		public bool DeleteAllManagedDCF()
		{
			bool succes = true;

			try
			{
				try
				{
					foreach (var v in currentConnectionProperties)
					{
						if (unloadedElements.Contains(v.Key))
						{
							protocol.Log(string.Format("QA{0}: |ERR: DCF DeleteAllManagedDCF|Ignoring Connection Property Cleanup: Unloaded Element:{1} ", protocol.QActionID, v.Key), LogType.Error, LogLevel.NoLogging);
							continue;
						}

						int thisDMAID;
						int thisEleID;
						SplitEleKey(v.Key, out thisDMAID, out thisEleID);
						foreach (int key in v.Value)
						{
#if debug
							protocol.Log("QA" + protocol.QActionID + "|DCF Full Delete|Triggered DCF Clear- Deleting Connection Property:" + key, LogType.Allways, LogLevel.NoLogging);
#endif
							int poskey = Math.Abs(key);
							protocol.DeleteConnectivityConnectionProperty(poskey, thisDMAID, thisEleID);
						}
					}

					currentConnectionProperties.Clear();
					newConnectionProperties.Clear();
				}
				catch (Exception e)
				{
					protocol.Log(string.Format("QA{0}:|ERR: DCF Full Delete|(Exception) Value at {1} with Exception:{2}", protocol.QActionID, "ClearManagedDC: CurrentConnectionProperties", e.ToString()), LogType.Error, LogLevel.NoLogging);
				}

				currentConnectionProperties.Clear();

				try
				{
					foreach (var v in currentInterfaceProperties)
					{
						if (unloadedElements.Contains(v.Key))
						{
							protocol.Log(string.Format("QA{0}: |ERR: DCF DeleteAllManagedDCF|Ignoring Interface Property Cleanup: Unloaded Element:{1} ", protocol.QActionID, v.Key), LogType.Error, LogLevel.NoLogging);
							continue;
						}

						int thisDMAID;
						int thisEleID;
						SplitEleKey(v.Key, out thisDMAID, out thisEleID);
						foreach (int key in v.Value)
						{
#if debug
							protocol.Log("QA" + protocol.QActionID + "|DCF Full Delete|Triggered DCF Clear- Deleting Interface Property:" + key, LogType.Allways, LogLevel.NoLogging);
#endif
							int poskey = Math.Abs(key);
							protocol.DeleteConnectivityInterfaceProperty(poskey, thisDMAID, thisEleID);
						}
					}

					currentInterfaceProperties.Clear();
				}
				catch (Exception e)
				{
					protocol.Log(string.Format("QA{0}:|ERR: DCF Full Delete|(Exception) Value at {1} with Exception:{2}", protocol.QActionID, "ClearManagedDC: CurrentInterfaceProperties", e.ToString()), LogType.Error, LogLevel.NoLogging);
				}

				try
				{
					foreach (var v in newConnectionProperties)
					{
						if (unloadedElements.Contains(v.Key))
						{
							protocol.Log(string.Format("QA{0}: |ERR: DCF DeleteAllManagedDCF|Ignoring (n) Connection Property Cleanup: Unloaded Element:{1} ", protocol.QActionID, v.Key), LogType.Error, LogLevel.NoLogging);
							continue;
						}

						int thisDMAID;
						int thisEleID;
						SplitEleKey(v.Key, out thisDMAID, out thisEleID);
						foreach (int key in v.Value)
						{
#if debug
							protocol.Log("QA" + protocol.QActionID + "|DCF Full Delete|Triggered DCF Clear- Deleting New Connection Property:" + key, LogType.Allways, LogLevel.NoLogging);
#endif
							int poskey = Math.Abs(key);
							protocol.DeleteConnectivityConnectionProperty(poskey, thisDMAID, thisEleID);
						}
					}

					newConnectionProperties.Clear();
				}
				catch (Exception e)
				{
					protocol.Log(string.Format("QA{0}:|ERR: DCF Full Delete|(Exception) Value at {1} with Exception:{2}", protocol.QActionID, "ClearManagedDC: NewConnectionProperties", e.ToString()), LogType.Error, LogLevel.NoLogging);
				}

				try
				{
					foreach (var v in newInterfaceProperties)
					{
						if (unloadedElements.Contains(v.Key))
						{
							protocol.Log(string.Format("QA{0}: |ERR: DCF DeleteAllManagedDCF|Ignoring (n) Interface Property Cleanup: Unloaded Element:{1} ", protocol.QActionID, v.Key), LogType.Error, LogLevel.NoLogging);
							continue;
						}

						int thisDMAID;
						int thisEleID;
						SplitEleKey(v.Key, out thisDMAID, out thisEleID);
						foreach (int key in v.Value)
						{
#if debug
							protocol.Log("QA" + protocol.QActionID + "|DCF Full Delete|Triggered DCF Clear- Deleting New Interface Property:" + key, LogType.Allways, LogLevel.NoLogging);
#endif
							int poskey = Math.Abs(key);
							protocol.DeleteConnectivityInterfaceProperty(poskey, thisDMAID, thisEleID);
						}
					}

					newInterfaceProperties.Clear();
				}
				catch (Exception e)
				{
					protocol.Log(string.Format("QA{0}:ERR: |DCF Full Delete|(Exception) Value at {1} with Exception:{2}", protocol.QActionID, "ClearManagedDC: NewInterfaceProperties", e.ToString()), LogType.Error, LogLevel.NoLogging);
				}

				try
				{
					foreach (var v in newConnections)
					{
						if (unloadedElements.Contains(v.Key))
						{
							protocol.Log(string.Format("QA{0}: |ERR: DCF DeleteAllManagedDCF|Ignoring (n) Connection Cleanup: Unloaded Element:{1} ", protocol.QActionID, v.Key), LogType.Error, LogLevel.NoLogging);
							continue;
						}

						int thisDMAID;
						int thisEleID;
						SplitEleKey(v.Key, out thisDMAID, out thisEleID);
						foreach (int key in v.Value)
						{
#if debug
							protocol.Log("QA" + protocol.QActionID + "|DCF Full Delete|Triggered DCF Clear- Deleting New Connection:" + key, LogType.Allways, LogLevel.NoLogging);
#endif
							int poskey = Math.Abs(key);
							protocol.DeleteConnectivityConnection(poskey, thisDMAID, thisEleID, true);
						}
					}

					newConnections.Clear();
				}
				catch (Exception e)
				{
					protocol.Log(string.Format("QA{0}:|ERR: DCF Full Delete|(Exception) Value at {1} with Exception:{2}", protocol.QActionID, "ClearManagedDC: NewConnection", e.ToString()), LogType.Error, LogLevel.NoLogging);
				}

				try
				{
					foreach (var v in currentConnections)
					{
						if (unloadedElements.Contains(v.Key))
						{
							protocol.Log(string.Format("QA{0}: |ERR: DCF DeleteAllManagedDCF|Ignoring Connection Cleanup: Unloaded Element:{1} ", protocol.QActionID, v.Key), LogType.Error, LogLevel.NoLogging);
							continue;
						}

						int thisDMAID;
						int thisEleID;
						SplitEleKey(v.Key, out thisDMAID, out thisEleID);
						foreach (int key in v.Value)
						{
#if debug
							protocol.Log("QA" + protocol.QActionID + "|DCF Full Delete|Triggered DCF Clear- Deleting Connection:" + key, LogType.Allways, LogLevel.NoLogging);
#endif
							int poskey = Math.Abs(key);
							protocol.DeleteConnectivityConnection(poskey, thisDMAID, thisEleID, true);
						}
					}

					currentConnections.Clear();
				}
				catch (Exception e)
				{
					protocol.Log(string.Format("QA{0}:|ERR: DCF Full Delete|(Exception) Value at {1} with Exception:{2}", protocol.QActionID, "ClearManagedDC: CurrentConnections", e.ToString()), LogType.Error, LogLevel.NoLogging);
				}
			}
			catch (Exception e)
			{
				protocol.Log(string.Format("QA{0}:|ERR: DCF Full Delete|(Exception) Value at {1} with Exception:{2}", protocol.QActionID, "ClearManagedDCF", e.ToString()), LogType.Error, LogLevel.NoLogging);
				succes = false;
			}

			return succes;
		}

		public void Dispose()
		{
			interfacesSV = null;
			connectionProperties = null;
			interfaceProperties = null;
			connections = null;
			map_AllConnections = null;
			SyncMapping();
		}

		/// <summary>
		/// Get the state of an element (based on SLDMS, so basically this comes pretty much to the same as a IsElementLoadedInSLDMS).
		/// </summary>
		/// <param name="iDmaId">ID of the DMA on which the element from which the state needs to be retrieved is located.</param>
		/// <param name="iElementId">ID of the element from which the state needs to be retrieved.</param>
		/// <returns>The element state. In case of failure, null is returned.</returns>
		public string GetElementState(UInt32 iDmaId, UInt32 iElementId)
		{
			try
			{
				DMSClass dms = new DMSClass();
				object oState = null;
				dms.Notify(91/*DMS_GET_ELEMENT_STATE*/, 0, iDmaId, iElementId, out oState);
				string sElementState = oState as string;

				return sElementState;
			}
			catch (Exception ex)
			{
				protocol.Log("QA" + protocol.QActionID + "|GetElementState|Exception thrown : " + Environment.NewLine + ex.ToString(), LogType.Error, LogLevel.NoLogging);
				return null;
			}
		}

		/// <summary>
		/// Gets DCF Interfaces using DCFDynamicLink objects to identify a unique interface. This method caches the previously retrieved Interfaces in the background and will not retrieve the interfaces again unless the refresh bool is set to true.
		/// </summary>
		/// <param name="refresh">Set To True if you want to force this method to perform a protocol.GetAllInterfaces and refresh it's internal Cache.</param>
		/// <param name="linksToInterfaces">One or more DCFDynamicLink objects that identify a unique interface (can be both internal, external or a mix of both).</param>
		/// <returns>An array with DCFDynamicLinkResult Objects in the same order as the requested UIDS, if an Interface (or interfaces) was not found then this interface will be null! Be sure to check for 'null' values before using a result!.</returns>
		public DCFDynamicLinkResult[] GetInterfaces(bool refresh, params DCFDynamicLink[] linksToInterfaces)
		{
			DCFDynamicLinkResult[] result = new DCFDynamicLinkResult[linksToInterfaces.Length];
			HashSet<string> refreshed = new HashSet<string>();
			HashSet<string> refreshedProperties = new HashSet<string>();
			for (int i = 0; i < linksToInterfaces.Length; i++)
			{
				var linkToInterface = linksToInterfaces[i];
				result[i] = new DCFDynamicLinkResult(linkToInterface, null);
				if (unloadedElements.Contains(linkToInterface.ElementKey))
				{
					protocol.Log(string.Format("QA{0}: |ERR: DCF Connection|Ignoring GetInterfaceRequest: Unloaded Element:{1} ", protocol.QActionID, linkToInterface.ElementKey), LogType.Error, LogLevel.NoLogging);
					continue;
				}

				if (linkToInterface == null) continue;
				try
				{
					FastCollection<ConnectivityInterface> allInterfaces;
					if ((!interfaces.TryGetValue(linkToInterface.ElementKey, out allInterfaces) || refresh) && !refreshed.Contains(linkToInterface.ElementKey))
					{
						Dictionary<int, ConnectivityInterface> allInterfacesTmp;
						if (linkToInterface.ElementKey == "local")
						{
							allInterfacesTmp = protocol.GetConnectivityInterfaces(localDMAID, localEleID);
						}
						else
						{
							string[] elementKeyA = linkToInterface.ElementKey.Split('/');
							allInterfacesTmp = protocol.GetConnectivityInterfaces(Convert.ToInt32(elementKeyA[0]), Convert.ToInt32(elementKeyA[1]));
						}

						if (allInterfacesTmp == null) continue;
						allInterfaces = new FastCollection<ConnectivityInterface>(allInterfacesTmp.Values.ToArray());
						interfaces[linkToInterface.ElementKey] = allInterfaces;
						refreshed.Add(linkToInterface.ElementKey);
					}

					if (linkToInterface.GetAll)
					{
						DCFDynamicLinkResult newResult = new DCFDynamicLinkResult(linkToInterface, allInterfaces.ToArray());
						result[i] = newResult;
					}
					else
					{
						string uniqueKey;
						Expression<Func<ConnectivityInterface, object>> indexer;
						if (String.IsNullOrEmpty(linkToInterface.InterfaceName))
						{
							if (linkToInterface.TableKey == null)
							{
								indexer = p => Convert.ToString(p.InterfaceId);
								uniqueKey = Convert.ToString(linkToInterface.ParameterGroupID);
							}
							else if (linkToInterface.TableKey == "*")
							{
								indexer = p => Convert.ToString(p.DynamicLink);
								uniqueKey = Convert.ToString(linkToInterface.ParameterGroupID);
							}
							else
							{
								indexer = p => p.DynamicLink + "/" + p.DynamicPK;
								uniqueKey = linkToInterface.ParameterGroupID + "/" + linkToInterface.TableKey;
							}
						}
						else
						{
							if (linkToInterface.Custom)
							{
								indexer = p => p.InterfaceCustomName;
								uniqueKey = linkToInterface.InterfaceName;
							}
							else
							{
								indexer = p => p.InterfaceName;
								uniqueKey = linkToInterface.InterfaceName;
							}
						}

						allInterfaces.AddIndex(indexer);
						var allFound = allInterfaces.FindValue(indexer, uniqueKey);

						DCFDynamicLinkResult newResult = new DCFDynamicLinkResult(linkToInterface, allFound.ToArray());
						result[i] = newResult;
					}

					if (linkToInterface.PropertyFilter != null)
					{
						try
						{
							// Get All the properties
							FastCollection<ConnectivityInterfaceProperty> allProperties;
							if ((!interfacePropertiesPerElement.TryGetValue(linkToInterface.ElementKey, out allProperties) || refresh) && !refreshedProperties.Contains(linkToInterface.ElementKey))
							{
								Dictionary<int, ConnectivityInterfaceProperty> allPropsTmp = new Dictionary<int, ConnectivityInterfaceProperty>();
								foreach (var intf in allInterfaces)
								{
									intf.InterfaceProperties.ToList().ForEach(x => allPropsTmp.Add(x.Key, x.Value));
								}

								allProperties = new FastCollection<ConnectivityInterfaceProperty>(allPropsTmp.Values.ToArray());

								interfacePropertiesPerElement[linkToInterface.ElementKey] = allProperties;
								refreshedProperties.Add(linkToInterface.ElementKey);
							}

							string uniquePropertyKey = String.Empty;
							Expression<Func<ConnectivityInterfaceProperty, object>> propertyIndexer = null;
							Func<ConnectivityInterfaceProperty, object> indexerSearch = null;

							PropertyFilter propFilter = linkToInterface.PropertyFilter;
							if (propFilter.ID != -1)
							{
								indexerSearch = p => p.InterfacePropertyId;
								uniquePropertyKey = Convert.ToString(propFilter.ID);
							}
							else
							{
								if (!String.IsNullOrEmpty(propFilter.Name))
								{
									indexerSearch = p => p.InterfacePropertyName;
									uniquePropertyKey = propFilter.Name;
								}

								if (!String.IsNullOrEmpty(propFilter.Type))
								{
									if (indexerSearch == null)
									{
										indexerSearch = p => p.InterfacePropertyType;
										uniquePropertyKey = propFilter.Type;
									}
									else
									{
										var temp = indexerSearch;
										indexerSearch = p => temp(p) + "/" + p.InterfacePropertyType;
										uniquePropertyKey = uniquePropertyKey + "/" + propFilter.Type;
									}
								}

								if (!String.IsNullOrEmpty(propFilter.Value))
								{
									if (indexerSearch == null)
									{
										indexerSearch = p => p.InterfacePropertyValue;
										uniquePropertyKey = propFilter.Value;
									}

									var temp = indexerSearch;
									indexerSearch = p => temp(p) + "/" + p.InterfacePropertyValue;
									uniquePropertyKey = uniquePropertyKey + "/" + propFilter.Value;
								}
							}

							if (indexerSearch != null)
							{
								propertyIndexer = p => indexerSearch(p);

								allProperties.AddIndex(propertyIndexer);
								var foundProperties = allProperties.FindValue(propertyIndexer, uniquePropertyKey);

								// make a list
								HashSet<string> foundInterfaces = new HashSet<string>(foundProperties.Select(p => p.Interface.ElementKey + "/" + p.Interface.InterfaceId));

								// Filter Current Results with the found Property Interfaces
								result[i] = new DCFDynamicLinkResult(result[i].link, result[i].AllInterfaces.Where(p => foundInterfaces.Contains(p.ElementKey + "/" + p.InterfaceId)).ToArray());
							}
						}
						catch (Exception e)
						{
							protocol.Log(string.Format("QA{0}:|ERR: DCF Interface|(Exception) Value {1} at GetInterfaces - By Property with Exception:{2}", protocol.QActionID, linkToInterface, e.ToString()), LogType.Error, LogLevel.NoLogging);
						}
					}
				}
				catch (Exception e)
				{
					protocol.Log(string.Format("QA{0}:|ERR: DCF Interface|(Exception) Value {1} at GetInterfaces with Exception:{2}", protocol.QActionID, linkToInterface, e.ToString()), LogType.Error, LogLevel.NoLogging);
				}
			}

			return result;
		}

		/// <summary>
		/// Gets DCF Interfaces using DCFDynamicLink structs to identify a unique interface. This method caches the previously retrieved Interfaces in the background and will not retrieve the interfaces again unless the refresh bool is set to true.
		/// </summary>
		/// <param name="linksToInterfaces">One or more DCFDynamicLink structs that identify a unique interface (can be both internal, external or a mix of both).</param>
		/// <returns>An array with DCFDynamicLinkResult Objects in the same order as the requested UIDS, if an Interface (or interfaces) was not found then this interface will be null!.</returns>
		public DCFDynamicLinkResult[] GetInterfaces(params DCFDynamicLink[] linksToInterfaces)
		{
			return GetInterfaces(false, linksToInterfaces);
		}

		/// <summary>
		/// Gets a Single Internal Interface Object based on a table in the driver and a unique string value defined in the IPColumnIdx.
		/// </summary>
		/// <param name="tableID">Table containing Interfaces (this should correspond to a Parameter Group Dynamic ID).</param>
		/// <param name="descrColumnIdx">IDX of the column Containing the Description of the row. If Naming is used, this IDX should contain the complete description created by Naming.</param>
		/// <param name="searchValueColumnIdx">IDX of the column containing a string value to be used as Key to lookup ConnectivityInterface Objects. This should be unique.</param>
		/// <param name="uniqueLink">String Value containing the Unique Key located in IPColumnIDX.</param>
		/// <param name="parameterGroupNames">All possible ParameterGroupNames that can occur for this Table.</param>
		/// <returns>ConnectivityInterface Object.</returns>
		public ConnectivityInterface GetInternalInterface(int tableID, UInt32 descrColumnIdx, UInt32 searchValueColumnIdx, string uniqueLink, params string[] parameterGroupNames)
		{
			try
			{
				var result = GetInternalInterfaces(tableID, descrColumnIdx, searchValueColumnIdx, parameterGroupNames, uniqueLink);
				ConnectivityInterface conIn;
				if (result.TryGetValue(uniqueLink, out conIn))
				{
					return conIn;
				}
			}
			catch (Exception e)
			{
				protocol.Log(string.Format("QA{0}:|ERR: DCF Interface|(Exception) Value {1} at GetInternalInterface with Exception:{2}", protocol.QActionID, uniqueLink, e.ToString()), LogType.Error, LogLevel.NoLogging);
			}

			return null;
		}

		/// <summary>
		/// Retrieves a filtered dictionary containing requested Interfaces using a unique value from a SearchValueColumn.
		/// </summary>
		/// <param name="tableID">Table containing Interfaces (this should correspond to a Parameter Group Dynamic ID).</param>
		/// <param name="descrColumnsIdx">IDX of the column Containing the Description of the row. If Naming is used, this IDX should contain the complete description created by Naming.</param>
		/// <param name="searchValueColumnIdx">IDX of the column containing a string value to be used as Key to lookup ConnectivityInterface Objects. This should be unique.</param>
		/// <param name="parameterGroupNames">All possible ParameterGroupNames that can occur for this Table.</param>
		/// <param name="searchValues">All Unique Values you want to retrieve Interfaces for.</param>
		/// <returns>Dictionary with the Unique Value as key and Interface object as Value.</returns>
		public Dictionary<string, ConnectivityInterface> GetInternalInterfaces(int tableID, UInt32 descrColumnsIdx, UInt32 searchValueColumnIdx, string[] parameterGroupNames, params string[] searchValues)
		{
			string uniqueCacheKey = tableID + String.Join(";", parameterGroupNames.OrderByDescending(p => p));
			Dictionary<string, ConnectivityInterface> result = new Dictionary<string, ConnectivityInterface>();
			try
			{
				if (!cachedTables.Contains(uniqueCacheKey))
				{
					UpdateInterfaces(tableID, descrColumnsIdx, searchValueColumnIdx, parameterGroupNames);
					cachedTables.Add(uniqueCacheKey);
				}

				foreach (string uniqueLink in searchValues)
				{
					ConnectivityInterface conIn;
					if (interfacesSV.TryGetValue(uniqueLink, out conIn))
					{
						result.Add(uniqueLink, conIn);
					}
				}
			}
			catch (Exception e)
			{
				protocol.Log(string.Format("QA{0}:|ERR: DCF Interface|(Exception) Value {1} at GetInternalInterfaces with Exception:{2}", protocol.QActionID, tableID, e.ToString()), LogType.Error, LogLevel.NoLogging);
			}

			return result;
		}

		/// <summary>
		/// Warning: [Obsolete: Please use GetInterfaces()] Gets one or more Internal Interfaces based on the Keys of the Tables used in the driver to create Interfaces with. (See dynamicId= in ParameterGroup).
		/// </summary>
		/// /// <param name="linksToInterfaces">DCFDynamicLink objects that link to a specific interface.</param>
		/// <returns>Returns an Array of ConnectivityInterfaces in the same Order as the Requested Keys. If no Interface was found, the value at position X will be Null.</returns>
		public ConnectivityInterface[] GetInternalInterfaces(params DCFDynamicLink[] linksToInterfaces)
		{
			var itfs = GetInterfaces(true, linksToInterfaces);
			ConnectivityInterface[] result = new ConnectivityInterface[itfs.Length];
			for (int i = 0; i < itfs.Length; i++)
			{
				result[i] = itfs[i].FirstInterface;
			}

			return result;
		}

		/// <summary>
		/// Warning: [Obsolete: Please Use GetInterfaces]Gets All Internal Interfaces based on the parameterGroup IDs (See dynamicId= in ParameterGroup).
		/// </summary>
		/// <param name="paramGroupID">ParameterGroupID used to Link the table with DCF Interfaces.</param>
		/// <returns>Returns an Array of ConnectivityInterfaces.</returns>
		public ConnectivityInterface[] GetInternalInterfaces(params int[] paramGroupID)
		{
			DCFDynamicLink[] dynamicIDs = new DCFDynamicLink[paramGroupID.Length];
			for (int i = 0; i < dynamicIDs.Length; i++)
			{
				dynamicIDs[i] = new DCFDynamicLink(paramGroupID[i]);
			}

			var itfs = GetInterfaces(true, dynamicIDs);
			List<ConnectivityInterface> result = new List<ConnectivityInterface>();
			for (int j = 0; j < itfs.Length; j++)
			{
				if (itfs[j] != null)
				{
					try
					{
						if (itfs[j].AllInterfaces != null)
						{
							for (int u = 0; u < itfs[j].AllInterfaces.Length; u++)
							{
								ConnectivityInterface conInt = itfs[j].AllInterfaces[u];
								if (conInt != null)
									result.Add(conInt);
							}
						}
					}
					catch (Exception e)
					{
						protocol.Log(string.Format("QA{0}:|ERR: DCF Interface|(Exception) Value {1} at GetInternalInterface (with tableKey) with Exception:{2}", protocol.QActionID, paramGroupID[j], e.ToString()), LogType.Error, LogLevel.NoLogging);
					}
				}
			}

			return result.ToArray();
		}

		/// <summary>
		/// Removes all Connection Properties with the given IDs for a specific Connection.
		/// </summary>
		/// <param name="connection">The ConnectivityConnection Object holding the properties.</param>
		/// <param name="force">Indicates if it should force delete all given IDs without checking if they are Managed by this element.</param>
		/// <param name="propertyIDs">One or more Property IDs for the Properties to Delete.</param>
		/// <returns>A boolean indicating if all deletes were successful.</returns>
		public bool RemoveConnectionProperties(ConnectivityConnection connection, bool force, params int[] propertyIDs)
		{
			try
			{
				bool nullInputDetected = false;
				if (connection == null)
				{
					protocol.Log(string.Format("QA{0}:|ERR: DCF Connection Property| Remove Connection Properties ConnectivityConnection connection was Null", protocol.QActionID), LogType.Error, LogLevel.NoLogging);
					nullInputDetected = true;
				}

				if (nullInputDetected) return false;

				if (cConnectionPropPID == -1)
				{
					protocol.Log("QA" + protocol.QActionID + "|ERR: DCF Connection Property|DCFHelper Error: Using RemoveConnectionProperties requires the CurrentConnectionPropertiesPID to be defined! Please change the Options Objects to include this PID", LogType.Error, LogLevel.NoLogging);
					return false;
				}

				bool success = true;

				string eleKey = CreateElementKey(connection.SourceDataMinerId, connection.SourceElementId);
				HashSet<int> managedNewByThisProtocol;
				if (!newConnectionProperties.TryGetValue(eleKey, out managedNewByThisProtocol)) managedNewByThisProtocol = new HashSet<int>();

				HashSet<int> managedCurrentByThisProtocol;
				if (!currentConnectionProperties.TryGetValue(eleKey, out managedCurrentByThisProtocol)) managedCurrentByThisProtocol = new HashSet<int>();

				foreach (int propertyID in propertyIDs)
				{
					if (force || managedNewByThisProtocol.Contains(propertyID) || managedNewByThisProtocol.Contains(-1 * propertyID) || managedCurrentByThisProtocol.Contains(propertyID) || managedCurrentByThisProtocol.Contains(-1 * propertyID))
					{
#if debug
						protocol.Log("QA" + protocol.QActionID + "|DCF Connection Property (" + propertyID + ")|Deleting Connection Property:" + propertyID, LogType.Allways, LogLevel.NoLogging);
#endif
						if (!connection.DeleteProperty(propertyID))
						{
							success = false;
							protocol.Log(string.Format("QA{0}:|ERR: DCF Connection Property (" + propertyID + ")| Removing Connection Property:{1} Returned False! Property may not have been Removed!", protocol.QActionID, propertyID), LogType.Error, LogLevel.NoLogging);
						}
						else
						{
							managedCurrentByThisProtocol.Remove(propertyID);
							managedNewByThisProtocol.Remove(propertyID);
							managedCurrentByThisProtocol.Remove(-1 * propertyID);
							managedNewByThisProtocol.Remove(-1 * propertyID);
						}
					}
				}

				newConnectionProperties[eleKey] = managedNewByThisProtocol;
				currentConnectionProperties[eleKey] = managedCurrentByThisProtocol;
				return success;
			}
			catch (Exception e)
			{
				protocol.Log(string.Format("QA{0}:|ERR: DCF Connection Property|(Exception) Value at {1} with Exception:{2}", protocol.QActionID, "RemoveConnectionProperties", e.ToString()), LogType.Error, LogLevel.NoLogging);
			}

			return false;
		}

		/// <summary>
		/// Remove all ConnectionProperties for a set of IDs.
		/// </summary>
		/// <param name="dataMinerID">DataMiner ID containing the Properties.</param>
		/// <param name="elementID">Element ID Containing the Properties.</param>
		/// <param name="force">Indicate if you want to force delete the Property, without using the Mapping Parameters.</param>
		/// <param name="propertyIDs">One or more Property IDs to remove.</param>
		/// <returns>Boolean indicating the success of the removal.</returns>
		public bool RemoveConnectionProperties(int dataMinerID, int elementID, bool force, params int[] propertyIDs)
		{
			try
			{
				if (cConnectionPropPID == -1)
				{
					protocol.Log("QA" + protocol.QActionID + "|ERR: DCF Connection Property|DCFHelper Error: Using RemoveConnectionProperties requires the CurrentConnectionPropertiesPID to be defined! Please change the Options Objects to include this PID", LogType.Error, LogLevel.NoLogging);
					return false;
				}

				bool success = true;

				string eleKey = CreateElementKey(dataMinerID, elementID);
				HashSet<int> managedNewByThisProtocol;
				if (!newConnectionProperties.TryGetValue(eleKey, out managedNewByThisProtocol)) managedNewByThisProtocol = new HashSet<int>();

				HashSet<int> managedCurrentByThisProtocol;
				if (!currentConnectionProperties.TryGetValue(eleKey, out managedCurrentByThisProtocol)) managedCurrentByThisProtocol = new HashSet<int>();

				foreach (int propertyID in propertyIDs)
				{
					if (force || managedNewByThisProtocol.Contains(propertyID) || managedNewByThisProtocol.Contains(-1 * propertyID) || managedCurrentByThisProtocol.Contains(propertyID) || managedCurrentByThisProtocol.Contains(-1 * propertyID))
					{
#if debug
						protocol.Log("QA" + protocol.QActionID + "|DCF Connection Property (" + propertyID + ")|Deleting Connection Property:" + propertyID, LogType.Allways, LogLevel.NoLogging);
#endif
						if (!protocol.DeleteConnectivityConnectionProperty(propertyID, dataMinerID, elementID))
						{
							success = false;
							protocol.Log(string.Format("QA{0}:|ERR: DCF Connection Property (" + propertyID + ")| Removing Connection Property:{1} Returned False! Property may not have been Removed!", protocol.QActionID, propertyID), LogType.Error, LogLevel.NoLogging);
						}
						else
						{
							managedCurrentByThisProtocol.Remove(propertyID);
							managedNewByThisProtocol.Remove(propertyID);
						}
					}
				}

				newConnectionProperties[eleKey] = managedNewByThisProtocol;
				currentConnectionProperties[eleKey] = managedCurrentByThisProtocol;
				return success;
			}
			catch (Exception e)
			{
				protocol.Log(string.Format("QA{0}:|ERR: DCF Connection Property|(Exception) Value at {1} with Exception:{2}", protocol.QActionID, "RemoveConnectionProperties", e.ToString()), LogType.Error, LogLevel.NoLogging);
			}

			return false;
		}

		/// <summary>
		/// Removes all Connections with the given Name from the Interface linked to a unique String Value. This string value should be available in the IPColumnIdx used in the UpdateInterfaces methods.
		/// </summary>
		/// <param name="inputSearchValue">String value from the searchValueColumnIdx used in the UpdateInterfaces methods to retrieve the Interface.</param>
		/// <param name="bothConnections">A boolean when true, attempts to remove both connections if this was an external connection.</param>
		/// <param name="force">A boolean when true, forces the removal. If false, it won't remove anything not created by this code.</param>
		/// <param name="connectionNames">Connection Names for Connections that need to be deleted.</param>
		/// <returns>Boolean indicating the success of the removal.</returns>
		public bool RemoveConnections(string inputSearchValue, bool bothConnections, bool force, params string[] connectionNames)
		{
			ConnectivityInterface inp = null;
			interfacesSV.TryGetValue(inputSearchValue, out inp);
			if (inp != null)
			{
				return RemoveConnections(inp, bothConnections, force, connectionNames);
			}
			else
			{
				return false;
			}
		}

		/// <summary>
		/// Removes all Connections with the given Name.
		/// </summary>
		/// <param name="input">ConnectivityInterface object containing the connection.</param>
		/// <param name="bothConnections">A boolean when true, attempts to remove both connections if this was an external connection.</param>
		/// <param name="force">A boolean when true, forces the removal. If false, it won't remove anything not created by this code.</param>
		/// <param name="connectionNames">Connection Names for Connections that need to be deleted.</param>
		/// <returns>Boolean indicating the success of the removal.</returns>
		public bool RemoveConnections(ConnectivityInterface input, bool bothConnections, bool force, params string[] connectionNames)
		{
			try
			{
				bool nullInputDetected = false;
				if (input == null)
				{
					protocol.Log(string.Format("QA{0}:|ERR: DCF Connection| Removing(A) DCF Connections ConnectivityInterface input was Null", protocol.QActionID), LogType.Error, LogLevel.NoLogging);
					nullInputDetected = true;
				}

				if (nullInputDetected) return false;

				List<int> connectionsToDelete = new List<int>();
				for (int u = 0; u < connectionNames.Length; u++)
				{
					ConnectivityConnection con = input.GetConnectionByName(connectionNames[u]);
					if (con == null) continue;
					int id = con.ConnectionId;
					connectionsToDelete.Add(id);
				}

				return RemoveConnections(input, bothConnections, force, connectionsToDelete.ToArray());
			}
			catch (Exception e)
			{
				protocol.Log(string.Format("QA{0}: |ERR: DCF Connection|(Exception) at RemoveConnections with Exception:{1}", protocol.QActionID, e.ToString()), LogType.Error, LogLevel.NoLogging);
			}

			return false;
		}

		/// <summary>
		/// Removes all Connections with the given Name.
		/// </summary>
		/// <param name="input">ConnectivityInterface object containing the connection.</param>
		/// <param name="bothConnections">A boolean when true, attempts to remove both connections if this was an external connection.</param>
		/// <param name="force">A boolean when true, forces the removal. If false, it won't remove anything not created by this code.</param>
		/// <param name="connectionIDs">All Connection IDs for Connections that need to be deleted.</param>
		/// <returns>Boolean indicating the success of the removal.</returns>
		public bool RemoveConnections(ConnectivityInterface input, bool bothConnections, bool force, params int[] connectionIDs)
		{
			try
			{
				bool nullInputDetected = false;
				if (input == null)
				{
					protocol.Log(string.Format("QA{0}: |ERR: DCF Connection|Removing DCF(B) Connections ConnectivityInterface input was Null", protocol.QActionID), LogType.Error, LogLevel.NoLogging);
					nullInputDetected = true;
				}

				if (nullInputDetected) return false;

				if (cConnectionsPID == -1)
				{
					protocol.Log("QA" + protocol.QActionID + "|ERR: DCF Connection|DCFHelper Error: Using RemoveConnections requires the CurrentConnectionsPID to be defined! Please change the Options Objects to include this PID", LogType.Error, LogLevel.NoLogging);
					return false;
				}

				bool finalResult = true;
				string eleKey = CreateElementKey(input.DataMinerId, input.ElementId);
				HashSet<int> managedNewByThisProtocol;
				if (!newConnections.TryGetValue(eleKey, out managedNewByThisProtocol)) managedNewByThisProtocol = new HashSet<int>();

				HashSet<int> managedCurrentByThisProtocol;
				if (!currentConnections.TryGetValue(eleKey, out managedCurrentByThisProtocol)) managedCurrentByThisProtocol = new HashSet<int>();

				for (int u = 0; u < connectionIDs.Length; u++)
				{
					var con = input.GetConnectionById(connectionIDs[u]);
					if (force || managedCurrentByThisProtocol.Contains(connectionIDs[u]) || managedCurrentByThisProtocol.Contains(-1 * connectionIDs[u]) || managedNewByThisProtocol.Contains(connectionIDs[u]) || managedNewByThisProtocol.Contains(-1 * connectionIDs[u]))
					{
#if debug
						protocol.Log("QA" + protocol.QActionID + "|DCF Connection (" + con.ConnectionId + ")|Deleting Connection:" + con.ConnectionName, LogType.Allways, LogLevel.NoLogging);
#endif

						if (input.DeleteConnection(connectionIDs[u], bothConnections))
						{
							managedNewByThisProtocol.Remove(connectionIDs[u]);
							managedCurrentByThisProtocol.Remove(connectionIDs[u]);
							managedNewByThisProtocol.Remove(-1 * connectionIDs[u]);
							managedCurrentByThisProtocol.Remove(-1 * connectionIDs[u]);
						}
						else
						{
							protocol.Log(string.Format("QA{0}:|ERR: DCF Connection (" + connectionIDs[u] + ")| Removing DCF Connection:{1} Returned False. Connection may not have been Removed", protocol.QActionID, connectionIDs[u]), LogType.Error, LogLevel.NoLogging);
							finalResult = false;
						}
					}
				}

				newConnections[eleKey] = managedNewByThisProtocol;
				currentConnections[eleKey] = managedCurrentByThisProtocol;

				return finalResult;
			}
			catch (Exception e)
			{
				protocol.Log(string.Format("QA{0}:|ERR: DCF Connection| (Exception) Value at {1} with Exception:{2}", protocol.QActionID, "RemoveConnections", e.ToString()), LogType.Error, LogLevel.NoLogging);
			}

			return false;
		}

		/// <summary>
		/// Remove all Connections with the provided ID.
		/// </summary>
		/// <param name="dataMinerID">DataMiner ID containing the connections.</param>
		/// <param name="elementID">Element ID Containing the connections.</param>
		/// <param name="bothConnections">For external connections, indicate if the connections on both elements must be deleted.</param>
		/// <param name="force">Indicate if you want to force delete the connection, without using the Mapping Parameters.</param>
		/// <param name="connectionIDs">One or more connection IDs to remove.</param>
		/// <returns>Boolean indicating the success of the removal.</returns>
		public bool RemoveConnections(int dataMinerID, int elementID, bool bothConnections, bool force, params int[] connectionIDs)
		{
			try
			{
				if (cConnectionsPID == -1)
				{
					protocol.Log("QA" + protocol.QActionID + "|ERR: DCF Connection|DCFHelper Error: Using RemoveConnections requires the CurrentConnectionsPID to be defined! Please change the Options Objects to include this PID", LogType.Error, LogLevel.NoLogging);
					return false;
				}

				bool finalResult = true;
				string eleKey = CreateElementKey(dataMinerID, elementID);
				if (unloadedElements.Contains(eleKey))
				{
					protocol.Log(string.Format("QA{0}: |ERR: DCF Connection|Ignoring RemoveConnections: Unloaded Element:{1} ", protocol.QActionID, eleKey), LogType.Error, LogLevel.NoLogging);
					return false;
				}

				HashSet<int> managedNewByThisProtocol;
				if (!newConnections.TryGetValue(eleKey, out managedNewByThisProtocol)) managedNewByThisProtocol = new HashSet<int>();

				HashSet<int> managedCurrentByThisProtocol;
				if (!currentConnections.TryGetValue(eleKey, out managedCurrentByThisProtocol)) managedCurrentByThisProtocol = new HashSet<int>();

				for (int u = 0; u < connectionIDs.Length; u++)
				{
					if (force || managedCurrentByThisProtocol.Contains(connectionIDs[u]) || managedCurrentByThisProtocol.Contains(-1 * connectionIDs[u]) || managedNewByThisProtocol.Contains(connectionIDs[u]) || managedNewByThisProtocol.Contains(-1 * connectionIDs[u]))
					{
#if debug
						protocol.Log("QA" + protocol.QActionID + "|DCF Connection (" + connectionIDs[u] + ")|Deleting Connection:" + connectionIDs[u], LogType.Allways, LogLevel.NoLogging);
#endif
						if (protocol.DeleteConnectivityConnection(connectionIDs[u], dataMinerID, elementID, bothConnections))
						{
							managedNewByThisProtocol.Remove(connectionIDs[u]);
							managedCurrentByThisProtocol.Remove(connectionIDs[u]);
						}
						else
						{
							protocol.Log(string.Format("QA{0}: |ERR: DCF Connection (" + connectionIDs[u] + ")| Removing DCF Connection:{1} Returned False. Connection may not have been Removed", protocol.QActionID, connectionIDs[u]), LogType.Error, LogLevel.NoLogging);
							finalResult = false;
						}
					}
				}

				newConnections[eleKey] = managedNewByThisProtocol;
				currentConnections[eleKey] = managedCurrentByThisProtocol;

				return finalResult;
			}
			catch (Exception e)
			{
				protocol.Log(string.Format("QA{0}:|ERR: DCF Connection| (Exception) Value at {1} with Exception:{2}", protocol.QActionID, "RemoveConnections", e.ToString()), LogType.Error, LogLevel.NoLogging);
			}

			return false;
		}

		/// <summary>
		/// Removes all Interface Properties with the given IDs for a specific Connection.
		/// </summary>
		/// <param name="itf">The ConnectivityInterface Object holding the properties.</param>
		/// <param name="force">Indicates if it should force delete all given IDs without checking if they are Managed by this element.</param>
		/// <param name="propertyIDs">One or more Property IDs for the Properties to Delete.</param>
		/// <returns>A boolean indicating if all deletes were successful.</returns>
		public bool RemoveInterfaceProperties(ConnectivityInterface itf, bool force, params int[] propertyIDs)
		{
			try
			{
				bool nullInputDetected = false;
				if (itf == null)
				{
					protocol.Log(string.Format("QA{0}:|ERR: DCF Interface Property|Remove Interface Properties ConnectivityInterface itf was Null", protocol.QActionID), LogType.Error, LogLevel.NoLogging);
					nullInputDetected = true;
				}

				if (nullInputDetected) return false;

				if (cInterfacePropPID == -1)
				{
					protocol.Log("QA" + protocol.QActionID + "|ERR: DCF Interface Property|DCFHelper Error: Using RemoveInterfaceProperties requires the CurrentInterfacePropertiesPID to be defined! Please change the Options Objects to include this PID", LogType.Error, LogLevel.NoLogging);
					return false;
				}

				bool success = true;
				string eleKey = CreateElementKey(itf.DataMinerId, itf.ElementId);
				HashSet<int> managedNewByThisProtocol;
				if (!newInterfaceProperties.TryGetValue(eleKey, out managedNewByThisProtocol)) managedNewByThisProtocol = new HashSet<int>();

				HashSet<int> managedCurrentByThisProtocol;
				if (!currentInterfaceProperties.TryGetValue(eleKey, out managedCurrentByThisProtocol)) managedCurrentByThisProtocol = new HashSet<int>();

				foreach (int propertyID in propertyIDs)
				{
					if (force || managedNewByThisProtocol.Contains(propertyID) || managedNewByThisProtocol.Contains(-1 * propertyID) || managedCurrentByThisProtocol.Contains(propertyID) || managedCurrentByThisProtocol.Contains(-1 * propertyID))
					{
#if debug
						protocol.Log("QA" + protocol.QActionID + "|DCF Interface Property (" + propertyID + ")|Deleting Interface Property:" + propertyID, LogType.Allways, LogLevel.NoLogging);
#endif
						if (!itf.DeleteProperty(propertyID))
						{
							success = false;
							protocol.Log(string.Format("QA{0}:|ERR: DCF Interface Property (" + propertyID + ")| Removing Interface Property:{1} Returned False! Property may not have been Removed!", protocol.QActionID, propertyID), LogType.Error, LogLevel.NoLogging);
						}
						else
						{
							managedCurrentByThisProtocol.Remove(propertyID);
							managedNewByThisProtocol.Remove(propertyID);
							managedCurrentByThisProtocol.Remove(-1 * propertyID);
							managedNewByThisProtocol.Remove(-1 * propertyID);
						}
					}
				}

				newInterfaceProperties[eleKey] = managedNewByThisProtocol;
				currentInterfaceProperties[eleKey] = managedCurrentByThisProtocol;
				return success;
			}
			catch (Exception e)
			{
				protocol.Log(string.Format("QA{0}:|ERR: DCF Interface Property|(Exception) Value at {1} with Exception:{2}", protocol.QActionID, "RemoveInterfaceProperties", e.ToString()), LogType.Error, LogLevel.NoLogging);
			}

			return false;
		}

		/// <summary>
		/// Remove all InterfaceProperties for a set of IDs.
		/// </summary>
		/// <param name="dataMinerID">DataMiner ID containing the Properties.</param>
		/// <param name="elementID">Element ID Containing the Properties.</param>
		/// <param name="force">Indicate if you want to force delete the Property, without using the Mapping Parameters.</param>
		/// <param name="propertyIDs">One or more Property IDs to remove.</param>
		/// <returns>Boolean indicating the success of the removal.</returns>
		public bool RemoveInterfaceProperties(int dataMinerID, int elementID, bool force, params int[] propertyIDs)
		{
			bool success = false;
			try
			{
				if (cInterfacePropPID == -1)
				{
					protocol.Log("QA" + protocol.QActionID + "|ERR: DCF Interface Property|DCFHelper Error: Using RemoveInterfaceProperties requires the CurrentInterfacePropertiesPID to be defined! Please change the Options Objects to include this PID", LogType.Error, LogLevel.NoLogging);
					return false;
				}

				success = true;
				string eleKey = CreateElementKey(dataMinerID, elementID);
				HashSet<int> managedNewByThisProtocol;
				if (!newInterfaceProperties.TryGetValue(eleKey, out managedNewByThisProtocol)) managedNewByThisProtocol = new HashSet<int>();

				HashSet<int> managedCurrentByThisProtocol;
				if (!currentInterfaceProperties.TryGetValue(eleKey, out managedCurrentByThisProtocol)) managedCurrentByThisProtocol = new HashSet<int>();

				foreach (int propertyID in propertyIDs)
				{
					if (force || managedNewByThisProtocol.Contains(propertyID) || managedNewByThisProtocol.Contains(-1 * propertyID) || managedCurrentByThisProtocol.Contains(propertyID) || managedCurrentByThisProtocol.Contains(-1 * propertyID))
					{
#if debug
						protocol.Log("QA" + protocol.QActionID + "|DCF Interface Property (" + propertyID + ")|Deleting Interface Property:" + propertyID, LogType.Allways, LogLevel.NoLogging);
#endif
						if (!protocol.DeleteConnectivityConnectionProperty(propertyID, dataMinerID, elementID))
						{
							success = false;
							protocol.Log(string.Format("QA{0}:|ERR: DCF Interface Property (" + propertyID + ")| Removing Interface Property:{1} Returned False! Property may not have been Removed!", protocol.QActionID, propertyID), LogType.Error, LogLevel.NoLogging);
						}
						else
						{
							managedCurrentByThisProtocol.Remove(propertyID);
							managedNewByThisProtocol.Remove(propertyID);
						}
					}
				}

				newInterfaceProperties[eleKey] = managedNewByThisProtocol;
				currentInterfaceProperties[eleKey] = managedCurrentByThisProtocol;
			}
			catch (Exception e)
			{
				protocol.Log(string.Format("QA{0}:|ERR: DCF Interface Property|(Exception) Value at {1} with Exception:{2}", protocol.QActionID, "RemoveInterfaceProperties", e.ToString()), LogType.Error, LogLevel.NoLogging);
			}

			return success;
		}

		/// <summary>
		/// Saves a collection of ConnectivityConnectionProperty objects to a given ConnectivityConnection.
		/// </summary>
		/// <param name="connectivityConnection">The Interface to link the Properties to.</param>
		/// <param name="full">Default: false, Indicates that every Property for this interface is provided in one go (similar to Fill Table At Once). Any Property managed by this Element that is still connected to this Interface will be removed.</param>
		/// <param name="fixedProperty">Default: false, Indicates this is a fixed property that can only be removed by the custom remove functionality.</param>
		/// <param name="addToReturnConnection">Default: false, In case of external connection, if this is set to true try and add the property to the return connection as well.</param>
		/// <param name="async">Default: false, Indicates if the Save should be performed asynchronous or if it should wait for the property to be fully built. Async: True will be much faster and is highly recommended for large scale setups.</param>
		/// <param name="newConnectProps">One or more ConnectivityConnectionProperty Objects holding the new/updated data.</param>
		/// <returns>A boolean indicating the overall success of the save.</returns>
		public bool SaveConnectionProperties(ConnectivityConnection connectivityConnection, bool full, bool fixedProperty, bool addToReturnConnection, bool async, params ConnectivityConnectionProperty[] newConnectProps)
		{
			try
			{
				bool nullInputDetected = false;
				if (connectivityConnection == null)
				{
					protocol.Log(string.Format("QA{0}:|ERR: DCF Connection Property| Save Connection Properties ConnectivityConnection was Null", protocol.QActionID), LogType.Error, LogLevel.NoLogging);
					nullInputDetected = true;
				}

				if (nullInputDetected) return false;

				if (cConnectionPropPID == -1)
				{
					protocol.Log("QA" + protocol.QActionID + "|ERR: DCF Connection Property|DCFHelper Error: Using SaveConnectionProperties requires the CurrentConnectionsPropertiesPID to be defined! Please change the Options Objects to include this PID", LogType.Error, LogLevel.NoLogging);
					return false;
				}

				bool finalResult = true;

				bool externalConnection = connectivityConnection.SourceDataMinerId != connectivityConnection.DestinationDMAId || connectivityConnection.SourceElementId != connectivityConnection.DestinationEId;

				string propertyIdentifier = connectivityConnection.ConnectionId + "-" + connectivityConnection.SourceDataMinerId + "/" + connectivityConnection.SourceElementId;

				Dictionary<string, ConnectivityConnectionProperty> allProps;

				// Retrieve all properties for this connection in a single call, if they haven't already been called earlier.
				if (!connectionProperties.TryGetValue(propertyIdentifier, out allProps))
				{
					var itfProps = connectivityConnection.ConnectionProperties;
					allProps = itfProps.GroupBy(p => p.Value.ConnectionPropertyName).ToDictionary(item => item.Key, item => item.First().Value);
					itfProps = null;
					connectionProperties.Add(propertyIdentifier, allProps);
				}

				int[] result = new int[newConnectProps.Length];

				// parse all properties and add them (this should be updated in the future to a bulk set if it gets added to DCF)
				for (int i = 0; i < newConnectProps.Length; i++)
				{
					bool thisResult = true;
					var newConnectProp = newConnectProps[i];
					if (newConnectProp == null)
					{
						result[i] = -1;
						continue;
					}

					ConnectivityConnectionProperty prop;
					if (allProps.TryGetValue(newConnectProp.ConnectionPropertyName, out prop))
					{
						// UPDATE PROPERTY
						if (prop.ConnectionPropertyName == newConnectProp.ConnectionPropertyName && prop.ConnectionPropertyType == newConnectProp.ConnectionPropertyType && prop.ConnectionPropertyValue == newConnectProp.ConnectionPropertyValue)
						{
#if debug
							protocol.Log("QA" + protocol.QActionID + "|DCF Connection Property (" + prop.ConnectionPropertyId + ")|Not Updating Connection Property:" + prop.ConnectionPropertyId + "/" + newConnectProp.ConnectionPropertyName + ":" + newConnectProp.ConnectionPropertyValue + "-- No Change Detected", LogType.Allways, LogLevel.NoLogging);
#endif
						}
						else
						{
#if debug
							protocol.Log("QA" + protocol.QActionID + "|DCF Connection Property (" + prop.ConnectionPropertyId + ")|Updating Connection Property:" + prop.ConnectionPropertyId + "/" + newConnectProp.ConnectionPropertyName + ":" + newConnectProp.ConnectionPropertyValue, LogType.Allways, LogLevel.NoLogging);
#endif
							prop.ConnectionPropertyType = newConnectProp.ConnectionPropertyType;
							prop.ConnectionPropertyValue = newConnectProp.ConnectionPropertyValue;
							//if (connectivityConnection.UpdateProperty(prop, false))
							if (connectivityConnection.UpdateProperty(prop.ConnectionPropertyId, prop.ConnectionPropertyName, prop.ConnectionPropertyType, prop.ConnectionPropertyValue, connectivityConnection.ConnectionId, false))
							{
								connectionProperties[propertyIdentifier][newConnectProp.ConnectionPropertyName] = prop;
							}
							else
							{
								protocol.Log(string.Format("QA{0}:|ERR: DCF Connection Property (" + prop.ConnectionPropertyId + ")| Updating Connection Property:{1} Returned False. Property may not have been Updated!", protocol.QActionID, prop.ConnectionPropertyId + "/" + newConnectProp.ConnectionPropertyName + ":" + newConnectProp.ConnectionPropertyValue), LogType.Error, LogLevel.NoLogging);
								finalResult = thisResult = false;
							}
						}
					}
					else
					{
						// ADD PROPERTY
#if debug
						protocol.Log("QA" + protocol.QActionID + "|DCF Connection Property|Adding Connection Property:" + newConnectProp.ConnectionPropertyName + ":" + newConnectProp.ConnectionPropertyValue, LogType.Allways, LogLevel.NoLogging);
#endif
						if (!async)
						{
							if (connectivityConnection.AddProperty(newConnectProp.ConnectionPropertyName, newConnectProp.ConnectionPropertyType, newConnectProp.ConnectionPropertyValue, out prop, 420000, false))
							{
								newConnectProp.ConnectionPropertyId = prop.ConnectionPropertyId;
								connectionProperties[propertyIdentifier].Add(prop.ConnectionPropertyName, prop);
#if debug
								protocol.Log("QA" + protocol.QActionID + "|DCF Connection Property (" + prop.ConnectionPropertyId + ")|Property Added Id:" + prop.ConnectionPropertyId, LogType.Allways, LogLevel.NoLogging);
#endif
							}
							else
							{
								protocol.Log(string.Format("QA{0}:|ERR: DCF Connection Property| Adding Connection Property:{1} Timed out after 7 Minutes! Property may not have been Added!", protocol.QActionID, newConnectProp.ConnectionPropertyName + ":" + newConnectProp.ConnectionPropertyValue), LogType.Error, LogLevel.NoLogging);
								finalResult = thisResult = false;
							}
						}
						else
						{
							int outID;
							if (connectivityConnection.AddProperty(newConnectProp.ConnectionPropertyName, newConnectProp.ConnectionPropertyType, newConnectProp.ConnectionPropertyValue, out outID, false))
							{
								newConnectProp.ConnectionPropertyId = outID;
								newConnectProp.Connection = connectivityConnection;
								prop = newConnectProp;

								connectionProperties[propertyIdentifier].Add(prop.ConnectionPropertyName, prop);
#if debug
								protocol.Log("QA" + protocol.QActionID + "|DCF Connection Property (" + prop.ConnectionPropertyId + ")|Property Getting Added (Async) Id:" + prop.ConnectionPropertyId, LogType.Allways, LogLevel.NoLogging);
#endif
							}
							else
							{
								protocol.Log(string.Format("QA{0}:|ERR: DCF Connection Property| Adding Connection Property -Async- :{1} Returned False! Property may not have been Added!", protocol.QActionID, newConnectProp.ConnectionPropertyName + ":" + newConnectProp.ConnectionPropertyValue), LogType.Error, LogLevel.NoLogging);
								finalResult = thisResult = false;
							}
						}
					}

					if (thisResult)
					{
						result[i] = prop.ConnectionPropertyId;

						string eleKey = CreateElementKey(connectivityConnection.SourceDataMinerId, connectivityConnection.SourceElementId);
						if (fixedProperty)
						{
							AddToPropertyDictionary(newConnectionProperties, eleKey, -1 * prop.ConnectionPropertyId);
						}
						else
						{
							AddToPropertyDictionary(newConnectionProperties, eleKey, prop.ConnectionPropertyId);
						}
					}
				}

				// full = true means this save contains all properties for this interface, any old properties not part of the provided data must be deleted if they are managed by this element.
				// Don't do this if one of the properties couldn't get saved because we would be working with invalid data and we don't want to delete anything by mistake
				if (full && finalResult)
				{
					var propertiesToDelete = allProps.Values.Select(p => p.ConnectionPropertyId).Except(result);
					RemoveConnectionProperties(connectivityConnection, false, propertiesToDelete.ToArray());
				}

				// Try and do this, even if one of the properties failed to save. Try and limit the amount of 'bad' properties
				if (externalConnection && addToReturnConnection)
				{
#if debug
					protocol.Log("QA" + protocol.QActionID + "|DCF Connection Property|External Connection Detected, Attempting to Add Properties to Return Connection...", LogType.Allways, LogLevel.NoLogging);
#endif
					// In case of external connections, also manually add the properties to the return connection
					int externalDMAID = connectivityConnection.DestinationDMAId;
					int externalEleID = connectivityConnection.DestinationEId;
					int externalInterface = connectivityConnection.DestinationInterfaceId;
					string elementKey = externalDMAID + "/" + externalEleID;
					if (!map_AllConnections.ContainsKey(elementKey))
					{
						var newPolledConnections = protocol.GetConnectivityConnections(externalDMAID, externalEleID);
						map_AllConnections[elementKey] = new FastCollection<ConnectivityConnection>(newPolledConnections.Values.ToList());
					}

					// Find the external connection based on source to destination being the opposite
					string uniqueKey = connectivityConnection.SourceDataMinerId + "/" + connectivityConnection.SourceElementId + "/" + connectivityConnection.SourceInterfaceId;
					FastCollection<ConnectivityConnection> elementConnections = map_AllConnections[elementKey];
					var returnConnections = elementConnections.FindValue(p => p.DestinationDMAId + "/" + p.DestinationEId + "/" + p.DestinationInterfaceId, uniqueKey).ToArray();
					if (returnConnections != null && returnConnections.Length > 0)
					{
						foreach (var returnConnection in returnConnections)
						{
							SaveConnectionProperties(returnConnection, full, fixedProperty, false, async, newConnectProps);
						}
					}
					else
					{
						protocol.Log("QA" + protocol.QActionID + "|ERR: DCF Connection Property| Could not Locate Return Connection for External Connection with Name:" + connectivityConnection.ConnectionName, LogType.Error, LogLevel.NoLogging);
					}
				}

				return finalResult;
			}
			catch (Exception e)
			{
				protocol.Log(string.Format("QA{0}:|ERR: DCF Connection Property|(Exception) Value at {1} with Exception:{2}", protocol.QActionID, "SaveConnectionProperties", e.ToString()), LogType.Error, LogLevel.NoLogging);
			}

			return false;
		}

		/// <summary>
		/// Saves a collection of ConnectivityConnectionProperty objects to a given ConnectivityConnection.
		/// </summary>
		/// <param name="connectivityConnection">The Interface to link the Properties to.</param>
		/// <param name="full">Default: false, Indicates that every Property for this interface is provided in one go (similar to Fill Table At Once). Any Property managed by this Element that is still connected to this Interface will be removed.</param>
		/// <param name="fixedProperty">Default: false, Indicates this is a fixed property that can only be removed by the custom remove functionality.</param>
		/// <param name="addToReturnConnection">Default: false, In case of external connection, if this is set to true try and add the property to the return connection as well.</param>
		/// <param name="newConnectProps">One or more ConnectivityConnectionProperty Objects holding the new/updated data.</param>
		/// <returns>A boolean indicating the overall success of the save.</returns>
		public bool SaveConnectionProperties(ConnectivityConnection connectivityConnection, bool full, bool fixedProperty, bool addToReturnConnection, params ConnectivityConnectionProperty[] newConnectProps)
		{
			return SaveConnectionProperties(connectivityConnection, full, fixedProperty, addToReturnConnection, false, newConnectProps);
		}

		/// <summary>
		/// Saves a collection of ConnectivityConnectionProperty objects to a given ConnectivityConnection.
		/// </summary>
		/// <param name="connectivityConnection">The Interface to link the Properties to.</param>
		/// <param name="full">Default: false, Indicates that every Property for this interface is provided in one go (similar to Fill Table At Once). Any Property managed by this Element that is still connected to this Interface will be removed.</param>
		/// <param name="fixedProperty">Default: false, Indicates this is a fixed property that can only be removed by the custom remove functionality.</param>
		/// <param name="newConnectProps">One or more ConnectivityConnectionProperty Objects holding the new/updated data.</param>
		/// <returns>A boolean indicating the overall success of the save.</returns>
		public bool SaveConnectionProperties(ConnectivityConnection connectivityConnection, bool full, bool fixedProperty, params ConnectivityConnectionProperty[] newConnectProps)
		{
			return SaveConnectionProperties(connectivityConnection, full, fixedProperty, false, newConnectProps);
		}

		/// <summary>
		/// Saves a collection of ConnectivityConnectionProperty objects to a given ConnectivityConnection.
		/// </summary>
		/// <param name="connectivityConnection">The Interface to link the Properties to.</param>
		/// <param name="full">Default: false, Indicates that every Property for this interface is provided in one go (similar to Fill Table At Once). Any Property managed by this Element that is still connected to this Interface will be removed.</param>
		/// <param name="newConnectProps">One or more ConnectivityConnectionProperty Objects holding the new/updated data.</param>
		/// <returns>A boolean indicating the overall success of the save.</returns>
		public bool SaveConnectionProperties(ConnectivityConnection connectivityConnection, bool full, params ConnectivityConnectionProperty[] newConnectProps)
		{
			return SaveConnectionProperties(connectivityConnection, full, false, newConnectProps);
		}

		/// <summary>
		/// This method is used to save both internal and external connections.
		/// </summary>
		/// <param name="forceRefresh">Indicates if the cache of Connections needs to be refreshed (performs a protocol.GetConnectivityConnections() in the background).</param>
		/// <param name="requests">One or more DCFSaveConnectionRequest objects that define the connection you wish to save.</param>
		/// <returns>One or more DCFSaveConnectionResults in the same order as the DCFSaveConnectionRequests. If a Connection fails to get created then the connections inside the DCFSaveConnectionResult will be null.</returns>
		public DCFSaveConnectionResult[] SaveConnections(bool forceRefresh, params DCFSaveConnectionRequest[] requests)
		{
			DCFSaveConnectionResult[] result = new DCFSaveConnectionResult[requests.Length];
			for (int i = 0; i < requests.Length; i++)
			{
				DCFSaveConnectionRequest currentRequest = requests[i];
				result[i] = new DCFSaveConnectionResult(null, null, false, true);
				bool updated = true;
				if (currentRequest == null) continue;
				if (currentRequest.source == null || currentRequest.destination == null)
				{
					protocol.Log(string.Format("QA{0}: |ERR: DCF Connection|ConnectionRequest Had empty Source or Destination. The Requested Interfaces might not exist.", protocol.QActionID), LogType.Error, LogLevel.NoLogging);
					continue;
				}

				try
				{
					if (currentRequest.customName == null) currentRequest.customName = currentRequest.source.InterfaceName + "->" + currentRequest.destination.InterfaceName;
					string sourceElementKey = currentRequest.source.ElementKey;
					if (unloadedElements.Contains(sourceElementKey))
					{
						protocol.Log(string.Format("QA{0}: |ERR: DCF Connection|Ignoring ConnectionRequest Unloaded Source Element:{1} ", protocol.QActionID, sourceElementKey), LogType.Error, LogLevel.NoLogging);
						continue;
					}

					string destinElementKey = currentRequest.destination.ElementKey;
					if (unloadedElements.Contains(destinElementKey))
					{
						protocol.Log(string.Format("QA{0}: |ERR: DCF Connection|Ignoring ConnectionRequest Unloaded Destination Element:{1} ", protocol.QActionID, destinElementKey), LogType.Error, LogLevel.NoLogging);
						continue;
					}

					bool internalConnection = sourceElementKey == destinElementKey;

					if (!map_AllConnections.ContainsKey(sourceElementKey) || forceRefresh)
					{
						var newPolledConnections = protocol.GetConnectivityConnections(currentRequest.source.DataMinerId, currentRequest.source.ElementId);
						if (newPolledConnections == null)
						{
							protocol.Log(string.Format("QA{0}: |ERR: DCF Connection|GetConnectivityConnections returned a Null for Element:" + currentRequest.source.DataMinerId + "/" + currentRequest.source.ElementId + " Either there was No Response, SLNet was not available, or there was an Exception in the DataMiner DCF API code.", protocol.QActionID), LogType.Error, LogLevel.NoLogging);
							continue;
						}

						map_AllConnections[sourceElementKey] = new FastCollection<ConnectivityConnection>(newPolledConnections.Values.ToList());
					}

					string uniqueKey;
					FastCollection<ConnectivityConnection> elementConnections = map_AllConnections[sourceElementKey];
					Expression<Func<ConnectivityConnection, object>> indexer;
					switch (currentRequest.connectionType)
					{
						case SaveConnectionType.Unique_Name:
							indexer = p => InternalExternalChar(p) + "_" + p.ConnectionName;
							uniqueKey = InternalExternalChar(currentRequest) + "_" + currentRequest.customName;
							break;

						case SaveConnectionType.Unique_Destination:
							indexer = p => InternalExternalChar(p) + "_" + p.DestinationDMAId + "/" + p.DestinationEId + "/" + p.DestinationInterfaceId;
							uniqueKey = InternalExternalChar(currentRequest) + "_" + currentRequest.destination.DataMinerId + "/" + currentRequest.destination.ElementId + "/" + currentRequest.destination.InterfaceId;
							break;

						case SaveConnectionType.Unique_Source:
							indexer = p => InternalExternalChar(p) + "_" + p.SourceInterfaceId;
							uniqueKey = InternalExternalChar(currentRequest) + "_" + Convert.ToString(currentRequest.source.InterfaceId);
							break;

						case SaveConnectionType.Unique_SourceAndDestination:
							indexer = p => InternalExternalChar(p) + "_" + p.SourceInterfaceId + "/" + p.DestinationDMAId + "/" + p.DestinationEId + "/" + p.DestinationInterfaceId;
							uniqueKey = InternalExternalChar(currentRequest) + "_" + currentRequest.source.InterfaceId + "/" + currentRequest.destination.ElementKey + "/" + currentRequest.destination.InterfaceId;
							break;

						default:
							indexer = p => InternalExternalChar(p) + "_" + p.ConnectionName;
							uniqueKey = InternalExternalChar(currentRequest) + "_" + currentRequest.customName;
							break;
					}

					elementConnections.AddIndex(indexer);

					// Find Original Connection
					ConnectivityConnection matchingConnection = elementConnections.FindValue(indexer, uniqueKey).FirstOrDefault();
					ConnectivityConnection newDestinationConnection = null;

					if (matchingConnection == null)
					{
						// Add a new Connection
						if (internalConnection)
						{
#if debug
							protocol.Log("QA" + protocol.QActionID + "|DCF Connection|Adding Internal Connection:" + currentRequest.customName + " | With Connection Filter: " + currentRequest.ConnectionFilter + " | on Element:" + currentRequest.source.ElementKey, LogType.Allways, LogLevel.NoLogging);
#endif
							// add an internal connection
							if (!currentRequest.source.AddConnection(currentRequest.customName, currentRequest.customName, currentRequest.destination, currentRequest.ConnectionFilter, false, out matchingConnection, out newDestinationConnection, 420000))
							{
								protocol.Log(string.Format("QA{0}: |ERR: DCF Connection|Adding Internal DCF Connection:{1} on element {2} Timed-Out after 7 minutes or returned false. Connection may not have been added", protocol.QActionID, currentRequest.customName, sourceElementKey), LogType.Error, LogLevel.NoLogging);
							}
						}
						else
						{
#if debug
							protocol.Log("QA" + protocol.QActionID + "|DCF Connection|Adding External Connection:" + currentRequest.customName + " | With Connection Filter: " + currentRequest.ConnectionFilter + " | from Element:" + currentRequest.source.ElementKey + " To Element:" + currentRequest.destination.ElementKey, LogType.Allways, LogLevel.NoLogging);
#endif
							// add an external connection
							if (!currentRequest.source.AddConnection(currentRequest.customName, currentRequest.customName + " -RETURN", currentRequest.destination, currentRequest.ConnectionFilter, currentRequest.createExternalReturn, out matchingConnection, out newDestinationConnection, 420000))
							{
								protocol.Log(string.Format("QA{0}:|ERR: DCF Connection|Adding External DCF Connection:{1} from element {2} to element {3} Timed-Out after 7 minutes or returned false. Connection may not have been added", protocol.QActionID, currentRequest.customName, sourceElementKey, currentRequest.destination), LogType.Error, LogLevel.NoLogging);
							}
						}
					}
					else
					{
						// Update the Connection
						// Check if Update is Necessary
						if (
							matchingConnection.ConnectionName == currentRequest.customName
							&& matchingConnection.SourceDataMinerId + "/" + matchingConnection.SourceElementId == currentRequest.source.ElementKey
							&& matchingConnection.SourceInterfaceId == currentRequest.source.InterfaceId
							&& matchingConnection.DestinationDMAId + "/" + matchingConnection.DestinationEId == currentRequest.destination.ElementKey
							&& matchingConnection.DestinationInterfaceId == currentRequest.destination.InterfaceId
							&& matchingConnection.ConnectionFilter == currentRequest.ConnectionFilter)
						{
							// NO UPDATE NECESSARY
							updated = false;

#if debug
							if (internalConnection)
							{
								protocol.Log("QA" + protocol.QActionID + "|DCF Connection (" + matchingConnection.ConnectionId + ") |Not Updating Internal Connection (ID:" + matchingConnection.ConnectionId + ") To:" + currentRequest.customName + " on Element:" + currentRequest.source.ElementKey + "-- No Change Detected", LogType.Allways, LogLevel.NoLogging);
							}
							else
							{
								protocol.Log("QA" + protocol.QActionID + "|DCF Connection (" + matchingConnection.ConnectionId + ") |Not Updating External Connection (ID:" + matchingConnection.ConnectionId + ") To:" + currentRequest.customName + " from Element:" + currentRequest.source.ElementKey + " To Element:" + currentRequest.destination.ElementKey + "-- No Change Detected", LogType.Allways, LogLevel.NoLogging);
							}
#endif
						}
						else
						{
							// UPDATE NECESSARY
							if (internalConnection)
							{
#if debug
								protocol.Log("QA" + protocol.QActionID + "|DCF Connection (" + matchingConnection.ConnectionId + ") |Updating Internal Connection (ID:" + matchingConnection.ConnectionId + ") To:" + currentRequest.customName + " | With Connection Filter: " + currentRequest.ConnectionFilter + " | on Element:" + currentRequest.source.ElementKey, LogType.Allways, LogLevel.NoLogging);
#endif
								if (!matchingConnection.Update(currentRequest.customName, currentRequest.source.InterfaceId, currentRequest.customName, currentRequest.destination.DataMinerId, currentRequest.destination.ElementId, currentRequest.destination.InterfaceId, currentRequest.ConnectionFilter, false, out newDestinationConnection, 420000))
								{
									protocol.Log(string.Format("QA{0}:|ERR: DCF Connection (" + matchingConnection.ConnectionId + ") | Updating Internal DCF Connection:{1} on element {2} Timed-Out after 7 minutes or returned false. Connection may not have been updated", protocol.QActionID, currentRequest.customName, sourceElementKey), LogType.Error, LogLevel.NoLogging);
								}
							}
							else
							{
#if debug
								protocol.Log("QA" + protocol.QActionID + "|DCF Connection (" + matchingConnection.ConnectionId + ") |Updating External Connection (ID:" + matchingConnection.ConnectionId + ") To:" + currentRequest.customName + " | With Connection Filter: " + currentRequest.ConnectionFilter + " | from Element:" + currentRequest.source.ElementKey + " To Element:" + currentRequest.destination.ElementKey, LogType.Allways, LogLevel.NoLogging);
#endif
								if (!matchingConnection.Update(currentRequest.customName, currentRequest.source.InterfaceId, currentRequest.customName + " -RETURN", currentRequest.destination.DataMinerId, currentRequest.destination.ElementId, currentRequest.destination.InterfaceId, currentRequest.ConnectionFilter, currentRequest.createExternalReturn, out newDestinationConnection, 420000))
								{
									protocol.Log(string.Format("QA{0}:|ERR: DCF Connection (" + matchingConnection.ConnectionId + ") | Updating External DCF Connection:{1} from element {2} to element {3} Timed-Out after 7 minutes or returned false. Connection may not have been updated", protocol.QActionID, currentRequest.customName, sourceElementKey, currentRequest.destination.ElementKey), LogType.Error, LogLevel.NoLogging);
								}
							}
						}
					}

					string inpEleKye = CreateElementKey(currentRequest.source.DataMinerId, currentRequest.source.ElementId);
					if (currentRequest.fixedConnection)
					{
						// Indicating fixed connections with negative values
						AddToPropertyDictionary(newConnections, inpEleKye, matchingConnection.ConnectionId * -1);
					}
					else
					{
						AddToPropertyDictionary(newConnections, inpEleKye, matchingConnection.ConnectionId);
					}

					result[i] = new DCFSaveConnectionResult(matchingConnection, newDestinationConnection, internalConnection, updated);
				}
				catch (Exception e)
				{
					protocol.Log(string.Format("QA{0}:|ERR: DCF Connection| Exception in SaveConnections for connectionRequest:{1}  with exception:{2}", protocol.QActionID, currentRequest.customName, e.ToString()), LogType.Error, LogLevel.NoLogging);
				}
			}

			return result;
		}

		/// <summary>
		/// This method is used to save both internal and external connections.
		/// </summary>
		/// <param name="requests">One or more DCFSaveConnectionRequest objects that define the connection you wish to save.</param>
		/// <returns>One or more DCFSaveConnectionResults in the same order as the DCFSaveConnectionRequests. If a.</returns>
		public DCFSaveConnectionResult[] SaveConnections(params DCFSaveConnectionRequest[] requests)
		{
			return SaveConnections(false, requests);
		}

		/// <summary>
		/// Will save an External Connection based on a Unique Value used to indicate the interface. This method is to be used together with UpdateInterfaces method which creates a mapping of all Unique Values and Interface Objects. Make sure to call UpdateInternalInterfaces  or UpdateExternalInterfaces before using this method.
		/// </summary>
		/// <param name="inputSearchValue">Unique Value indicating the Input Interface.</param>
		/// <param name="outputSearchValue">Unique Value indicating the Output Interface.</param>
		/// <param name="connectionName">Name of the connection.</param>
		/// <param name="saveOnBoth">Indicates if the connection has to be made on the Output but in the opposite direction.</param>
		/// <returns>The created or Updated Connection objects in a Tuple with Item1 = local connection and Item2 = remote connection, will return Null if the save Failed.</returns>
		public Tuple<ConnectivityConnection, ConnectivityConnection> SaveExternalConnection(string inputSearchValue, string outputSearchValue, string connectionName, bool saveOnBoth)
		{
			ConnectivityInterface inp = null;
			ConnectivityInterface outp = null;
			Tuple<ConnectivityConnection, ConnectivityConnection> result = null;
			interfacesSV.TryGetValue(inputSearchValue, out inp);
			interfacesSV.TryGetValue(outputSearchValue, out outp);
			if (inp != null && outp != null)
			{
				result = SaveExternalConnection(inp, outp, connectionName, saveOnBoth);
			}

			return result;
		}

		/// <summary>
		/// Warning: [Obsolete - Please use SaveConnections()]Will add or update an external connection.
		/// </summary>
		/// <param name="input">The Input Interface.</param>
		/// <param name="output">The Output Interface.</param>
		/// <param name="connectionName">The Name of the connection.</param>
		/// <param name="saveOnBoth">Indicates if the connection has to be made on the Output but in the opposite direction.</param>
		/// <returns>The created or Updated Connection objects in a Tuple with Item1 = local connection and Item2 = remote connection, will return Null if the save Failed.</returns>
		public Tuple<ConnectivityConnection, ConnectivityConnection> SaveExternalConnection(ConnectivityInterface input, ConnectivityInterface output, string connectionName, bool saveOnBoth)
		{
			var conReq = new DCFSaveConnectionRequest(input, output, connectionName);
			conReq.createExternalReturn = saveOnBoth;
			var result = SaveConnections(conReq);
			if (result[0] != null)
			{
				return new Tuple<ConnectivityConnection, ConnectivityConnection>(result[0].sourceConnection, result[0].destinationConnection);
			}
			else return null;
		}

		/// <summary>
		/// Saves a collection of ConnectivityInterfaceProperty objects to a given ConnectivityInterface.
		/// </summary>
		/// <param name="connectivityInterface">The Interface to link the Properties to.</param>
		/// <param name="full">When full is true, this call will auto-remove any property previously added (by this code) and not being saved.</param>
		/// <param name="fixedProperty">When FixedProperty is true, this property will never be automatically removed. Only manual removal will get rid of it.</param>
		/// <param name="newInterfProps">One or more ConnectivityInterfaceProperty Objects holding the new/updated data.</param>
		/// <returns>A boolean indicating the save was successful.</returns>
		public bool SaveInterfaceProperties(ConnectivityInterface connectivityInterface, bool full, bool fixedProperty, params ConnectivityInterfaceProperty[] newInterfProps)
		{
			try
			{
				bool nullInputDetected = false;
				if (connectivityInterface == null)
				{
					protocol.Log(string.Format("QA{0}:|ERR: DCF Interface Property|Save Interface Properties ConnectivityInterface connectivityInterface was Null", protocol.QActionID), LogType.Error, LogLevel.NoLogging);
					nullInputDetected = true;
				}

				if (nullInputDetected) return false;

				if (cInterfacePropPID == -1)
				{
					protocol.Log("QA" + protocol.QActionID + "|ERR: DCF Interface Property|DCFHelper Error: Using SaveInterfaceProperties requires the CurrentInterfacePropertiesPID to be defined! Please change the Options Objects to include this PID", LogType.Error, LogLevel.NoLogging);
					return false;
				}

				bool finalResult = true;

				// Retrieve all properties for this connection in a single call, if they haven't already been called earlier.
				Dictionary<string, ConnectivityInterfaceProperty> allProps;
				if (!interfaceProperties.TryGetValue(connectivityInterface.InterfaceId + "-" + connectivityInterface.ElementKey, out allProps))
				{
					var itfProps = connectivityInterface.InterfaceProperties;
					allProps = itfProps.GroupBy(p => p.Value.InterfacePropertyName).ToDictionary(item => item.Key, item => item.First().Value);
					itfProps = null;
					interfaceProperties.Add(connectivityInterface.InterfaceId + "-" + connectivityInterface.ElementKey, allProps);
				}

				// parse all properties and add them (this should be updated in the future to a bulk set if it gets added to DCF)
				int[] result = new int[newInterfProps.Length];

				for (int i = 0; i < newInterfProps.Length; i++)
				{
					var newInterfProp = newInterfProps[i];
					if (newInterfProp == null)
					{
						result[i] = -1;
						continue;
					}

					ConnectivityInterfaceProperty prop;
					if (allProps.TryGetValue(newInterfProp.InterfacePropertyName, out prop))
					{
						// UPDATE PROPERTY
						if (prop.InterfacePropertyName == newInterfProp.InterfacePropertyName && prop.InterfacePropertyType == newInterfProp.InterfacePropertyType && prop.InterfacePropertyValue == newInterfProp.InterfacePropertyValue)
						{
#if debug
							protocol.Log("QA" + protocol.QActionID + "|DCF Interface Property (" + prop.InterfacePropertyId + ")|Not Updating Interface Property:" + prop.InterfacePropertyId + "/" + newInterfProp.InterfacePropertyName + ":" + newInterfProp.InterfacePropertyValue + "-- No Change Detected", LogType.Allways, LogLevel.NoLogging);
#endif
						}
						else
						{
#if debug
							protocol.Log("QA" + protocol.QActionID + "|DCF Interface Property (" + prop.InterfacePropertyId + ")|Updating Interface Property:" + prop.InterfacePropertyId + "/" + newInterfProp.InterfacePropertyName + ":" + newInterfProp.InterfacePropertyValue, LogType.Allways, LogLevel.NoLogging);
#endif
							prop.InterfacePropertyType = newInterfProp.InterfacePropertyType;
							prop.InterfacePropertyValue = newInterfProp.InterfacePropertyValue;
							if (prop.Update())
							{
								interfaceProperties[connectivityInterface.InterfaceId + "-" + connectivityInterface.ElementKey][newInterfProp.InterfacePropertyName] = prop;
							}
							else
							{
								protocol.Log(string.Format("QA{0}:|ERR: DCF Interface Property (" + prop.InterfacePropertyId + ")| Updating Interface Property:{1} Returned False! Property may not have been Updated!", protocol.QActionID, prop.InterfacePropertyId + "/" + newInterfProp.InterfacePropertyName + ":" + newInterfProp.InterfacePropertyValue), LogType.Error, LogLevel.NoLogging);
								finalResult = false;
							}
						}
					}
					else
					{
						// ADD PROPERTY
#if debug
						protocol.Log("QA" + protocol.QActionID + "|DCF Interface Property|Adding Interface Property:" + newInterfProp.InterfacePropertyName + ":" + newInterfProp.InterfacePropertyValue, LogType.Allways, LogLevel.NoLogging);
#endif
						if (connectivityInterface.AddProperty(newInterfProp.InterfacePropertyName, newInterfProp.InterfacePropertyType, newInterfProp.InterfacePropertyValue, out prop, 42000))
						{
							interfaceProperties[connectivityInterface.InterfaceId + "-" + connectivityInterface.ElementKey].Add(newInterfProp.InterfacePropertyName, prop);
#if debug
							protocol.Log("QA" + protocol.QActionID + "|DCF Interface Property (" + prop.InterfacePropertyId + ")|Property Added Id:" + prop.InterfacePropertyId, LogType.Allways, LogLevel.NoLogging);
#endif
						}
						else
						{
							protocol.Log(string.Format("QA{0}:|ERR: DCF Interface Property (" + prop.InterfacePropertyId + ")|Adding Interface Property:{1} Timed out after 7 minutes. Property may not have been Added!", protocol.QActionID, prop.InterfacePropertyId + "/" + newInterfProp.InterfacePropertyName + ":" + newInterfProp.InterfacePropertyValue), LogType.Error, LogLevel.NoLogging);
							finalResult = false;
						}
					}

					result[i] = prop.InterfacePropertyId;

					string eleKey = CreateElementKey(connectivityInterface.DataMinerId, connectivityInterface.ElementId);
					if (fixedProperty)
					{
						AddToPropertyDictionary(newInterfaceProperties, eleKey, -1 * result[i]);
					}
					else
					{
						AddToPropertyDictionary(newInterfaceProperties, eleKey, result[i]);
					}
				}

				if (full)
				{
					var propertiesToDelete = allProps.Values.Select(p => p.InterfacePropertyId).Except(result);
					RemoveInterfaceProperties(connectivityInterface, false, propertiesToDelete.ToArray());
				}

				return finalResult;
			}
			catch (Exception e)
			{
				protocol.Log(string.Format("QA{0}:|ERR: DCF Interface Property| (Exception) Value at {1} with Exception:{2}", protocol.QActionID, "SaveInterfaceProperties", e.ToString()), LogType.Error, LogLevel.NoLogging);
			}

			return false;
		}

		/// <summary>
		/// Saves a collection of ConnectivityInterfaceProperty objects to a given ConnectivityInterface.
		/// </summary>
		/// <param name="connectivityInterface">The Interface to link the Properties to.</param>
		/// <param name="full">Indicates that every Property for this interface is provided in one go (similar to Fill Table At Once). Any Property managed by this Element that is still connected to this Interface will be removed.</param>
		/// <param name="newInterfProps">One or more ConnectivityConnectionProperty Objects holding the new/updated data.</param>
		/// <returns>An Integer array holding all the ID's of successfully saved Properties.</returns>
		public bool SaveInterfaceProperties(ConnectivityInterface connectivityInterface, bool full, params ConnectivityInterfaceProperty[] newInterfProps)
		{
			return SaveInterfaceProperties(connectivityInterface, full, false, newInterfProps);
		}

		/// <summary>
		/// Will save an Internal Connection based on a Unique Value used to indicate the interface. This method is to be used together with UpdateInterfaces method which creates a mapping of all Unique Values and Interface Objects. Make sure to call UpdateInternalInterfaces  or UpdateExternalInterfaces before using this method.
		/// </summary>
		/// <param name="inputSearchValue">Unique Value indicating the Input Interface.</param>
		/// <param name="outputSearchValue">Unique Value indicating the Output Interface.</param>
		/// <param name="connectionName">Name of the connection.</param>
		/// <returns>The created or Updated Connection object, will return Null if the save Failed.</returns>
		public ConnectivityConnection SaveInternalConnection(string inputSearchValue, string outputSearchValue, string connectionName)
		{
			ConnectivityInterface inp = null;
			ConnectivityInterface outp = null;
			ConnectivityConnection connection = null;
			interfacesSV.TryGetValue(inputSearchValue, out inp);
			interfacesSV.TryGetValue(outputSearchValue, out outp);
			if (inp != null && outp != null)
			{
				connection = SaveInternalConnection(inp, outp, connectionName);
			}

			return connection;
		}

		/// <summary>
		/// Will Save an internal connection [Obsolete - Please use SaveConnections()].
		/// </summary>
		/// <param name="input">The Input Interface.</param>
		/// <param name="output">The Output Interface.</param>
		/// <param name="connectionName">The Name for this Connection.</param>
		/// <returns>A ConnectivityConnection object, will return Null if the save Failed.</returns>
		public ConnectivityConnection SaveInternalConnection(ConnectivityInterface input, ConnectivityInterface output, string connectionName)
		{
			var result = SaveConnections(new DCFSaveConnectionRequest(input, output, connectionName));
			if (result[0] != null)
			{
				return result[0].sourceConnection;
			}
			else return null;
		}

		/// <summary>
		/// Creates/Updates an Internal Mapping of All Interfaces with as Key the string value from the 'searchValueColumnIdx' chosen in this method. And the Value is the ConnectivityInterface Object.
		/// </summary>
		/// <param name="dmaID">The dma ID.</param>
		/// <param name="eleID">The element ID.</param>
		/// <param name="tableID">Table containing Interfaces (this should correspond to a Parameter Group Dynamic ID).</param>
		/// <param name="descrColumnIdx">IDX of the column Containing the Description of the row. If Naming is used, this IDX should contain the complete description created by Naming.</param>
		/// <param name="searchValueColumnIdx">IDX of the column containing a string value to be used as Key to lookup ConnectivityInterface Objects. This should be unique.</param>
		/// <param name="parameterGroupNames">All possible ParameterGroupNames that can occur for this Table.</param>
		public void UpdateExternalInterfaces(int dmaID, int eleID, int tableID, UInt32 descrColumnIdx, UInt32 searchValueColumnIdx, string[] parameterGroupNames)
		{
			try
			{
				var allInterfaces = protocol.GetConnectivityInterfaces(dmaID, eleID);
				var newInterfacesNames = allInterfaces.GroupBy(p => p.Value.InterfaceName).ToDictionary(item => item.Key, item => item.First().Value);
				allInterfaces = null;
				DMSClass dms = new DMSClass();
				Object returnValue = new Object();
				dms.Notify(87/*DMS_GET_VALUE*/, 0, new UInt32[] { (UInt32)dmaID, (UInt32)eleID }, tableID, out returnValue);

				// Checking returnValue for null will not work, returnValue will throw a COMException
				object[] returnValueA = (Object[])returnValue;
				object[] columns = (Object[])returnValueA[4]; // Value of table
				if (columns == null) return;
				object[] keys = (object[])columns[descrColumnIdx];
				object[] ips = (object[])columns[searchValueColumnIdx];

				if (ips != null && ips.Length > 0)
				{
					for (int i = 0; i < ips.Length; i++)
					{
						string key = Convert.ToString(((object[])keys[i])[0]);
						string searchValue = Convert.ToString(((object[])ips[i])[0]);
						for (int u = 0; u < parameterGroupNames.Length; u++)
						{
							string intfKey = parameterGroupNames[u] + " " + key;
							ConnectivityInterface interf;
							newInterfacesNames.TryGetValue(intfKey, out interf);
							if (interf != null)
							{
								interfacesSV[dmaID + "/" + eleID + "/" + searchValue] = interf;
								break;
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				protocol.Log(string.Format("QA{0}:|ERR: DCF Interface|(DMS Call ERROR) {1} at UpdateExternalInterfaces with Exception:{2}", protocol.QActionID, "Bad DMAId/EleID/ParaID", e.ToString()), LogType.Error, LogLevel.NoLogging);
			}
		}

		/// <summary>
		/// Creates/Updates an Internal Mapping of All Interfaces with as Key the string value from the 'searchValueColumnIdx' chosen in this method. And the Value is the ConnectivityInterface Object.
		/// </summary>
		/// <param name="tableID">Table containing Interfaces (this should correspond to a Parameter Group Dynamic ID).</param>
		/// <param name="descrColumnIdx">IDX of the column Containing the Description of the row. If Naming is used, this IDX should contain the complete description created by Naming.</param>
		/// <param name="searchValueColumnIdx">IDX of the column containing a string value to be used as Key to lookup ConnectivityInterface Objects. This should be unique.</param>
		/// <param name="parameterGroupNames">All possible ParameterGroupNames that can occur for this Table.</param>
		public void UpdateInterfaces(int tableID, UInt32 descrColumnIdx, UInt32 searchValueColumnIdx, params string[] parameterGroupNames)
		{
			try
			{
				var allInterfaces = protocol.GetConnectivityInterfaces(protocol.DataMinerID, protocol.ElementID);
				var newInterfacesNames = allInterfaces.GroupBy(p => p.Value.InterfaceName).ToDictionary(item => item.Key, item => item.First().Value);
				allInterfaces = null;
				object[] columns = (Object[])protocol.NotifyProtocol(321 /*NT_GT_TABLE_COLUMNS*/, tableID, new UInt32[] { descrColumnIdx, searchValueColumnIdx });
				object[] keys = (object[])columns[0];
				object[] ips = (object[])columns[1];

				if (ips != null && ips.Length > 0)
				{
					for (int i = 0; i < ips.Length; i++)
					{
						string key = Convert.ToString(keys[i]);
						string searchValue = Convert.ToString(ips[i]);
						for (int u = 0; u < parameterGroupNames.Length; u++)
						{
							string intfKey = parameterGroupNames[u] + " " + key;
							ConnectivityInterface interf;
							newInterfacesNames.TryGetValue(intfKey, out interf);
							if (interf != null)
							{
								interfacesSV[searchValue] = interf;
								break;
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				protocol.Log(string.Format("QA{0}:|ERR: DCF Interface|(Exception) at UpdateInterfaces with Exception:{1}", protocol.QActionID, e.ToString()), LogType.Error, LogLevel.NoLogging);
			}
		}

		private void AddToPropertyDictionary(Dictionary<string, HashSet<int>> propertyDictionary, string eleKey, int[] propIDs)
		{
			HashSet<int> returned;
			if (propertyDictionary.TryGetValue(eleKey, out returned))
			{
				returned.UnionWith(propIDs);
			}
			else
			{
				returned = new HashSet<int>(propIDs);
				propertyDictionary.Add(eleKey, returned);
			}
		}

		private void AddToPropertyDictionary(Dictionary<string, HashSet<int>> propertyDictionary, string eleKey, int propID)
		{
			HashSet<int> returned;
			if (propertyDictionary.TryGetValue(eleKey, out returned))
			{
				returned.Add(propID);
			}
			else
			{
				returned = new HashSet<int>();
				returned.Add(propID);
				propertyDictionary.Add(eleKey, returned);
			}
		}

		private string CreateElementKey(int dataMinerID, int eleID)
		{
			return dataMinerID + "/" + eleID;
		}

		private string InternalExternalChar(ConnectivityConnection p)
		{
			return (p.DestinationDMAId == p.SourceDataMinerId && p.SourceElementId == p.DestinationEId) ? "I" : "E";
		}

		private string InternalExternalChar(DCFSaveConnectionRequest p)
		{
			return (p.source.ElementKey == p.destination.ElementKey) ? "I" : "E";
		}

		/// <summary>
		/// Allows to know if an element is active (in SLDMS) (and optionally, loop until it is).
		/// </summary>
		/// <param name="protocol">The SLProtocol object.</param>
		/// <param name="iDmaId">ID of the DMA on which the element to be checked is located.</param>
		/// <param name="iElementId">ID of the element to be checked.</param>
		/// <param name="iSecondsToWait">Number of seconds to wait for the element to be active.</param>
		/// <returns>True if the element is active within the given 'iSecondsToWait'.</returns>
		private bool IsElementActiveInSLDMS(SLProtocol protocol, UInt32 iDmaId, UInt32 iElementId, int iSecondsToWait = 0)
		{
			bool bFullyLoaded = false;

			DateTime dtStart = DateTime.Now;
			int iElapsedSeconds = 0;

			while (!bFullyLoaded && iElapsedSeconds <= iSecondsToWait)
			{
				try
				{
					DMSClass dms = new DMSClass();
					object oState = null;
					dms.Notify(91/*DMS_GET_ELEMENT_STATE*/, 0, iDmaId, iElementId, out oState);

					string sElementState = Convert.ToString(oState);
					if (sElementState.Equals("Active", StringComparison.InvariantCultureIgnoreCase))
					{
						bFullyLoaded = true;
					}
				}
				catch (Exception ex)
				{
					protocol.Log("QA" + protocol.QActionID + "|ERR: IsElementActiveInSLDMS|Exception thrown : " + Environment.NewLine + ex.ToString(), LogType.Error, LogLevel.NoLogging);
				}

				if (!bFullyLoaded)
				{
					System.Threading.Thread.Sleep(100);
					iElapsedSeconds = (int)(DateTime.Now - dtStart).TotalSeconds;
				}
			}

			return bFullyLoaded;
		}

		/// <summary>
		/// Allows to know if an element is fully loaded in SLElement (and optionally, loop until it is).
		/// </summary>
		/// <param name="protocol">The SLProtocol object.</param>
		/// <param name="iDmaId">ID of the DMA on which the element to be checked is located.</param>
		/// <param name="iElementId">ID of the element to be checked.</param>
		/// <param name="iSecondsToWait">Number of seconds to wait for the element to be fully loaded.</param>
		/// <returns>True if the element is fully loaded within the given 'iSecondsToWait'.</returns>
		private bool IsElementLoadedInSLElement(SLProtocol protocol, UInt32 iDmaId, UInt32 iElementId, int iSecondsToWait = 0)
		{
			bool bIsFullyStarted = false;

			DateTime dtStart = DateTime.Now;
			int iElapsedSeconds = 0;
			string sExceptionThrown = null;

			while (!bIsFullyStarted && iElapsedSeconds <= iSecondsToWait)
			{
				try
				{
					object oResult = protocol.NotifyDataMiner(377/*NT_ELEMENT_STARTUP_COMPLETE*/, new UInt32[] { iDmaId, iElementId }, null);

					if (oResult != null)
					{
						bIsFullyStarted = Convert.ToBoolean(oResult);
					}
				}
				catch (Exception ex)
				{
					sExceptionThrown = ex.ToString();
				}

				if (!bIsFullyStarted)
				{
					System.Threading.Thread.Sleep(100);
					iElapsedSeconds = (int)(DateTime.Now - dtStart).TotalSeconds;
				}
			}

			if (!bIsFullyStarted && sExceptionThrown != null)
			{
				protocol.Log("QA" + protocol.QActionID + "|ERR: IsElementLoadedInSLElement|Exception thrown :" + Environment.NewLine + sExceptionThrown, LogType.Error, LogLevel.NoLogging);
			}

			return bIsFullyStarted;
		}

		/// <summary>
		/// Allows to know if an element is fully loaded in SLNet (and optionally, loop until it is).
		/// </summary>
		/// <param name="protocol">The SLProtocol object.</param>
		/// <param name="iDmaId">ID of the DMA on which the element to be checked is located.</param>
		/// <param name="iElementId">ID of the element to be checked.</param>
		/// <param name="iSecondsToWait">Number of seconds to wait for the element to be fully loaded.</param>
		/// <returns>True if the element is fully loaded within the given 'iSecondsToWait'.</returns>
		private bool IsElementLoadedInSLNet(SLProtocol protocol, UInt32 iDmaId, UInt32 iElementId, int iSecondsToWait = 0)
		{
			bool bFullyLoaded = false;

			DateTime dtStart = DateTime.Now;
			int iElapsedSeconds = 0;

			while (!bFullyLoaded && iElapsedSeconds <= iSecondsToWait)
			{
				GetElementProtocolMessage getElPro = new GetElementProtocolMessage((int)iDmaId, (int)iElementId);
				DMSMessage[] result = protocol.SLNet.SendMessage(getElPro);
				if (result != null)
				{
					foreach (GetElementProtocolResponseMessage response in result)
					{
						if (response != null)
						{
							if (!response.WasBuiltWithUnsafeData)
							{
								bFullyLoaded = true;
							}
						}
					}
				}

				if (!bFullyLoaded)
				{
					System.Threading.Thread.Sleep(100);
					iElapsedSeconds = (int)(DateTime.Now - dtStart).TotalSeconds;
				}
			}

			return bFullyLoaded;
		}

		private bool IsElementStarted(SLProtocol protocol, int dmaID, int eleID, int timeoutSeconds)
		{
			bool result = false;
			try
			{
				UInt32[] varValue1 = new UInt32[2];
				varValue1[0] = Convert.ToUInt32(dmaID);
				varValue1[1] = Convert.ToUInt32(eleID);
				result = true;
#if debug
				protocol.Log("QA" + protocol.QActionID + "| DBG Load Check ***** SLDMS", LogType.DebugInfo, LogLevel.NoLogging);
#endif
				result = result && IsElementActiveInSLDMS(protocol, varValue1[0], varValue1[1], timeoutSeconds);
#if debug
				if (result == false)
				{
					protocol.Log("QA" + protocol.QActionID + "|SLDMS Check Failed!", LogType.Error, LogLevel.NoLogging);
					return result;
				}

				protocol.Log("QA" + protocol.QActionID + "| DBG Load Check ***** SLElement", LogType.DebugInfo, LogLevel.NoLogging);
#endif
				result = result && IsElementLoadedInSLElement(protocol, varValue1[0], varValue1[1], timeoutSeconds);
				if (result == false)
				{
					protocol.Log("QA" + protocol.QActionID + "|SLElement Check Failed!", LogType.Error, LogLevel.NoLogging);
					return result;
				}
#if debug
				protocol.Log("QA" + protocol.QActionID + "| DBG Load Check ***** SLNet", LogType.DebugInfo, LogLevel.NoLogging);
#endif
				result = result && IsElementLoadedInSLNet(protocol, varValue1[0], varValue1[1], timeoutSeconds);
				if (result == false)
				{
					protocol.Log("QA" + protocol.QActionID + "|SLNet Check Failed!", LogType.Error, LogLevel.NoLogging);
					return result;
				}
#if debug
				protocol.Log("QA" + protocol.QActionID + "| DBG Load Check ***** Finished", LogType.DebugInfo, LogLevel.NoLogging);
#endif
			}
			catch (Exception e)
			{
				result = false;
				protocol.Log(string.Format("QA{0}:|ERR: DCF STARTUP|(Exception) at IsElementStarted:{1} with Exception:{2}", protocol.QActionID, dmaID + "/" + eleID, e.ToString()), LogType.Error, LogLevel.NoLogging);
			}

			return result;
		}

		private void PropDictionaryToBuffer(Dictionary<string, HashSet<int>> dic, int paramID)
		{
			StringBuilder newBuffer = new StringBuilder();
			if (dic != null)
			{
				foreach (var dicEle in dic)
				{
					string eleKey = dicEle.Key;
					newBuffer.Append(eleKey);
					foreach (int pid in dicEle.Value)
					{
						newBuffer.Append('/');
						newBuffer.Append(pid);
					}

					newBuffer.Append(";");
				}
			}

			string result = newBuffer.ToString().TrimEnd(';');
#if debug
			protocol.Log("QA" + protocol.QActionID + "|DCF New Mapping (" + paramID + ")|" + result, LogType.Allways, LogLevel.NoLogging);
#endif
			protocol.SetParameter(paramID, result);
		}

		private void PropertiesBufferToDictionary(int parameterID, Dictionary<string, HashSet<int>> propDic)
		{
			string currentItfsProps = Convert.ToString(protocol.GetParameter(parameterID));
#if debug
			protocol.Log("QA" + protocol.QActionID + "|DCF Old Mapping (" + parameterID + ")|" + currentItfsProps, LogType.Allways, LogLevel.NoLogging);
#endif
			foreach (string itfsProp in currentItfsProps.Split(';'))
			{
				if (!String.IsNullOrEmpty(itfsProp))
				{
					string eleKey;
					string propKeys;
					SplitElePropKey(itfsProp, out eleKey, out propKeys);
					if (String.IsNullOrEmpty(propKeys)) continue;
					string[] propKeysA = propKeys.Split('/');
					int[] propKeysInt = Array.ConvertAll<string, int>(propKeysA, p => Convert.ToInt32(p));
					AddToPropertyDictionary(propDic, eleKey, propKeysInt);
				}
			}
		}

		private void RemoveDeleted()
		{
			Dictionary<string, HashSet<int>> itfsToDelete = new Dictionary<string, HashSet<int>>();
			Dictionary<string, HashSet<int>> conPropsToDelete = new Dictionary<string, HashSet<int>>();
			Dictionary<string, HashSet<int>> connectionsToDelete = new Dictionary<string, HashSet<int>>();
			Dictionary<string, string> elementStates = new Dictionary<string, string>();

			if (cConnectionsPID != -1)
			{
				foreach (var currentConnection in currentConnections)
				{
					HashSet<int> internalNewConnections;
					IEnumerable<int> source;
					if (newConnections.TryGetValue(currentConnection.Key, out internalNewConnections))
					{
						source = currentConnection.Value.Except(internalNewConnections).Where(i => i >= 0); // all the current values not in new
					}
					else
					{
						source = currentConnection.Value.Where(i => i >= 0);
					}

					HashSet<int> currentToDelete;
					if (connectionsToDelete.TryGetValue(currentConnection.Key, out currentToDelete))
					{
						currentToDelete.UnionWith(source);
					}
					else
					{
						HashSet<int> internalToDelete = new HashSet<int>(source);
						connectionsToDelete.Add(currentConnection.Key, internalToDelete);
					}
				}
			}

			if (cInterfacePropPID != -1)
			{
				foreach (var currentInterfaceProperty in currentInterfaceProperties)
				{
					HashSet<int> internalNewProps;
					IEnumerable<int> source;
					if (newInterfaceProperties.TryGetValue(currentInterfaceProperty.Key, out internalNewProps))
					{
						source = currentInterfaceProperty.Value.Except(internalNewProps).Where(i => i >= 0); // all the current values not in new
					}
					else
					{
						source = currentInterfaceProperty.Value.Where(i => i >= 0);
					}

					HashSet<int> currentToDelete;
					if (itfsToDelete.TryGetValue(currentInterfaceProperty.Key, out currentToDelete))
					{
						currentToDelete.UnionWith(source);
					}
					else
					{
						HashSet<int> internalToDelete = new HashSet<int>(source);
						itfsToDelete.Add(currentInterfaceProperty.Key, internalToDelete);
					}
				}
			}

			if (cConnectionPropPID != -1)
			{
				foreach (var currentConnectionProperty in currentConnectionProperties)
				{
					HashSet<int> internalNewProps;
					IEnumerable<int> source;
					if (newConnectionProperties.TryGetValue(currentConnectionProperty.Key, out internalNewProps))
					{
						source = currentConnectionProperty.Value.Except(internalNewProps).Where(i => i >= 0); // all the current values not in new
					}
					else
					{
						source = currentConnectionProperty.Value.Where(i => i >= 0);
					}

					HashSet<int> currentToDelete;
					if (conPropsToDelete.TryGetValue(currentConnectionProperty.Key, out currentToDelete))
					{
						currentToDelete.UnionWith(source);
					}
					else
					{
						HashSet<int> internalToDelete = new HashSet<int>(source);
						conPropsToDelete.Add(currentConnectionProperty.Key, internalToDelete);
					}
				}
			}

			// Delete Connections (will automatically remove all properties for this connection)
			if (connectionsToDelete.Count > 0)
			{
				foreach (var delCon in connectionsToDelete)
				{
					if (unloadedElements.Contains(delCon.Key))
					{
						protocol.Log(string.Format("QA{0}: |ERR: DCF Cleanup|Ignoring Connection Cleanup: Unloaded Element:{1} ", protocol.QActionID, delCon.Key), LogType.Error, LogLevel.NoLogging);
						continue;
					}

					string eleKey = delCon.Key;
					if (delCon.Value.Count > 0)
					{
						int thisDMAID;
						int thisEleID;
						SplitEleKey(eleKey, out thisDMAID, out thisEleID);

						HashSet<int> managedNewByThisProtocol;
						if (!newConnections.TryGetValue(eleKey, out managedNewByThisProtocol)) managedNewByThisProtocol = new HashSet<int>();

						HashSet<int> managedCurrentByThisProtocol;
						if (!currentConnections.TryGetValue(eleKey, out managedCurrentByThisProtocol)) managedCurrentByThisProtocol = new HashSet<int>();

						string state;
						if (!elementStates.TryGetValue(eleKey, out state)) state = GetElementState((UInt32)thisDMAID, (UInt32)thisEleID);
						bool deleted = String.IsNullOrEmpty(state);
						bool active = state == "active";

						foreach (int key in delCon.Value)
						{
							if (active || deleted)
							{
								if (active)
								{
									protocol.DeleteConnectivityConnection(key, thisDMAID, thisEleID, true);
								}

#if debug
								protocol.Log("QA" + protocol.QActionID + "|DCF Connection (" + key + ")|Sync- Deleted Connection:" + eleKey + "/" + key, LogType.Allways, LogLevel.NoLogging);
#endif
								managedCurrentByThisProtocol.Remove(key);
								managedNewByThisProtocol.Remove(key);
							}
							else
							{
								managedNewByThisProtocol.Add(key);
							}
						}

						newConnections[eleKey] = managedNewByThisProtocol;
						currentConnections[eleKey] = managedCurrentByThisProtocol;
					}
				}
			}

			if (itfsToDelete.Count > 0)
			{
				// Delete Interface Properties
				foreach (var delItf in itfsToDelete)
				{
					if (unloadedElements.Contains(delItf.Key))
					{
						protocol.Log(string.Format("QA{0}: |ERR: DCF Cleanup|Ignoring Interface Property Cleanup: Unloaded Element:{1} ", protocol.QActionID, delItf.Key), LogType.Error, LogLevel.NoLogging);
						continue;
					}

					string eleKey = delItf.Key;
					HashSet<int> managedNewByThisProtocol;
					if (delItf.Value.Count > 0)
					{
						if (!newInterfaceProperties.TryGetValue(eleKey, out managedNewByThisProtocol)) managedNewByThisProtocol = new HashSet<int>();

						HashSet<int> managedCurrentByThisProtocol;
						if (!currentInterfaceProperties.TryGetValue(eleKey, out managedCurrentByThisProtocol)) managedCurrentByThisProtocol = new HashSet<int>();

						int thisDMAID;
						int thisEleID;
						SplitEleKey(eleKey, out thisDMAID, out thisEleID);

						string state;
						if (!elementStates.TryGetValue(eleKey, out state)) state = GetElementState((UInt32)thisDMAID, (UInt32)thisEleID);
						bool deleted = String.IsNullOrEmpty(state);
						bool active = state == "active";

						foreach (int key in delItf.Value)
						{
							if (active || deleted)
							{
								if (active)
								{
									protocol.DeleteConnectivityInterfaceProperty(key, thisDMAID, thisEleID);
								}

#if debug
								protocol.Log("QA" + protocol.QActionID + "|DCF Interface Property (" + key + ")|Sync- Deleted Interface Property:" + eleKey + "/" + key, LogType.Allways, LogLevel.NoLogging);
#endif
								managedCurrentByThisProtocol.Remove(key);
								managedNewByThisProtocol.Remove(key);
							}
							else
							{
								managedNewByThisProtocol.Add(key);
							}
						}

						newInterfaceProperties[eleKey] = managedNewByThisProtocol;
						currentInterfaceProperties[eleKey] = managedCurrentByThisProtocol;
					}
				}
			}

			if (conPropsToDelete.Count > 0)
			{
				foreach (var delConProp in conPropsToDelete)
				{
					if (unloadedElements.Contains(delConProp.Key))
					{
						protocol.Log(string.Format("QA{0}: |ERR: DCF Cleanup|Ignoring Connection Property Cleanup: Unloaded Element:{1} ", protocol.QActionID, delConProp.Key), LogType.Error, LogLevel.NoLogging);
						continue;
					}

					string eleKey = delConProp.Key;
					int thisDMAID;
					int thisEleID;
					SplitEleKey(eleKey, out thisDMAID, out thisEleID);

					if (delConProp.Value.Count > 0)
					{
						HashSet<int> managedNewByThisProtocol;
						if (!newConnectionProperties.TryGetValue(eleKey, out managedNewByThisProtocol)) managedNewByThisProtocol = new HashSet<int>();

						HashSet<int> managedCurrentByThisProtocol;
						if (!currentConnectionProperties.TryGetValue(eleKey, out managedCurrentByThisProtocol)) managedCurrentByThisProtocol = new HashSet<int>();

						string state;
						if (!elementStates.TryGetValue(eleKey, out state)) state = GetElementState((UInt32)thisDMAID, (UInt32)thisEleID);
						bool deleted = String.IsNullOrEmpty(state);
						bool active = state == "active";

						foreach (int key in delConProp.Value)
						{
							if (active || deleted)
							{
								if (active)
								{
									protocol.DeleteConnectivityConnectionProperty(key, thisDMAID, thisEleID);
								}

#if debug
								protocol.Log("QA" + protocol.QActionID + "|Connection Property (" + key + ") |Sync- Deleted Connection Property:" + eleKey + "/" + key, LogType.Allways, LogLevel.NoLogging);
#endif
								managedCurrentByThisProtocol.Remove(key);
								managedNewByThisProtocol.Remove(key);
							}
							else
							{
								managedNewByThisProtocol.Add(key);
							}
						}

						newConnectionProperties[eleKey] = managedNewByThisProtocol;
						currentConnectionProperties[eleKey] = managedCurrentByThisProtocol;
					}
				}
			}
		}

		private bool SplitEleKey(string elementKey, out int dmaID, out int elementID)
		{
			string[] elementKeyA = elementKey.Split('/');
			if (elementKeyA.Length > 1)
			{
				dmaID = Convert.ToInt32(elementKeyA[0]);
				elementID = Convert.ToInt32(elementKeyA[1]);
				return true;
			}
			else
			{
				dmaID = -1;
				elementID = -1;
				return false;
			}
		}

		private void SplitElePropKey(string itfsProp, out string eleID, out string propKey)
		{
			int endOfDmaID = itfsProp.IndexOf('/');
			if (endOfDmaID != -1)
			{
				int endOfEleID = itfsProp.IndexOf('/', endOfDmaID + 1);
				if (endOfEleID == -1)
				{
					eleID = itfsProp;
					propKey = String.Empty;
				}
				else
				{
					eleID = itfsProp.Substring(0, endOfEleID);
					propKey = itfsProp.Substring(endOfEleID + 1);
				}
			}
			else
			{
				eleID = String.Empty;
				propKey = String.Empty;
			}
		}

		/// <summary>
		/// Syncs all changes done to external Parameters that hold the Property Mappings. This needs to be called to keep track of the properties that are managed by this driver.
		/// </summary>
		private void SyncMapping()
		{
#if debug
			protocol.Log("QA" + protocol.QActionID + "|DCF Starting Sync|", LogType.Allways, LogLevel.NoLogging);
#endif

			// Add the negative mapping to the newMapping
			if (cConnectionPropPID != -1) SyncNegative(currentConnectionProperties, newConnectionProperties);
			if (cInterfacePropPID != -1) SyncNegative(currentInterfaceProperties, newInterfaceProperties);
			if (cConnectionsPID != -1) SyncNegative(currentConnections, newConnections);

			switch (helperType)
			{
				case SyncOption.Custom:

					if (cConnectionPropPID != -1)
					{
						foreach (var v in newConnectionProperties)
						{
							HashSet<int> ids;
							if (currentConnectionProperties.TryGetValue(v.Key, out ids))
							{
								ids.UnionWith(v.Value);
								currentConnectionProperties[v.Key] = ids;
							}
							else
							{
								currentConnectionProperties.Add(v.Key, v.Value);
							}
						}
					}

					if (cInterfacePropPID != -1)
					{
						foreach (var v in newInterfaceProperties)
						{
							HashSet<int> ids;
							if (currentInterfaceProperties.TryGetValue(v.Key, out ids))
							{
								ids.UnionWith(v.Value);
								currentInterfaceProperties[v.Key] = ids;
							}
							else
							{
								currentInterfaceProperties.Add(v.Key, v.Value);
							}
						}
					}

					if (cConnectionsPID != -1)
					{
						foreach (var v in newConnections)
						{
							HashSet<int> ids;
							if (currentConnections.TryGetValue(v.Key, out ids))
							{
								ids.UnionWith(v.Value);
								currentConnections[v.Key] = ids;
							}
							else
							{
								currentConnections.Add(v.Key, v.Value);
							}
						}
					}

					SyncToParams();
					break;

				case SyncOption.PollingSync:
					SyncToParams();
					break;

				case SyncOption.EndOfPolling:
					RemoveDeleted();
					if (newInterfacePropID != -1)
					{
						protocol.SetParameter(newInterfacePropID, String.Empty);
					}

					if (newConnectionPropPID != -1)
					{
						protocol.SetParameter(newConnectionPropPID, String.Empty);
					}

					if (newConnectionsPID != -1)
					{
						protocol.SetParameter(newConnectionsPID, String.Empty);
					}

					if (cInterfacePropPID != -1)
					{
						PropDictionaryToBuffer(newInterfaceProperties, cInterfacePropPID);
					}

					if (cConnectionPropPID != -1)
					{
						PropDictionaryToBuffer(newConnectionProperties, cConnectionPropPID);
					}

					if (cConnectionsPID != -1)
					{
						PropDictionaryToBuffer(newConnections, cConnectionsPID);
					}

					break;
			}
		}

		private void SyncNegative(Dictionary<string, HashSet<int>> currentDic, Dictionary<string, HashSet<int>> newDic)
		{
			foreach (var v in currentDic)
			{
				foreach (var p in v.Value)
				{
					if (p < 0)
					{
						AddToPropertyDictionary(newDic, v.Key, p);
					}
				}
			}
		}

		private void SyncToParams()
		{
			if (newInterfacePropID != -1)
			{
				PropDictionaryToBuffer(newInterfaceProperties, newInterfacePropID);
			}

			if (newConnectionPropPID != -1)
			{
				PropDictionaryToBuffer(newConnectionProperties, newConnectionPropPID);
			}

			if (newConnectionsPID != -1)
			{
				PropDictionaryToBuffer(newConnections, newConnectionsPID);
			}

			if (cInterfacePropPID != -1)
			{
				PropDictionaryToBuffer(currentInterfaceProperties, cInterfacePropPID);
			}

			if (cConnectionPropPID != -1)
			{
				PropDictionaryToBuffer(currentConnectionProperties, cConnectionPropPID);
			}

			if (cConnectionsPID != -1)
			{
				PropDictionaryToBuffer(currentConnections, cConnectionsPID);
			}
		}

		#endregion Methods
	}

	/// <summary>
	/// Provide PIDs that will hold Mapping of all Connections and Properties Managed by this Element. Leaving PIDs out will create a more efficient DCFHelper Object but with limited functionality.
	/// For Example: Only defining the CurrentConnectionsPID will allow a user to Add and Remove Connections but it will not be possible to Manipulate any Properties.
	/// </summary>
	[SuppressMessage("Microsoft.StyleCop.CSharp.MaintainabilityRules", "*", Justification = "Reverse Compatibility")]
	public class DCFMappingOptions
	{
		public SyncOption HelperType = SyncOption.Custom;
		public int PIDcurrentConnectionProperties = -1;
		public int PIDcurrentConnections = -1;
		public int PIDcurrentInterfaceProperties = -1;
		public int PIDnewConnectionProperties = -1;
		public int PIDnewConnections = -1;
		public int PIDnewInterfaceProperties = -1;
	}

	[SuppressMessage("Microsoft.StyleCop.CSharp.NamingRules", "*", Justification = "Reverse Compatibility")]
	[SuppressMessage("Microsoft.StyleCop.CSharp.MaintainabilityRules", "*", Justification = "Reverse Compatibility")]
	public class DCFSaveConnectionRequest
	{
		public SaveConnectionType connectionType = SaveConnectionType.Unique_Name;
		public bool createExternalReturn = true;
		public string customFilter = String.Empty;
		public string customName;
		public ConnectivityInterface destination;
		public bool fixedConnection;
		public ConnectivityInterface source;

		public DCFSaveConnectionRequest(ConnectivityInterface source, ConnectivityInterface destination, bool fixedConnection = false)
		{
			this.fixedConnection = fixedConnection;
			this.source = source;
			this.destination = destination;
		}

		public DCFSaveConnectionRequest(ConnectivityInterface source, ConnectivityInterface destination, SaveConnectionType connectionType, bool fixedConnection = false)
			: this(source, destination, fixedConnection)
		{
			this.connectionType = connectionType;
		}

		public DCFSaveConnectionRequest(ConnectivityInterface source, ConnectivityInterface destination, string customName, bool fixedConnection = false)
			: this(source, destination, fixedConnection)
		{
			this.customName = customName;
		}

		public DCFSaveConnectionRequest(ConnectivityInterface source, ConnectivityInterface destination, string customName, string connectionFilter, bool fixedConnection = false)
			: this(source, destination, fixedConnection)
		{
			this.customName = customName;
			this.customFilter = connectionFilter;
		}

		public DCFSaveConnectionRequest(ConnectivityInterface source, ConnectivityInterface destination, SaveConnectionType connectionType, string customName, bool fixedConnection = false)
			: this(source, destination, connectionType, fixedConnection)
		{
			this.customName = customName;
		}

		public DCFSaveConnectionRequest(ConnectivityInterface source, ConnectivityInterface destination, SaveConnectionType connectionType, string customName, string connectionFilter, bool fixedConnection = false)
			: this(source, destination, connectionType, fixedConnection)
		{
			this.customName = customName;
			this.customFilter = connectionFilter;
		}

		public DCFSaveConnectionRequest(DCFHelper dcf, DCFDynamicLink source, DCFDynamicLink destination, bool fixedConnection = false)
		{
			var result = dcf.GetInterfaces(source, destination);
			if (result[0] != null)
				this.source = result[0].FirstInterface;
			if (result[1] != null)
				this.destination = result[1].FirstInterface;
			this.fixedConnection = fixedConnection;
		}

		public DCFSaveConnectionRequest(DCFHelper dcf, DCFDynamicLink source, DCFDynamicLink destination, SaveConnectionType connectionType, bool fixedConnection = false)
			: this(dcf, source, destination, fixedConnection)
		{
			this.connectionType = connectionType;
		}

		public DCFSaveConnectionRequest(DCFHelper dcf, DCFDynamicLink source, DCFDynamicLink destination, SaveConnectionType connectionType, string customName, bool fixedConnection = false)
			: this(dcf, source, destination, connectionType, fixedConnection)
		{
			this.customName = customName;
		}

		public DCFSaveConnectionRequest(DCFHelper dcf, DCFDynamicLink source, DCFDynamicLink destination, string customName, bool fixedConnection = false)
			: this(dcf, source, destination, fixedConnection)
		{
			this.customName = customName;
		}

		public DCFSaveConnectionRequest(DCFHelper dcf, DCFDynamicLink source, DCFDynamicLink destination, string customName, string connectionFilter, bool fixedConnection = false)
			: this(dcf, source, destination, fixedConnection)
		{
			this.customName = customName;
			this.customFilter = connectionFilter;
		}

		public DCFSaveConnectionRequest(DCFHelper dcf, DCFDynamicLink source, DCFDynamicLink destination, SaveConnectionType connectionType, string customName, string connectionFilter, bool fixedConnection = false)
			: this(dcf, source, destination, connectionType, fixedConnection)
		{
			this.customName = customName;
			this.customFilter = connectionFilter;
		}

		public string ConnectionFilter
		{
			get { return customFilter; }
			set { customFilter = value; }
		}
	}

	[SuppressMessage("Microsoft.StyleCop.CSharp.NamingRules", "*", Justification = "Reverse Compatibility")]
	[SuppressMessage("Microsoft.StyleCop.CSharp.MaintainabilityRules", "*", Justification = "Reverse Compatibility")]
	public class DCFSaveConnectionResult
	{
		public ConnectivityConnection destinationConnection;
		public bool internalConnection;
		public ConnectivityConnection sourceConnection;
		public bool updated;

		public DCFSaveConnectionResult(ConnectivityConnection sourceConnection, ConnectivityConnection destinationConnection, bool internalConnection, bool updated)
		{
			this.sourceConnection = sourceConnection;
			this.destinationConnection = destinationConnection;
			this.internalConnection = internalConnection;
			this.updated = updated;
		}
	}

	[SuppressMessage("Microsoft.StyleCop.CSharp.NamingRules", "*", Justification = "Reverse Compatibility")]
	[SuppressMessage("Microsoft.StyleCop.CSharp.MaintainabilityRules", "*", Justification = "Reverse Compatibility")]
	public class DVEColumn
	{
		public int columnIDX;
		public int tablePID;
		public int timeoutTime = 20;

		public DVEColumn(int tablePID, int columnIDX, int timeoutTime)
		{
			this.tablePID = tablePID;
			this.columnIDX = columnIDX;
			this.timeoutTime = timeoutTime;
		}

		public DVEColumn(int tablePID, int columnIDX)
			: this(tablePID, columnIDX, 20)
		{
		}
	}

	[SuppressMessage("Microsoft.StyleCop.CSharp.NamingRules", "*", Justification = "Reverse Compatibility")]
	[SuppressMessage("Microsoft.StyleCop.CSharp.MaintainabilityRules", "*", Justification = "Reverse Compatibility")]
	public class ExternalElement
	{
		public int dmaID; public int eleID;
		public string elementKey;
		public int timeoutTime = 20;

		public ExternalElement(int dmaID, int eleID, int timeoutTime)
		{
			this.dmaID = dmaID;
			this.eleID = eleID;
			elementKey = dmaID + "/" + eleID;
			this.timeoutTime = timeoutTime;
		}

		public ExternalElement(int dmaID, int eleID)
			: this(dmaID, eleID, 20)
		{
		}

		public ExternalElement(string elementKey)
		{
			string[] elementKeyA = elementKey.Split('/');
			if (elementKeyA.Length > 1)
			{
				Int32.TryParse(elementKeyA[0], out dmaID);
				Int32.TryParse(elementKeyA[1], out eleID);
				this.elementKey = elementKey;
			}
		}
	}

	public class FastCollection<T> : IEnumerable<T>
	{
		private Dictionary<string, ILookup<object, T>> _indexes;
		private IList<T> _items;
		private IList<Expression<Func<T, object>>> _lookups;

		public FastCollection(IList<T> data)
		{
			_items = data;
			_lookups = new List<Expression<Func<T, object>>>();
			_indexes = new Dictionary<string, ILookup<object, T>>();
		}

		public FastCollection()
		{
			_lookups = new List<Expression<Func<T, object>>>();
			_indexes = new Dictionary<string, ILookup<object, T>>();
		}

		public void Add(T item)
		{
			if (_items == null)
			{
				_items = new List<T>();
				_items.Add(item);
			}
			else
			{
				_items.Add(item);
			}

			RebuildIndexes();
		}

		public void Add(IList<T> data, IEqualityComparer<T> comparer)
		{
			if (_items == null)
			{
				_items = data;
			}
			else
			{
				_items = data.Union(_items, comparer).ToList();
			}

			RebuildIndexes();
		}

		public void AddIndex(Expression<Func<T, object>> property)
		{
			if (!_indexes.ContainsKey(property.ToString()))
			{
				_lookups.Add(property);
				_indexes.Add(property.ToString(), _items.ToLookup(property.Compile()));
			}
		}

		public IEnumerable<T> FindValue<TProperty>(Expression<Func<T, TProperty>> property, TProperty value)
		{
			var key = property.ToString();
			if (_indexes.ContainsKey(key))
			{
				return _indexes[key][value];
			}
			else
			{
				var c = property.Compile();
				return _items.Where(x => c(x).Equals(value));
			}
		}

		public IEnumerator<T> GetEnumerator()
		{
			return _items.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public void RebuildIndexes()
		{
			if (_lookups.Count > 0)
			{
				_indexes = new Dictionary<string, ILookup<object, T>>();
				foreach (var lookup in _lookups)
				{
					_indexes.Add(lookup.ToString(), _items.ToLookup(lookup.Compile()));
				}
			}
		}

		public void Remove(T item)
		{
			_items.Remove(item);
			RebuildIndexes();
		}
	}

	public class PropertyFilter
	{
		private List<PropertyFilter> and = new List<PropertyFilter>();
		private int id = -1;
		private string name = String.Empty;
		private List<PropertyFilter> or = new List<PropertyFilter>();
		private string type = String.Empty;
		private string value = String.Empty;
		////private bool not = false;

		public PropertyFilter(int id)
		{
			this.ID = id;
		}

		////public bool NOT
		////{
		////    get { return not; }
		////    set { not = value; }
		////}
		////public List<PropertyFilter> AND
		////{
		////    get { return and; }
		////    set { and = value; }
		////}
		////public List<PropertyFilter> OR
		////{
		////    get { return or; }
		////    set { or = value; }
		////}
		public PropertyFilter(string name, string type, string value)
		{
			this.Name = name;
			this.Type = type;
			this.Value = value;
		}

		public PropertyFilter(string name, string value)
		{
			this.Name = name;
			this.Value = value;
		}

		public PropertyFilter(string name)
		{
			this.Name = name;
		}

		public int ID
		{
			get { return id; }
			set { id = value; }
		}

		public string Name
		{
			get { return name; }
			set { name = value; }
		}

		public string Type
		{
			get { return type; }
			set { type = value; }
		}

		public string Value
		{
			get { return this.value; }
			set { this.value = value; }
		}
	}

	internal class CustomComparer<T> : IEqualityComparer<T>
	{
		private Func<T, object> keySelector;

		public CustomComparer(Func<T, object> keySelector)
		{
			this.keySelector = keySelector;
		}

		public bool Equals(T x, T y)
		{
			return keySelector(x).Equals(keySelector(y));
		}

		public int GetHashCode(T obj)
		{
			return keySelector(obj).GetHashCode();
		}
	}

	#endregion Classes
}