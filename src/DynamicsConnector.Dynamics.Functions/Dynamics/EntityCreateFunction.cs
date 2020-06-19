using DynamicsConnector.Core.Models;
using DynamicsConnector.Core.Providers;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;

namespace DynamicsConnector.Dynamics.Functions.Dynamics
{
    public static class EntityCreateFunction
    {
        #region Variables and Constants
        private static DynamicsHelper _dynamicsService;
        private static readonly dynamic _credentials =
            new
            {
                crmorg = ConfigurationManager.AppSettings["DynamicsURL"],
                user = ConfigurationManager.AppSettings["DynamicsAuthenticationUser"],
                password = ConfigurationManager.AppSettings["DynamicsAuthenticationPassword"]
            };

        private static readonly string azureWebJobsStorage = ConfigurationManager.AppSettings["AzureWebJobsStorage"];
        

        private static readonly string shareName = ConfigurationManager.AppSettings["MappingShareName"];
        private static readonly string fileName = ConfigurationManager.AppSettings["MappingFileName"];
        
        #endregion

        [FunctionName("DynamicsEntityCreate")]
        public static void Run([ServiceBusTrigger("entitycreate", access: AccessRights.Listen)] string mySbMsg, ILogger log)
        {
            var startTime = DateTime.Now;
            try
            {
                log.LogInformation($"Function 'DynamicsEntityCreate' executed at: {startTime}");

                if (mySbMsg == null)
                {
                    throw new ArgumentNullException(nameof(mySbMsg));
                }  
                
                _dynamicsService = new DynamicsHelper(_credentials.crmorg, _credentials.user, _credentials.password, log);

                var message = JsonConvert.DeserializeObject<InstanceCreateMessage>(mySbMsg);
                IDictionary<string, object> entityDetails = new Dictionary<string, object>();                
                var entityMapper = GetEntityMapper(message.DynamicsEntityName, log);
                if(entityMapper != null)
                {
                    foreach(var k in entityMapper)
                    {
                        if (message.EntityDetails.ContainsKey(k.Key) && !entityDetails.ContainsKey(k.Value))
                            entityDetails.Add(k.Value, message.EntityDetails[k.Key]);
                    }
                }

                if (entityDetails.Count == 0)
                    entityDetails = message.EntityDetails;

                if(message.Update && message.Id != Guid.Empty)
                {
                    _dynamicsService.UpdateEntityAttributes(message.DynamicsEntityName, message.Id, entityDetails);
                }
                else
                _dynamicsService.CreateEntityInstance(message.DynamicsEntityName, message.DynamicsEntityToken, entityDetails, message.Update, message.MissingMappingField);
            }
            catch (Exception exception)
            {
                log.LogError($"Function 'AlertCreate' Exeption: {exception.Message} {exception.InnerException ?? exception.InnerException}");
            }
            finally
            {
                log.LogInformation($"Function 'AlertCreate' finished at: {DateTime.Now}, Timespan = {(DateTime.Now - startTime).TotalSeconds} seconds!");
            }
        }

        private static Dictionary<string, string> GetEntityMapper(string entityName, ILogger log)
        {
            if (string.IsNullOrEmpty(azureWebJobsStorage))
                return null;

            var storageParams = azureWebJobsStorage.Split(';');
            string storageAccountKey = "", storageAccountName = "";

            foreach (var p in storageParams)
            {
                if (p.StartsWith("AccountName"))
                    storageAccountName = p.Substring(p.IndexOf('=') + 1);
                if(p.StartsWith("AccountKey"))
                    storageAccountKey = p.Substring(p.IndexOf('=') + 1);
            } 

            if (string.IsNullOrEmpty(storageAccountKey) || string.IsNullOrEmpty(storageAccountName))
            {
                log.LogInformation($"Storgae account Key or Name hasn't been set!");
                return null;
            }

            if (string.IsNullOrEmpty(shareName) || string.IsNullOrEmpty(fileName))
            {
                log.LogInformation($"ShareName or File Name hasn't been set!");
                return null;
            }

            var fileStorageProvider = new FileStorageProvider(storageAccountName, storageAccountKey);

            var encoded = fileStorageProvider.ReadFromFile(shareName, fileName);
            var decodedMessage = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(encoded);

            if (decodedMessage != null && decodedMessage.ContainsKey(entityName))
                return decodedMessage[entityName];
            else
                return null;  
        }            
    }
}
