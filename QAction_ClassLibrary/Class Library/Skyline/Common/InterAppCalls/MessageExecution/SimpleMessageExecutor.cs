namespace Skyline.DataMiner.Library.Common.InterAppCalls.MessageExecution
{
	using Skyline.DataMiner.Library.Common.InterAppCalls.CallSingle;

	/// <summary>
	/// Represents a message executor for a specific provided message. There may only be one executor per message type.
	/// </summary>
	/// <typeparam name="T">The message type.</typeparam>
	public abstract class SimpleMessageExecutor<T> : ISimpleMessageExecutor
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SimpleMessageExecutor{T}"/> class using the specified message.
		/// </summary>
		/// <param name="message">The message</param>
		protected SimpleMessageExecutor(T message)
		{
			Message = message;
		}

		/// <summary>
		/// Gets the message to execute.
		/// </summary>
		public T Message { get; private set; }

		/// <inheritdoc />
		public abstract bool TryExecute(object dataSource, object dataDestination, out Message optionalReturnMessage);
	}
}