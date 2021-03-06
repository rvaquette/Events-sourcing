﻿using System;
using System.Text;
using Payments.Models;
using RabbitMQ.Client;
/*
    RabbitMQ message broker where the API layer will post messages for processing
 */
namespace Payments.RabbitMQ
{
    public class RabbitMQDirectClient
    {
        private IConnection _connection;
        private IModel _channel;
        private string _replyQueueName;
        private QueueingBasicConsumer _consumer;

        public void CreateConnection()
        {
            var factory = new ConnectionFactory { HostName = "localhost", UserName = "guest", Password = "guest" };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _replyQueueName = _channel.QueueDeclare(queue: "rpc_reply", durable: true, exclusive: false,
                autoDelete: false, arguments: null);

            _consumer = new QueueingBasicConsumer(_channel);

            //noAck: false => consumer will acknowledge when it finishes processing
            _channel.BasicConsume(queue: _replyQueueName, noAck: true, consumer: _consumer);
        }

        public void Close()
        {
            _connection.Close();
        }
        /*
         The client application posts messages directly onto a queue. 
         For each message that gets posted, the application waits for a reply from a reply queue. 
         This essentially makes this a synchronous process. 
         */

        public string MakePayment(CardPayment payment)
        {
            //we need a guid which is global unique identifier the consumer application
            //will put that same correlation ID onto the reply
            //i.e. assure that we're dealing with the correct reply by checking the ID.
            var corrId = Guid.NewGuid().ToString();
            var props = _channel.CreateBasicProperties();
            props.ReplyTo = _replyQueueName;

            /*
             When a message is posted to the server from the client, 
            a correlation ID is generated and attached to the message properties. 
            The same correlation ID is put onto the properties in a reply message. 
            This is  useful, as it allows you to easily tie together the replies in 
            the originating messages if you store them for retrieval later.
            The client posts a message to the RPC queue that has a correlation ID of e.g. 12345. 
            This message is received by the server and a reply is sent back to the client 
            on a reply queue with the same correlation ID of e.g 12345. 
            */
            props.CorrelationId = corrId;

            //create the RPC queue and bind it to the default exchange with routingKey (rpc_queue)
            //also pass our properties into basic publish along with a serialized card payment message
            _channel.BasicPublish(exchange: "", routingKey: "rpc_queue", basicProperties: props, body: payment.Serialize());


            //just sitting and wait for a response from the direct card payment consumer
            while (true) //run an infinite loop to listen to messages in the queue
            {
                //de-queue the message
                var ea = _consumer.Queue.Dequeue();

                //check that the correlation ID from the message coming back is the same
                //as the ID we stored earlier
                if (ea.BasicProperties.CorrelationId != corrId) continue;

                var authCode = Encoding.UTF8.GetString(ea.Body);
                return authCode;
            }
        }
    }
}
