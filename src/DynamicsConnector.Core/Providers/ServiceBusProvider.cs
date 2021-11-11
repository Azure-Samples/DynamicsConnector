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
                var client = GetClient(parameters);

                foreach (var dataItem in data)
                {
                    var serializedMessage = JsonConvert.SerializeObject(dataItem);
                    var payloadStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedMessage));
                    var msg = new ServiceBusMessage(payloadStream.ToString());
                    if (client is ServiceBusSender serviceBusSender)
                    {
                        msg.ApplicationProperties["Status"] = parameters[3];
                        await serviceBusSender.SendMessageAsync(msg);
                    }
                    else
                    if (client is ServiceBusSender serviceBussender)
                    {
                        serviceBussender.SendMessageAsync(msg);
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

                var msg = new ServiceBusMessage(payloadStream.ToString());

                if (client is ServiceBusSender serviceBusSender)
                {
                    msg.ApplicationProperties["Status"] = parameters[3];
                    await serviceBusSender.SendMessageAsync(msg);
                }
                else
                if (client is ServiceBusSender serviceBussender)
                {
                    serviceBussender.SendMessageAsync(msg);
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private ServiceBusSender GetClient(string[] parameters)
        {
            if (parameters.Length != 4)
                throw new ArgumentException("Wrong Parameters Count");

            string cs = parameters[0], topic = parameters[1], queue = parameters[2];

            var client = new ServiceBusClient(cs);
            ServiceBusSender senderqueue = client.CreateSender(queue);
            ServiceBusSender sendertopic = client.CreateSender(topic);
            if (string.IsNullOrEmpty(topic))
                return senderqueue;

            return sendertopic;
        }


    }
}
