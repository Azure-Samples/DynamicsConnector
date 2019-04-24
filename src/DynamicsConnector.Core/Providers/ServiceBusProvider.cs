using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;

namespace DynamicsConnector.Core.Providers
{
    public class ServiceBusProvider : IDataDestinationProvider
    {
        public async Task PostDataAsync<T>(List<T> data, params string[] parameters)
        {
            try
            {
                var client = GetClient(parameters);

                foreach (var dataItem in data)
                {
                    var serializedMessage = JsonConvert.SerializeObject(dataItem);
                    var payloadStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedMessage));

                    var msg = new BrokeredMessage(payloadStream, true);

                    if (client is TopicClient topicClient)
                    {
                        msg.Properties["Status"] = parameters[3];
                        await topicClient.SendAsync(msg);
                    }
                    else
                    if (client is QueueClient queueClient)
                    {
                        queueClient.Send(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task PostDataAsync<T>(T dataItem, params string[] parameters)
        {
            try
            {
                var client = GetClient(parameters);

                var serializedMessage = JsonConvert.SerializeObject(dataItem);
                var payloadStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedMessage));

                var msg = new BrokeredMessage(payloadStream, true);

                if (client is TopicClient topicClient)
                {
                    msg.Properties["Status"] = parameters[3];
                    await topicClient.SendAsync(msg);
                }
                else
                if (client is QueueClient queueClient)
                {
                    queueClient.Send(msg);
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private ClientEntity GetClient(string[] parameters)
        {
            if (parameters.Length != 4)
                throw new ArgumentException("Wrong Parameters Count");

            string cs = parameters[0], topic = parameters[1], queue = parameters[2];

            if (string.IsNullOrEmpty(topic))
                return QueueClient.CreateFromConnectionString(cs, queue);

            return TopicClient.CreateFromConnectionString(cs, topic);
        }


    }
}
