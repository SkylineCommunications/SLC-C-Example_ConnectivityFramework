namespace Skyline.DataMiner.Library.Protocol.Snmp.Rates
{
	using System;
	using System.Collections.Generic;

	using Skyline.DataMiner.Scripting;

	/// <summary>
	/// Allows retrieving delta time between 2 consecutive executions of an SNMP group.
	/// </summary>
	public class SnmpDeltaHelper
	{
		private readonly SLProtocol protocol;
		private readonly int groupId;

		private readonly CalculationMethod calculationMethod;

		private bool deltaLoaded;
		private TimeSpan delta;
		private readonly Dictionary<string, TimeSpan> deltaPerInstance = new Dictionary<string, TimeSpan>();

		/// <summary>
		/// Initializes a new instance of the <see cref="SnmpDeltaHelper"/> class.<br/>
		/// Such instance is used to retrieve delta time between 2 consecutive executions of an SNMP group.
		/// </summary>
		/// <param name="protocol">Link with SLProtocol process.</param>
		/// <param name="groupId">The ID of the protocol group on which delta values are required.</param>
		/// <param name="calculationMethodPid">The PID of the parameter allowing the user to choose between 'Fast' and 'Accurate' delta calculation methods.</param>
		public SnmpDeltaHelper(SLProtocol protocol, int groupId, uint calculationMethodPid)
		{
			if (groupId < 0)
			{
				throw new ArgumentException("The group ID must not be negative.", nameof(groupId));
			}

			this.protocol = protocol;
			this.groupId = groupId;

			calculationMethod = (CalculationMethod)Convert.ToInt32(protocol.GetParameter((int)calculationMethodPid));
			if (calculationMethod != CalculationMethod.Accurate && calculationMethod != CalculationMethod.Fast)
			{
				throw new NotSupportedException("Unexpected SNMP Rate Calculation Method value '" + protocol.GetParameter((int)calculationMethodPid) + "'" +
					" retrieved from Param with PID '" + calculationMethodPid + "'.");
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SnmpDeltaHelper"/> class.<br/>
		/// Such instance is used to retrieve delta time between 2 consecutive executions of an SNMP group.
		/// </summary>
		/// <param name="protocol">Link with SLProtocol process.</param>
		/// <param name="groupId">The ID of the protocol group on which delta values are required.</param>
		/// <param name="calculationMethod">Allows to define which delta calculation methods should be used.<br/>
		/// Note that opting for the 'Accurate' method only makes sense for SNMP tables.</param>
		public SnmpDeltaHelper(SLProtocol protocol, int groupId, CalculationMethod calculationMethod = CalculationMethod.Fast)
		{
			if (groupId < 0)
			{
				throw new ArgumentException("The group ID is negative.", nameof(groupId));
			}

			this.protocol = protocol;
			this.groupId = groupId;
			this.calculationMethod = calculationMethod;
		}

		/// <summary>
		/// Configures DataMiner to use the expected delta calculation method.
		/// </summary>
		/// <param name="protocol">Link with SLProtocol process</param>
		/// <param name="groupId">The ID of the protocol group for which the delta tracking method should be changed.</param>
		/// <param name="calculationMethod">Allows to define which delta calculation methods should be used.<br/>
		/// Note that opting for the 'Accurate' method only makes sense for SNMP tables.</param>
		public static void UpdateRateDeltaTracking(SLProtocol protocol, int groupId, CalculationMethod calculationMethod)
		{
			if (groupId < 0)
			{
				throw new ArgumentException("The group ID is negative.", nameof(groupId));
			}

			switch (calculationMethod)
			{
				case CalculationMethod.Fast:
					protocol.NotifyProtocol(/*NT_SET_BITRATE_DELTA_INDEX_TRACKING*/ 448, groupId, false);
					break;
				case CalculationMethod.Accurate:
					protocol.NotifyProtocol(/*NT_SET_BITRATE_DELTA_INDEX_TRACKING*/ 448, groupId, true);
					break;
				default:
					throw new ArgumentException("The calculationMethod is unknown.", nameof(calculationMethod));
			}
		}

		/// <summary>
		/// Retrieves the delta.
		/// </summary>
		/// <param name="rowKey">In case the delta calculation method is set to 'Accurate', allows to define the Primary Key of the table row for which a delta is required.</param>
		/// <returns><see cref="System.TimeSpan"/> elapsed between the last 2 executions of a given SNMP group.</returns>
		internal TimeSpan? GetDelta(string rowKey)
		{
			if (!deltaLoaded)
			{
				LoadDelta();
			}

			// Based on SNMP standalone
			if (rowKey == null)
			{
				return delta;
			}

			// Based on SNMP column
			switch (calculationMethod)
			{
				case CalculationMethod.Fast:
					return delta;
				case CalculationMethod.Accurate:
					return deltaPerInstance.ContainsKey(rowKey) ? deltaPerInstance[rowKey] : delta;
				default:
					return null;
			}
		}

		private void LoadDelta()
		{
			switch (calculationMethod)
			{
				case CalculationMethod.Fast:
					LoadFastDelta();
					break;
				case CalculationMethod.Accurate:
					LoadAccurateDeltaValues();
					break;
				default:
					// Unknown calculation method, do nothing (already handled in constructor)
					break;
			}

			deltaLoaded = true;
		}

		private void LoadFastDelta()
		{
			int deltaInMilliseconds = Convert.ToInt32(protocol.NotifyProtocol(269 /*NT_GET_BITRATE_DELTA*/, groupId, null));
			delta = TimeSpan.FromMilliseconds(deltaInMilliseconds);
			////protocol.Log("QA" + protocol.QActionID + "|LoadFastDelta|deltaInMilliseconds '" + deltaInMilliseconds + "' - delta '" + delta + "'", LogType.DebugInfo, LogLevel.NoLogging);
		}

		private void LoadAccurateDeltaValues()
		{
			object deltaRaw = protocol.NotifyProtocol(269 /*NT_GET_BITRATE_DELTA*/, groupId, "");
			switch (deltaRaw)
			{
				case int deltaInMilliseconds:
					// In case of timeout, a single delta is returned.
					delta = TimeSpan.FromMilliseconds(deltaInMilliseconds);
					////protocol.Log("QA" + protocol.QActionID + "|LoadAccurateDeltaValues|deltaInMilliseconds '" + deltaInMilliseconds + "' - delta '" + delta + "'", LogType.DebugInfo, LogLevel.NoLogging);

					foreach (var key in deltaPerInstance.Keys)
					{
						deltaPerInstance[key] = delta;
					}

					break;
				case object[] deltaValues:
					// In case of successful group execution, a delta per instance is returned.
					for (int i = 0; i < deltaValues.Length; i++)
					{
						if (!(deltaValues[i] is object[] deltaKeyAndValue) || deltaKeyAndValue.Length != 2)
						{
							protocol.Log("QA" + protocol.QActionID + "|LoadSnmpGroupExecutionAccurateDeltas|Unexpected format for deltaValues[" + i + "]", LogType.Error, LogLevel.NoLogging);
							continue;
						}

						string deltaKey = Convert.ToString(deltaKeyAndValue[0]);
						int deltaInMilliseconds = Convert.ToInt32(deltaKeyAndValue[1]);

						deltaPerInstance[deltaKey] = TimeSpan.FromMilliseconds(deltaInMilliseconds);
						////protocol.Log("QA" + protocol.QActionID + "|LoadAccurateDeltaValues|deltaKey '" + deltaKey + "' " +
						////	"- deltaInMilliseconds '" + deltaInMilliseconds + "' " +
						////	"- delta '" + deltaPerInstance[deltaKey] + "'", LogType.DebugInfo, LogLevel.NoLogging);
					}

					break;
				default:
					protocol.Log("QA" + protocol.QActionID + "|LoadSnmpGroupExecutionAccurateDeltas|Unexpected format returned by NT_GET_BITRATE_DELTA.", LogType.Error, LogLevel.NoLogging);
					break;
			}
		}
	}
}
