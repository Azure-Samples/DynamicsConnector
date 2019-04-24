using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace DynamicsConnector.Core.Providers
{
    public class SqlDBAdapter
    {
        public List<T> GetSourceData<T>(string sourceCs, string sql)
        {
            using (SqlConnection conn = new SqlConnection(sourceCs))
            {
                conn.Open();
                return conn.QueryAsync<T>(sql, null).Result.ToList();
            }
        }

        public List<IDictionary<string, object>> DapperSelect(string connectionString, string query, object parameters = null)
        {            
            using (var connection = new SqlConnection(connectionString))
            {
                var items = connection.Query(query, parameters).ToList();
                return items.Select(x => (IDictionary<string, object>)x).ToList();
            }            
        }
    }
}
