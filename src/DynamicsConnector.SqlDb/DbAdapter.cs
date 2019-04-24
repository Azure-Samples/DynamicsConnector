using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicsConnector.Core.Providers;
using Microsoft.Extensions.Logging;

namespace DynamicsConnector.SqlDb
{
    public class DbAdapter
    {
        private string cs;
        private ILogger Logger;

        public DbAdapter(string cs, ILogger logger)
        {
            this.cs = cs;
            this.Logger = logger; 
        }

        public List<IDictionary<string, object>> GetItems(string sourceTable)
        {
            try
            {
                var dataSource = new SqlDBAdapter();

                Logger.LogInformation($"Retriving data from {sourceTable}.");

                var items = dataSource.DapperSelect(cs, $"SELECT * FROM [dbo].[{sourceTable}]");

                Logger.LogInformation($"Found {items.Count} items.");

                return items;
            }
            catch(Exception ex)
            {
                Logger.LogError(ex.Message);
                return null;
            }
        }        
    }
}
