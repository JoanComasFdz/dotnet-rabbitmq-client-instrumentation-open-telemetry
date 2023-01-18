using System.Diagnostics;
using RabbitMQ.Client;

namespace InstrumentedRabbitMqDotNetClient.Instrumentation
{
    /// <summary>
    /// <para>
    /// Adds methods to create instrumentation events so that other frameworks can act on the actions here performed.
    /// </para>
    /// </summary>
    internal interface IRabbitMQDiagnosticSource
    {
        /// <summary>
        /// Creates an Start activity and tells a diagnostic listener to start it.
        /// </summary>
        /// <param name="props">The basic properties instance coming from the RabbitMQ connection.</param>
        /// <param name="eventName">The name (or routingKey) of the event being published.</param>
        /// <param name="payload">The data to be sent (or event content).</param>
        /// <returns>The activity already started.</returns>
        Activity StartSend(IBasicProperties props, string eventName, string payload);

        /// <summary>
        /// Creates a Process activity and tells the diagnostic listener to start it.
        /// </summary>
        /// <param name="props">The basic properties instance coming from the RabbitMQ connection.</param>
        /// <param name="eventName">The name (or routingKey) of the event being processed.</param>
        /// <returns></returns>
        Activity StartProcess(IBasicProperties props, string eventName);

        /// <summary>
        /// <para>
        /// T ells the diagnostic listener to stop an activity and disposes the activity.
        /// </para>
        /// <para>
        /// Use the methods <see cref="StartSend"/> and <see cref="StartProcess"/> to create an activity. Process your actions. Then call this method
        /// with that same activity.
        /// </para>
        /// </summary>
        /// <param name="activity">The activity to be stopped.</param>
        /// <param name="failure">Indicates whether there was a failure while processing your actions.</param>
        void Stop(Activity activity, bool failure = false);
    }
}