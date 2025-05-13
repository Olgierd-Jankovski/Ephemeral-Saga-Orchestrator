using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Contracts;

namespace Contracts;

public class Common
{
    public record OrderCreatedEvent
    {
        public int OrderId { get; init; }
        public string SagaId { get; init; } = "";
        public DateTime Timestamp { get; init; }
    }

    public record OrderCancelledEvent
    {
        public int OrderId { get; init; }
        public string SagaId { get; init; } = "";
        public DateTime Timestamp { get; init; }
    }

    // now with the inventory
    public record InventoryCreatedEvent
    {
        public int InventoryId { get; init; }
        public string SagaId { get; init; } = "";
        public DateTime Timestamp { get; init; }
    }

    public record InventoryCancelledEvent
    {
        public int InventoryId { get; init; }
        public string SagaId { get; init; } = "";
        public DateTime Timestamp { get; init; }
    }

    public interface IEventBus
    {
        Task PublishAsync<T>(T domainEvent);
        void Subscribe<T>(IEventConsumer<T> consumer);
    }

    public interface IEventConsumer<T>
    {
        Task ConsumeAsync(T domainEvent);
    }

    public class RabbitMQEventBus : IEventBus, IDisposable
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _exchangeName = "saga_events";
        private readonly ConcurrentDictionary<string, object> _subscriptions = new();

        public RabbitMQEventBus(string rabbitMqHostName = "localhost")
        {
            var factory = new ConnectionFactory() { HostName = rabbitMqHostName };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare a durable topic exchange for events
            _channel.ExchangeDeclare(exchange: _exchangeName, type: ExchangeType.Topic, durable: true);
        }

        public async Task PublishAsync<T>(T domainEvent)
        {
            // Use the type name as routing key (e.g. "OrderCreatedEvent")
            var routingKey = typeof(T).Name;
            var message = JsonSerializer.Serialize(domainEvent);
            var body = Encoding.UTF8.GetBytes(message);

            _channel.BasicPublish(
                exchange: _exchangeName,
                routingKey: routingKey,
                basicProperties: null,
                body: body);

            await Task.CompletedTask;
        }

        public void Subscribe<T>(IEventConsumer<T> consumer)
        {
            var routingKey = typeof(T).Name;

            // Create a unique (temporary) queue for the subscriber
            var queueName = _channel.QueueDeclare().QueueName;
            _channel.QueueBind(queue: queueName, exchange: _exchangeName, routingKey: routingKey);

            var eventingConsumer = new EventingBasicConsumer(_channel);
            eventingConsumer.Received += async (model, ea) =>
            {
                try
                {
                    var body = ea.Body.ToArray();
                    var message = Encoding.UTF8.GetString(body);
                    var domainEvent = JsonSerializer.Deserialize<T>(message);
                    if (domainEvent != null)
                    {
                        await consumer.ConsumeAsync(domainEvent);
                    }
                    // Acknowledge processing
                    _channel.BasicAck(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing event {routingKey}: {ex.Message}");
                    // Optionally use a negative acknowledgement or log the error
                }
            };

            _channel.BasicConsume(queue: queueName, autoAck: false, consumer: eventingConsumer);
            _subscriptions[routingKey] = consumer;
        }

        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}

