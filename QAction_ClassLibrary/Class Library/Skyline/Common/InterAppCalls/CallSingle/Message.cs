namespace Skyline.DataMiner.Library.Common.InterAppCalls.CallSingle
{
	using Skyline.DataMiner.Library.Common;
	using Skyline.DataMiner.Library.Common.InterAppCalls.Shared;
	using Skyline.DataMiner.Library.Common.Serializing;
	using Skyline.DataMiner.Library.Common.Subscription.Waiters.InterApp;
	using Skyline.DataMiner.Net;

	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.Linq;
	using System.Reflection;
	using Skyline.DataMiner.Library.Common.InterAppCalls.MessageExecution;

	/// <summary>
	/// Represents a single command or response.
	/// </summary>
	public class Message
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Message"/> class.
		/// </summary>
		/// <remarks>Creates an instance of Message with a new GUID created using <see cref="System.Guid.NewGuid"/>.</remarks>
		public Message()
		{
			Guid = System.Guid.NewGuid().ToString();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Message"/> class using the specified GUID.
		/// </summary>
		/// <param name="guid">The GUID.</param>
		public Message(string guid)
		{
			Guid = guid;
		}

		/// <summary>
		/// Gets or sets a globally unique identifier (GUID) identifying this message.
		/// </summary>
		/// <value>A globally unique identifier (GUID) identifying this message.</value>
		public string Guid { get; set; }

		/// <summary>
		/// Gets or sets the return address.
		/// </summary>
		/// <value>The return address.</value>
		/// <remarks>The return address represents a parameter on a specific element (identified by a DataMiner Agent ID, element ID and parameter ID) which will be checked for a return message. This use of this property is optional.</remarks>
		public ReturnAddress ReturnAddress { get; set; }

		/// <summary>
		/// Gets or sets the source of this message.
		/// </summary>
		/// <value>The source of this message.</value>
		public Source Source { get; set; }

		/// <summary>
		/// Retrieves the executor of this message using custom mapping. This contains all logic to perform when receiving this message.
		/// </summary>
		/// <param name="messageToExecutorMapping">A mapping to link message type with the right execution type.</param>
		/// <returns>The executor holding all logic for processing this message.</returns>
		/// <exception cref="AmbiguousMatchException">Unable to find executor for this type of message.</exception>
		public IMessageExecutor CreateExecutor(IDictionary<Type, Type> messageToExecutorMapping)
		{
			return MessageExecutorFactory.CreateExecutor(this, messageToExecutorMapping);
		}

		/// <summary>
		/// Retrieves the executor of this message using custom mapping. This contains all logic to perform when receiving this message.
		/// </summary>
		/// <param name="messageToExecutorMapping">A mapping to link message type with the right execution type.</param>
		/// <returns>The executor holding all logic for processing this message.</returns>
		/// <exception cref="AmbiguousMatchException">Unable to find executor for this type of message.</exception>
		public IBaseMessageExecutor CreateBaseExecutor(IDictionary<Type, Type> messageToExecutorMapping)
		{
			return MessageExecutorFactory.CreateBaseExecutor(this, messageToExecutorMapping);
		}

		/// <summary>
		/// Tries to get the Executor and run the default execution flow.
		/// </summary>
		/// <param name="dataSource">A source used during DataGets, usually SLProtocol or Engine.</param>
		/// <param name="dataDestination">A destination used during DataSets, usually SLProtocol or Engine.</param>
		/// <param name="messageToExecutorMapping">A mapping to link message type with the right execution type.</param>
		/// <param name="optionalReturnMessage">The return message that might get created.</param>
		/// <returns>A boolean to indicate if the execution was successful.</returns>
		/// <exception cref="AmbiguousMatchException">Unable to find executor for this type of message.</exception>
		public bool TryExecute(object dataSource, object dataDestination, IDictionary<Type, Type> messageToExecutorMapping, out Message optionalReturnMessage)
		{
			var executor = CreateBaseExecutor(messageToExecutorMapping);
			return DefaultBaseExecuteFlow(dataSource, dataDestination, out optionalReturnMessage, executor);
		}

		/// <summary>
		/// Sends a message as a reply to this message using a default serializer and SLNet communication to the specified return address DataMiner Agent ID/element ID/parameter ID of the current message without waiting on a reply.
		/// </summary>
		/// <param name="connection">The SLNet connection to use.</param>
		/// <param name="replyMessage">The message to reply with.</param>
		/// <param name="knownTypes">A list of all the possible Message classes, necessary for the default background serializer.</param>
		public void Reply(IConnection connection, Message replyMessage, List<Type> knownTypes)
		{
			var defaultSerializer = SerializerFactory.CreateInterAppSerializer(typeof(Message), knownTypes);
			Reply(connection, replyMessage, defaultSerializer);
		}

		/// <summary>
		/// Sends a message as a reply to this message using SLNet communication to the specified return address DataMiner Agent ID/element ID/parameter ID of the current message without waiting on a reply.
		/// </summary>
		/// <param name="connection">The SLNet connection to use.</param>
		/// <param name="replyMessage">The message to reply with.</param>
		/// <param name="serializer">A custom serializer.</param>
		public void Reply(IConnection connection, Message replyMessage, ISerializer serializer)
		{
			replyMessage.Guid = Guid;
			Send(connection, ReturnAddress.AgentId, ReturnAddress.ElementId, ReturnAddress.ParameterId, serializer);
		}

		/// <summary>
		/// Sends a message as a reply to this message using a default serializer and SLNet communication to the specified return address DataMiner Agent ID/element ID/parameter ID of the current message and waits on a reply.
		/// </summary>
		/// <param name="connection">The SLNet connection to use.</param>
		/// <param name="replyMessage">The message to reply with.</param>
		/// <param name="timeout">The maximum time to wait between each reply. Wait time resets each time a reply is received.</param>
		/// <param name="knownTypes">A list of all the possible Message classes, necessary for the default background serializer.</param>
		public Message Reply(IConnection connection, Message replyMessage, TimeSpan timeout, List<Type> knownTypes)
		{
			var defaultSerializer = SerializerFactory.CreateInterAppSerializer(typeof(Message), knownTypes);
			return Reply(connection, replyMessage, timeout, defaultSerializer);
		}

		/// <summary>
		/// Sends a message as a reply to this message using SLNet communication to the specified return address DataMiner Agent ID/element ID/parameter ID of the current message and waits on a reply.
		/// </summary>
		/// <param name="connection">The SLNet connection to use.</param>
		/// <param name="replyMessage">The message to reply with.</param>
		/// <param name="timeout">The maximum time to wait between each reply. Wait time resets each time a reply is received.</param>
		/// <param name="serializer">A custom serializer.</param>
		public Message Reply(IConnection connection, Message replyMessage, TimeSpan timeout, ISerializer serializer)
		{
			replyMessage.Guid = Guid;
			return Send(connection, ReturnAddress.AgentId, ReturnAddress.ElementId, ReturnAddress.ParameterId, timeout, serializer);
		}

		/// <summary>
		/// Sends this message using a default serializer and SLNet communication to a specific DataMiner Agent ID/element ID/parameter ID without waiting on a reply.
		/// </summary>
		/// <param name="connection">The SLNet connection to use.</param>
		/// <param name="agentId">The DataMiner Agent ID of the destination.</param>
		/// <param name="elementId">The element ID of the destination.</param>
		/// <param name="parameterId">The parameter ID of the destination.</param>
		/// <param name="knownTypes">A list of all the possible Message classes, necessary for the default background serializer.</param>
		public void Send(IConnection connection, int agentId, int elementId, int parameterId, List<Type> knownTypes)
		{
			var defaultSerializer = SerializerFactory.CreateInterAppSerializer(typeof(Message), knownTypes);
			Send(connection, agentId, elementId, parameterId, defaultSerializer);
		}

		/// <summary>
		/// Sends this message using SLNet communication to a specific DataMiner Agent ID/element ID/parameter ID without waiting on a reply.
		/// </summary>
		/// <param name="connection">The SLNet connection to use.</param>
		/// <param name="agentId">The DataMiner Agent ID of the destination.</param>
		/// <param name="elementId">The element ID of the destination.</param>
		/// <param name="parameterId">The parameter ID of the destination.</param>
		/// <param name="serializer">A custom serializer.</param>
		public void Send(IConnection connection, int agentId, int elementId, int parameterId, ISerializer serializer)
		{
			var destination = new DmsElementId(agentId, elementId);
			IDms thisDma = connection.GetDms();
			var element = thisDma.GetElement(destination);
			var parameter = element.GetStandaloneParameter<string>(parameterId);
			Stopwatch sw = new Stopwatch();
			sw.Start();
		
			string value = Serialize(serializer);
			System.Diagnostics.Debug.WriteLine("CLP - InterApp - Serialized: " + sw.ElapsedMilliseconds + " ms");
			sw.Restart();
			parameter.SetValue(value);
			System.Diagnostics.Debug.WriteLine("CLP - InterApp - Value Set to external pid: " + sw.ElapsedMilliseconds + " ms");
		}

		/// <summary>
		/// Sends this message using SLNet communication to a specific DataMiner Agent ID/element ID/parameter ID and waits on a reply.
		/// </summary>
		/// <param name="connection">The SLNet connection to use.</param>
		/// <param name="agentId">The DataMiner Agent ID of the destination.</param>
		/// <param name="elementId">The element ID of the destination.</param>
		/// <param name="parameterId">The parameter ID of the destination.</param>
		/// <param name="timeout">The maximum time to wait between each reply. Wait time resets each time a reply is received.</param>
		/// <param name="knownTypes">A list of all the possible Message classes, necessary for the default background serializer.</param>
		/// <returns>The reply response to this command.</returns>
		public Message Send(IConnection connection, int agentId, int elementId, int parameterId, TimeSpan timeout, List<Type>knownTypes)
		{
			var defaultSerializer = SerializerFactory.CreateInterAppSerializer(typeof(Message), knownTypes);
			return this.Send(connection, agentId, elementId, parameterId, timeout, defaultSerializer);
		}

		/// <summary>
		/// Sends this message using SLNet communication to a specific DataMiner Agent ID/element ID/parameter ID and waits on a reply.
		/// </summary>
		/// <param name="connection">The SLNet connection to use.</param>
		/// <param name="agentId">The DataMiner Agent ID of the destination.</param>
		/// <param name="elementId">The element ID of the destination.</param>
		/// <param name="parameterId">The parameter ID of the destination.</param>
		/// <param name="timeout">The maximum time to wait between each reply. Wait time resets each time a reply is received.</param>
		/// <param name="serializer">A custom serializer that can be used in the background.</param>
		/// <returns>The reply response to this command.</returns>
		public Message Send(IConnection connection, int agentId, int elementId, int parameterId, TimeSpan timeout, ISerializer serializer)
		{
			if (ReturnAddress != null)
			{
				Stopwatch sw = new Stopwatch();
				sw.Start();
				using (MessageWaiter waiter = new MessageWaiter(new ConnectionCommunication(connection), null, serializer, this))
				{
					System.Diagnostics.Debug.WriteLine("CLP - InterApp - Creation of MessageWait: " + sw.ElapsedMilliseconds + " ms");
					sw.Restart();
					Send(connection, agentId, elementId, parameterId, serializer);
					System.Diagnostics.Debug.WriteLine("CLP - InterApp - Sent: " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
					System.Diagnostics.Debug.WriteLine("CLP - InterApp - Sending of message: " + sw.ElapsedMilliseconds + " ms");
					sw.Restart();
					return waiter.WaitNext(timeout).First();
				}
			}

			return null;

		}

		/// <summary>
		/// Serializes this object using the internal ISerializer.
		/// </summary>
		/// <returns>The serialized string of this object.</returns>
		private string Serialize(ISerializer serializer)
		{
			return serializer.SerializeToString(this);
		}

		private static bool DefaultBaseExecuteFlow(object dataSource, object dataDestination, out Message optionalReturnMessage, IBaseMessageExecutor executor)
		{
			if (executor is IMessageExecutor)
			{
				return DefaultExecuteFlow(dataSource, dataDestination, out optionalReturnMessage, (IMessageExecutor)executor);
			}
			else if (executor is ISimpleMessageExecutor)
			{
				return ((ISimpleMessageExecutor)executor).TryExecute(dataSource, dataDestination, out optionalReturnMessage);
			}

			optionalReturnMessage = default(Message);
			return false;
		}

		private static bool DefaultExecuteFlow(object dataSource, object dataDestination, out Message optionalReturnMessage, IMessageExecutor executor)
		{
			executor.DataGets(dataSource);
			executor.Parse();
			bool result = executor.Validate();
			if (result)
			{
				executor.Modify();
				executor.DataSets(dataDestination);
			}

			optionalReturnMessage = executor.CreateReturnMessage();

			return result;
		}
	}
}