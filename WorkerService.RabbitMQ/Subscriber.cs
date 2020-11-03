﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WorkerService.RabbitMQ
{
    public class Subscriber : BackgroundService
    {
        private ILogger<Subscriber> _logger;
        private IConnection _connection;
        private IModel _channel;
        private string _consumerTag;

        public Subscriber(ILogger<Subscriber> logger)
        {
            _logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var hostName = "locahost";
            var connectionFactory = new ConnectionFactory()
            {
                Uri = new Uri($"amqp://localhost")
            };
            _connection = connectionFactory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.BasicQos(0, 1, false);
            _logger.LogInformation("Waitting for messages...");

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                var appId = Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["app_id"]);
                var messageId = Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["message_id"]);
                var contentType = Encoding.UTF8.GetString((byte[])ea.BasicProperties.Headers["content_type"]);
                _logger.LogInformation($"App Id: {appId}\t Message Id: {messageId} \t Content Type: {contentType}");

                var body = ea.Body;
                var message = Encoding.UTF8.GetString(body.ToArray());
                _logger.LogInformation($"[x] {message}");
                _channel.BasicAck(ea.DeliveryTag, multiple: false);
            };

            var queueName = "workerservice.queue";
            _consumerTag = _channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);

            _logger.LogInformation("Press [enter] to exit.");

            return Task.CompletedTask;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _channel.BasicCancel(_consumerTag);
            _channel.Close();
            _connection.Close();
            return base.StopAsync(cancellationToken);
        }
    }
}