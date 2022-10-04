namespace Skyline.DataMiner.Library.Common.InterAppCalls.MessageExecution
{
	using CallSingle;

	/// <summary>
	/// Represents an executor for messages. Command pattern: simple Try and Execute method with optional out type return message.
	/// </summary>
	public interface ISimpleMessageExecutor : IBaseMessageExecutor
	{
		/// <summary>
		/// Performs all the actions regarding the execution logic of the incoming message.
		/// And optionally returns a message the be sent back as a response message.
		/// </summary>
		/// <param name="dataSource">SLProtocol, Engine, or other data sources.</param>
		/// <param name="dataDestination">SLProtocol, Engine, or another data destination.</param>
		/// <param name="optionalReturnMessage">A message representing the response for the processed message.</param>
		/// <returns>A boolean indicating if the received data is valid.</returns>
		bool TryExecute(object dataSource, object dataDestination, out Message optionalReturnMessage);
	}
}