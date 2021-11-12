using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Newtonsoft.Json;

namespace DynamicsConnector.Core.Providers
{
    public class ServiceBusProvider : IDataDestinationProvider
    {
        public async Task PostDataAsync<T>(List<T> data, params string[] parameters)
        {
            try
            {
                foreach (var dataItem in data)
                {
                    var serializedMessage = JsonConvert.SerializeObject(dataItem);
                    var payloadStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedMessage));
                    string cs = parameters[0], topic = parameters[1], queue = parameters[2];

                    var client = new ServiceBusClient(cs);
                    var msg = new ServiceBusMessage(payloadStream.ToString());
                    if (parameters.Length != 4)
                    {
                        throw new ArgumentException("Wrong Parameters Count");
                    }
                    else if (string.IsNullOrEmpty(topic))
                    {
                        ServiceBusSender queueSender = client.CreateSender(queue);
                        msg.ApplicationProperties["Status"] = parameters[3];
                        await queueSender.SendMessageAsync(msg);
                    }
                    else
                    {
                        ServiceBusSender topicSender = client.CreateSender(topic);
                        await topicSender.SendMessageAsync(msg);
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
                var serializedMessage = JsonConvert.SerializeObject(dataItem);
                var payloadStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedMessage));
                string cs = parameters[0], topic = parameters[1], queue = parameters[2];
                var client = new ServiceBusClient(cs);
                var msg = new ServiceBusMessage(payloadStream.ToString());
                if (parameters.Length != 4)
                {
                    throw new ArgumentException("Wrong Parameters Count");
                }
                else if (string.IsNullOrEmpty(topic))
                {
                    ServiceBusSender queueSender = client.CreateSender(queue);
                    msg.ApplicationProperties["Status"] = parameters[3];
                    await queueSender.SendMessageAsync(msg);
                }
                else
                {
                    ServiceBusSender topicSender = client.CreateSender(topic);
                    await topicSender.SendMessageAsync(msg);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        


    }
}
