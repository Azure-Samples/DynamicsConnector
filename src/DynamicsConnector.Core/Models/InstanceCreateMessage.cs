using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicsConnector.Core.Models
{
    public class InstanceCreateMessage
    {
        public Guid Id { get; set; }
        public string DynamicsEntityName { get; set; }
        public string DynamicsEntityToken { get; set; }
        public IDictionary<string, object> EntityDetails { get; set; }
        public string MissingMappingField { get; set; }
        public bool Update { get; set; }

        public InstanceCreateMessage(string entity, string entityToken, IDictionary<string, object> entityDetails, bool update = false)
        {
            this.DynamicsEntityName = entity;
            this.DynamicsEntityToken = entityToken;
            this.EntityDetails = entityDetails;
            this.Update = update;            
        }
    }
}
