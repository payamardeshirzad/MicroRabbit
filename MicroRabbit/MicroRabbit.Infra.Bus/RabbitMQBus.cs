using MediatR;
using MicroRabbit.Domain.Core.Bus;
using MicroRabbit.Domain.Core.Commands;
using MicroRabbit.Domain.Core.Events;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MicroRabbit.Infra.Bus
{
    // prevent from inheritance
    public sealed class RabbitMQBus : IEventBus
    {
        private readonly IMediator _mediator;
        private readonly Dictionary<string, List<Type>> _handlers;
        private readonly List<Type> _eventTypes;

        public RabbitMQBus(IMediator mediator)
        {
            _mediator = mediator;
            _handlers = new Dictionary<string, List<Type>>();
            _eventTypes = new List<Type>();
        }
        public Task SendCommand<T>(T command) where T : Command
        {
            return _mediator.Send(command);
        }
        /// <summary>
        /// RabbitMQ.Client is added to the project
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="event"></param>
        public void Publish<T>(T @event) where T : Event
        {
            var factory = new ConnectionFactory() { HostName= "localhost"};
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                // get the event name using reflection
                var eventName = @event.GetType().Name;

                // declare a queue
                channel.QueueDeclare(eventName, false, false, false, null);
                // message, or let's say our event we want to publish
                var message = JsonConvert.SerializeObject(@event);
                // Encode the message into a body
                var body = Encoding.UTF8.GetBytes(message);
                channel.BasicPublish("", eventName, null, body);
            }

        }

        public void Subscribe<T, TH>()
            where T : Event
            where TH : IEventHandler<T>
        {
            var eventName = typeof(T).Name;
            var handlerType = typeof(TH);

            // check to see event type list contains that type of event
            if (!_eventTypes.Contains(typeof(T)))
            {
                _eventTypes.Add(typeof(T));
            }

            // check in the dictionary if the event name exists in it
            if (!_handlers.ContainsKey(eventName))
            {
                _handlers.Add(eventName, new List<Type>());
            }

            // Do a validation on handlers list. If an event is there and is listed, no need to add it for second time
            if (_handlers[eventName].Any(s=> s.GetType() == handlerType))
            {
                throw new ArgumentException(
                    $"Handler type {handlerType} already is registered for '{eventName}'", 
                    nameof(handlerType));
            }

            // assign the handler and add it to the list of types
            _handlers[eventName].Add(handlerType);

            StartBasicConsume<T>();
        }

        private void StartBasicConsume<T>() where T : Event
        {
            // This one will be async
            var factory = new ConnectionFactory() { HostName = "localhost", DispatchConsumersAsync = true };
            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            var eventName = typeof(T).Name;
            channel.QueueDeclare(eventName, false, false, false, null);
            var consumer = new AsyncEventingBasicConsumer(channel);
            // create a delegate. As soon as a message comes into the queue Consumer_Received will kick off.
            // basically it is listening to our messages in the queue
            consumer.Received += Consumer_Received;
            channel.BasicConsume(eventName, true, consumer);
        }

        private async Task Consumer_Received(object sender, BasicDeliverEventArgs e)
        {
            // get all the information about the message which is sitting in our queue
            var eventName = e.RoutingKey;
            var message = Encoding.UTF8.GetString(e.Body);
            try
            {
                // ProcessEvent will know which handler is subscribed using reflection and activators dynamically
                await ProcessEvent(eventName, message).ConfigureAwait(false);

            }
            catch (Exception ex)
            {

            }
        }

        private async Task ProcessEvent(string eventName, string message)
        {
            // check the dictionary of handlers to see if it contains the key
            if(_handlers.ContainsKey(eventName))
            {
                // we have the dictionary of types based on the eventnames
                var subscriptions = _handlers[eventName];
                foreach( var subscription in subscriptions)
                {
                    //construct the handler dynamically. We create an instance of this type, let's say subscription
                    var handler = Activator.CreateInstance(subscription);
                    if (handler == null) continue;
                    var eventType = _eventTypes.SingleOrDefault(t => t.Name == eventName);
                    var @event = JsonConvert.DeserializeObject(message, eventType);
                    // concreteType is the actual object
                    var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);
                    // This will use generics to kick off the handle method inside of our handler
                    // and passes it the @event. This line does the main work of routing to the right handler
                    // in all of our microservices
                    await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { @event });

                }
            }
        }
    }
}
