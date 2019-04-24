using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicsConnector.Core.Providers
{
    public interface IDataDestinationProvider
    {
        Task PostDataAsync<T>(List<T> data, params string[] parameters);
        Task PostDataAsync<T>(T dataItem, params string[] parameters);
    }
}
