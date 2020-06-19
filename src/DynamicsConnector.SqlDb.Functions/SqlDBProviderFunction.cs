using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DynamicsConnector.Core.Models;
using DynamicsConnector.Core.Providers;
using DynamicsConnector.SqlDb;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DynamicsConnector.SqlDb.Functions
{
    public static class SqlDBProviderFunction
    {
        private static ConnectionStringSettings dataBaseConnectionString = ConfigurationManager.ConnectionStrings["SourceDB_CS"];
        private static string sourceDataTable = ConfigurationManager.AppSettings["SourceDataTable"];

        private static string serviceBusConnectionString = ConfigurationManager.AppSettings["AzureWebJobsServiceBus"];
        private static string dynamicsInstanceCreateQueue = ConfigurationManager.AppSettings["DynamicsInstanceCreateQueue"];
        private static string dynamicsEntityName = ConfigurationManager.AppSettings["DynamicsEntityName"];
        private static string dynamicsEntityToken = ConfigurationManager.AppSettings["DynamicsEntityToken"];
        private static string missingMappingField = ConfigurationManager.AppSettings["MissingMappingField"];
        private static bool update = false;

        [FunctionName("SqlDbProviderFunction")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, ILogger log)
        {
            await RunAsync(log);
            return req.CreateResponse(HttpStatusCode.OK, "Faults publisher function! See details in log.");
        }
        //public static void Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger logger)        
        //{           
        //    RunAsync(logger).GetAwaiter().GetResult();
        //    return;
        //}

        public static async Task RunAsync(ILogger logger)
        {
            var currentTime = DateTime.Now;
            logger.LogInformation($"Sql DB Publisher Function has started at {currentTime}");

            DbAdapter ff = new DbAdapter(dataBaseConnectionString.ConnectionString, logger);
            IDataDestinationProvider dataDestination = new ServiceBusProvider();

            var dbEntities = ff.GetItems(sourceDataTable);

            if (ConfigurationManager.AppSettings["Update"] != null)
                bool.TryParse(ConfigurationManager.AppSettings["Update"], out update);

            if (dbEntities.Any())
            {
                bool update = (currentTime.Hour == 0);

                logger.LogInformation($"Starting iterating over Faults, update state is {update}.");                

                foreach (var sourceItem in dbEntities)
                {
                    var message = new InstanceCreateMessage(dynamicsEntityName, dynamicsEntityToken, sourceItem, update) { MissingMappingField = missingMappingField };

                    try
                    {
                        await dataDestination.PostDataAsync<InstanceCreateMessage>(message, new string[] { serviceBusConnectionString, "", dynamicsInstanceCreateQueue, "" });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex.Message);
                    }                    
                }

                logger.LogInformation("Iteration over Faults is completed!");
            }

            return;
        }
    }
}
