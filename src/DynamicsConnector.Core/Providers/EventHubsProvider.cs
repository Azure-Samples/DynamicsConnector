using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicsConnector.Core.Providers
{
    public class EventHubsProvider : IDataDestinationProvider
    {
        public Task PostDataAsync<T>(List<T> data, params string[] parameters)
        {
            throw new NotImplementedException();
        }

        public Task PostDataAsync<T>(T dataItem, params string[] parameters)
        {
            throw new NotImplementedException();
        }
    }
}
