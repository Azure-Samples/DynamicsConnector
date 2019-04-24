using Microsoft.Extensions.Logging;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;

namespace DynamicsConnector.Dynamics
{
    public class DynamicsHelper
    {
        private readonly dynamic crmorg;
        private readonly dynamic user;
        private readonly dynamic password;

        private OrganizationServiceProxy organizationProxy;
        private ILogger Logger;


        public DynamicsHelper(dynamic crmorg, dynamic user, dynamic password, ILogger logger)
        {
            this.crmorg = crmorg;
            this.user = user;
            this.password = password;            
            this.Logger = logger;

            organizationProxy = GetOrganizationServiceProxy(crmorg, user, password);
        }

        public void CreateEntityInstance(string entityName, string entityToken, IDictionary<string, object> entityDetails, bool update, string missingMappingField)
        {
            try
            {
                DataCollection<Entity> entytiesCollection = GetEntityInstances(organizationProxy, entityName, new Dictionary<string, object>() { { entityToken, entityDetails[entityToken] } });

                if (entytiesCollection == null || entytiesCollection.Count == 0)
                {
                    Entity createdEntity = BuildEntityInstance(organizationProxy, entityDetails, entityName, missingMappingField);

                    if (createdEntity != null)
                    {
                        Guid entityId = organizationProxy.Create(createdEntity);
                        Logger.LogInformation($"Created entity with ID {entityId}");
                    }
                }
                else
                if(update)
                {
                    Entity currentEntity = entytiesCollection.First();
                    UpdateEntityAttributes(currentEntity, entityDetails);
                }
                else
                {
                    Logger.LogWarning($"Dynamics Entity was not created! Entity with properties : {entityToken} - {entityDetails[entityToken]} already exists in Dynamics!");
                    return;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("CreateEntityInstance " + ex.Message + ex.InnerException ?? ex.InnerException.Message);
            }
        }

        public void UpdateEntityAttributes(string entityName, Guid id, IDictionary<string, object> entityDetails)
        {
            Entity dynamicsEntity = organizationProxy.Retrieve(entityName, id, new ColumnSet(true));
            if (dynamicsEntity == null)
                return;

            UpdateEntityAttributes(dynamicsEntity, entityDetails);
        }

        public void UpdateEntityAttributes(Entity currentEntity, IDictionary<string, object> entityDetails)
        {
            try
            {
                Logger.LogInformation($"Entity is already created in Dynamics, trying to update.");

                var dynamicsEntityProperties = GetDynamicsEntityProperties(currentEntity.LogicalName);


                bool changed = false;
                foreach (var entityDetail in entityDetails)
                {
                    var entityMetadata = dynamicsEntityProperties.FirstOrDefault(a => a.LogicalName == MainKey(entityDetail.Key));

                    if (entityDetail.Value != null && HasValueChanged(currentEntity, entityMetadata, entityDetail))
                    {
                        changed = true;                        
                        this.ApplyNewValue(currentEntity, entityMetadata, entityDetail);                        
                    }
                }
                if (changed)
                {
                    organizationProxy.Update(currentEntity);
                    Logger.LogInformation($"Updated entity with ID {currentEntity.Id}");
                }
                else
                    Logger.LogInformation($"Update skipped for entity with ID {currentEntity.Id} since nothing has changed");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Update Entity {currentEntity.LogicalName} Exception! " + ex.Message + ex.InnerException ?? ex.InnerException.Message);
            }
        }

        private string MainKey(string key)
        {
            return key.Split(new[] { "___" }, StringSplitOptions.RemoveEmptyEntries).First();
        }

        private bool HasValueChanged(Entity currentEntity, AttributeMetadata entityMetadata, KeyValuePair<string, object> entityDetail)
        {
            var key = MainKey(entityDetail.Key);
            if (!currentEntity.Attributes.Contains(key))
                return false;

            var entityType = currentEntity[key].GetType(); 

            switch (currentEntity[key])
            {
                case string val:
                    if(val != entityDetail.Value.ToString())
                        return true;
                    break;

                case int ival:
                    int i;
                    if (int.TryParse(entityDetail.Value.ToString(), out i) && ival != i)
                        return true;
                    break;

                case double dval:
                    double d;
                    if (Double.TryParse(entityDetail.Value.ToString(), out d) && (Math.Abs(dval - d) > 0.001))
                        return true;
                    break;

                case decimal decval:
                    decimal decv;
                    if (decimal.TryParse(entityDetail.Value.ToString(), out decv) && (decval != decv))
                        return true;
                    break;

                case bool bval:
                    bool b;
                    if (Boolean.TryParse(entityDetail.Value.ToString(), out b) && (b != bval))
                        return true;
                    break;

                case DateTime dtval:
                    DateTime dt;
                    if (DateTime.TryParse(entityDetail.Value.ToString(), out dt) && (dt != dtval))
                        return true;
                    break;

                case Money mval:
                    decimal money;
                    if (decimal.TryParse(entityDetail.Value.ToString(), out money) && (money != mval.Value))
                        return true;
                    break;

                case EntityReference er:
                    return er.Name != entityDetail.Value.ToString();

                case OptionSetValue osv:                    
                    int iosv;
                    OptionMetadata currentOption;
                    if (int.TryParse(entityDetail.Value.ToString(), out iosv))
                    {
                        currentOption = ((EnumAttributeMetadata)entityMetadata).OptionSet.Options.First(o => o.Value == iosv);                        
                    }                        
                    else
                    {
                        currentOption = ((EnumAttributeMetadata)entityMetadata).OptionSet.Options.First(o => o.Label.LocalizedLabels.First().Label.ToLower() == entityDetail.Value.ToString().ToLower());                        
                    }
                    return osv.Value != currentOption.Value;                    

                default:
                    return false;                    
            }                

            return false;
        }
        
        internal Entity BuildEntityInstance(OrganizationServiceProxy organizationProxy, IDictionary<string, object> entityDetails, string entityName, string missingMappingfield = null)
        {
            try
            {
                var dynamicsEntityProperties = GetDynamicsEntityProperties(entityName);

                //Creating new Alert
                Entity dynamicsEntity = new Entity(entityName);
                StringBuilder otherInfo = new StringBuilder();                                

                foreach (var keyWithPrefix in entityDetails.Keys)
                {
                    var keys = keyWithPrefix.Split(new[] { "___" }, StringSplitOptions.RemoveEmptyEntries);
                    var key = keys.First();

                    // By default assume that corresponsing property on the related entity has the same name
                    var lookUpKey = key;

                    // Otherwise referenced entity property name should be stored within current mapping localname___refname
                    if (keys.Count() > 1)
                        lookUpKey = keys.Last();

                    if (dynamicsEntityProperties != null && dynamicsEntityProperties.Any(a => a.LogicalName == key))
                    {
                        var entityMetadata = dynamicsEntityProperties.First(a => a.LogicalName == key);
                        
                        ApplyNewValue(dynamicsEntity, entityMetadata, entityDetails[keyWithPrefix], key, lookUpKey);
                    }
                    else
                        if (entityDetails[key] != null)
                    {
                        otherInfo.AppendLine($"{key} - {entityDetails[key]}");
                    }
                }

                if(missingMappingfield != null && !string.IsNullOrEmpty(missingMappingfield) && dynamicsEntityProperties.Any(a => a.LogicalName == missingMappingfield))
                {
                    dynamicsEntity.Attributes[missingMappingfield] = otherInfo.ToString();
                }                

                return dynamicsEntity;
            }
            catch (Exception exception)
            {
                Logger.LogError(exception.Message);
                return null;
            }
        }

        private void ApplyNewValue(Entity dynamicsEntity, AttributeMetadata entityMetadata, KeyValuePair<string, object> entityDetail)
        {
            var keys = entityDetail.Key.Split(new[] { "___" }, StringSplitOptions.RemoveEmptyEntries);
            var key = keys.First();

            // By default assume that corresponsing property on the related entity has the same name
            var lookUpKey = key;

            // Otherwise referenced entity property name should be stored within current mapping localname___refname
            if (keys.Count() > 1)
                lookUpKey = keys.Last();

            ApplyNewValue(dynamicsEntity, entityMetadata, entityDetail.Value, key, lookUpKey);
        }
        private void ApplyNewValue(Entity dynamicsEntity, AttributeMetadata entityMetadata, object entityDetail, string key, string lookUpKey = null)
        {
            switch (entityMetadata.AttributeType)
            {
                case AttributeTypeCode.DateTime:
                    DateTime dateField;
                    if (DateTime.TryParse(entityDetail.ToString(), out dateField))
                        dynamicsEntity.Attributes[key] = dateField;
                    else
                        Logger.LogWarning($"Wasn't able to convert property {key} with data {entityDetail} to datetime!");
                    break;

                case AttributeTypeCode.Picklist:
                    PicklistAttributeMetadata picklistAttributeMetadata = entityMetadata as PicklistAttributeMetadata;
                    if (picklistAttributeMetadata != null)
                    {
                        var item = picklistAttributeMetadata.OptionSet.Options.FirstOrDefault(o => o.Label.LocalizedLabels.FirstOrDefault().Label.ToLower() == entityDetail.ToString().ToLower());
                        if (item != null)
                            dynamicsEntity.Attributes[key] = new OptionSetValue(item.Value ?? -1);
                        else
                        {
                            Logger.LogWarning($"Wasn't able to find lookup field {entityDetail} within Picklist options {key}.");
                            Logger.LogWarning("Adding new field to Option Set");
                            int maxVal = picklistAttributeMetadata.OptionSet.Options.Max(o => o.Value) ?? 0;
                            int? value = AddOptionSetItem(dynamicsEntity.LogicalName, key, entityDetail, maxVal + 10);
                            if(value != null)
                                dynamicsEntity.Attributes[key] = new OptionSetValue(value ?? -1);
                        }
                    }
                    else
                        Logger.LogWarning($"Wasn't able to convert property {key} with data {entityDetail} to Option Set Value!");
                    break;

                case AttributeTypeCode.Virtual:
                    MultiSelectPicklistAttributeMetadata multiPicklistAttributeMetadata = entityMetadata as MultiSelectPicklistAttributeMetadata;
                    if (multiPicklistAttributeMetadata != null)
                    {
                        IEnumerable<string> labels = entityDetail.ToString().Split(';');
                        var items = multiPicklistAttributeMetadata.OptionSet.Options.Where(o => labels.Contains(o.Label.LocalizedLabels.FirstOrDefault().Label.ToString()));
                        if (items != null)
                            dynamicsEntity.Attributes[key] = new OptionSetValueCollection(items.Select(i => new OptionSetValue(i.Value ?? -1)).ToList());
                        else
                            Logger.LogWarning($"Wasn't able to find lookup field {entityDetail.ToString()} within Picklist options {key}");
                    }
                    else
                        Logger.LogWarning($"Wasn't able to convert property {key} with data {entityDetail} to Multi Select Picklist!");
                    break;

                case AttributeTypeCode.Lookup:
                    var refKey = ((LookupAttributeMetadata)entityMetadata).Targets.FirstOrDefault();
                    var entities = GetEntityInstances(organizationProxy, refKey, new Dictionary<string, object> { { lookUpKey, entityDetail } });
                    if (entities != null && entities.Any())
                    {
                        var entity = entities.First();
                        dynamicsEntity.Attributes[key] = new EntityReference(entity.LogicalName, entity.Id);
                    }
                    break;

                case AttributeTypeCode.Money:
                    decimal money;
                    if (decimal.TryParse(entityDetail.ToString(), out money))
                        dynamicsEntity.Attributes[key] = new Money(money);
                    else Logger.LogWarning($"Wasn't able to convert property {key} with data {entityDetail} to decimal!");
                    break;

                case AttributeTypeCode.Double:
                    double dval;
                    if (double.TryParse(entityDetail.ToString(), out dval))
                        dynamicsEntity.Attributes[key] = dval;
                    else Logger.LogWarning($"Wasn't able to convert property {key} with data {entityDetail} to double!");
                    break;

                case AttributeTypeCode.Decimal:
                    decimal decval;
                    if (decimal.TryParse(entityDetail.ToString(), out decval))
                        dynamicsEntity.Attributes[key] = decval;
                    else Logger.LogWarning($"Wasn't able to convert property {key} with data {entityDetail} to decimal!");
                    break;

                case AttributeTypeCode.Memo:
                case AttributeTypeCode.String:
                    dynamicsEntity.Attributes[key] = entityDetail.ToString();
                    break;

                case AttributeTypeCode.Integer:
                    int ival;
                    if (int.TryParse(entityDetail.ToString(), out ival))
                        dynamicsEntity.Attributes[key] = ival;
                    else Logger.LogWarning($"Wasn't able to convert property {key} with data {entityDetail} to Integer!");
                    break;

                case AttributeTypeCode.Boolean:
                    bool bval;
                    if (Boolean.TryParse(entityDetail.ToString(), out bval))
                        dynamicsEntity.Attributes[key] = bval;
                    else Logger.LogWarning($"Wasn't able to convert property {key} with data {entityDetail} to Boolean!");
                    break;

                case AttributeTypeCode.Customer:               
                    refKey = ((LookupAttributeMetadata)entityMetadata).Targets.FirstOrDefault();
                    entities = GetEntityInstances(organizationProxy, refKey, new Dictionary<string, object> { { lookUpKey, entityDetail } });
                    if (entities != null && entities.Any())
                    {
                        var entity = entities.First();
                        dynamicsEntity.Attributes[key] = new EntityReference(entity.LogicalName, entity.Id);
                    }
                    break;

                case AttributeTypeCode.Status:
                case AttributeTypeCode.State:
                    int istate;
                    if (int.TryParse(entityDetail.ToString(), out istate))  
                        dynamicsEntity.Attributes[key] = new OptionSetValue(istate);
                    else
                    {
                        var option =((EnumAttributeMetadata)entityMetadata).OptionSet.Options.Last(o => o.Label.LocalizedLabels.First().Label.ToLower() == entityDetail.ToString().ToLower());
                        if (option != null)
                            dynamicsEntity.Attributes[key] = new OptionSetValue(option.Value ?? 1);
                    }
                    break;

                default:
                    Logger.LogError($"Not added Attribute {key} of type {entityMetadata.AttributeType} with value {entityDetail} to the entity during creation!");
                    break;
            }
        }

        private int? AddOptionSetItem(string logicalName, string key, object entityDetail, int optionValue)
        {
            try
            {
                var insertOptionValueRequest = new InsertOptionValueRequest
                {
                    EntityLogicalName = logicalName,
                    AttributeLogicalName = key,
                    Label = new Label(entityDetail.ToString(), 1033),
                    Value = optionValue
                };

                return ((InsertOptionValueResponse)organizationProxy.Execute(insertOptionValueRequest)).NewOptionValue;
            }
            catch(Exception ex)
            {
                return null;
            }
        }

        public IEnumerable<AttributeMetadata> GetDynamicsEntityProperties(string entityName)
        {
            try
            {
                RetrieveEntityRequest retrieveEntityRequest = new RetrieveEntityRequest
                {
                    EntityFilters = EntityFilters.Attributes,
                    LogicalName = entityName
                };

                RetrieveEntityResponse entityMetadata = (RetrieveEntityResponse)organizationProxy.Execute(retrieveEntityRequest);
                return entityMetadata.EntityMetadata.Attributes;

            }
            catch (Exception exception)
            {
                Logger.LogError("GetDynamicsEntityProperties: " + exception.Message + exception.InnerException ?? "; Inner " + exception.InnerException.Message);
                return null;
            }
        }

        public IEnumerable<Entity> GetEntityInstances(string entityName, Dictionary<string, object> conditions)
        {
            return GetEntityInstances(organizationProxy, entityName, conditions);
        }

        public DataCollection<Entity> GetEntityInstances(OrganizationServiceProxy organizationProxy, string entityName, Dictionary<string, object> conditions)
        {
            try
            {
                QueryExpression alertsQuery = new QueryExpression
                {
                    EntityName = entityName,
                    ColumnSet = new ColumnSet(true)
                };

                if(conditions == null || !conditions.Any())
                {
                    alertsQuery.Criteria.Conditions.Add(
                        new ConditionExpression
                        {
                            AttributeName = "createdon",                            
                            Operator = ConditionOperator.NotEqual,
                            Values = { DateTime.MaxValue }
                        }
                        );
                }
                else
                foreach (var condition in conditions)
                {
                    alertsQuery.Criteria.Conditions.Add(
                        new ConditionExpression
                        {
                            AttributeName = condition.Key,
                            Operator = ConditionOperator.Equal,
                            Values = { condition.Value == null ? "Unknown" : condition.Value.ToString() }
                        }
                        );
                }

                var res = organizationProxy.RetrieveMultiple(alertsQuery);
                return res.Entities;
            }
            catch (Exception exception)
            {
                Logger.LogError(exception.Message);
                return null;
            }
        }

        public void DeleteInstance(string entityName, Guid id)
        {
            organizationProxy.Delete(entityName, id);            
        }

        private OrganizationServiceProxy GetOrganizationServiceProxy(string url, string user, string password)
        {
            try
            {
                ClientCredentials clientCredentials = new ClientCredentials();
                clientCredentials.UserName.UserName = user;
                clientCredentials.UserName.Password = password;
                // Set security protocol to TLS 1.2 for version 9.0 of Customer Engagement Platform
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                return new OrganizationServiceProxy(new Uri(url + "/XRMServices/2011/Organization.svc"), null, clientCredentials, null);
            }
            catch (Exception exception)
            {
                Logger.LogError(exception.Message);
                return null;
            }
        }
    }
}
