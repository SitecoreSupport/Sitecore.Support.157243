using Microsoft.Xrm.Sdk;
using Sitecore.Connect.Crm.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Sdk.Messages;
using Sitecore.Connect.Crm.DynamicsCrm.Extensions;
using Sitecore.Connect.Crm.DynamicsCrm.Plugins;
using Microsoft.Crm.Sdk.Messages;
using Sitecore.Connect.Crm.DynamicsCrm.Repositories;

namespace Sitecore.Support.Connect.Crm.DynamicsCrm.Repositories
{
    public class XrmClientMarketingListMembershipRepositoryEx : BaseXrmClientMemberEntitiesRepository
    {
        public XrmClientMarketingListMembershipRepositoryEx(string entityName) : base(entityName)
        {
        }
        protected override IEnumerable<Entity> ReadMembers(Guid entityId, RepositoryOperationContext context)
        {
            if (IsDynamicList(entityId))
            {
                return GetMembersForDynamicMarketingList(entityId, context);
            }
            return GetMembersForStaticMarketingList(entityId, context);
        }
        private bool IsDynamicList(Guid marketingListEntityId)
        {
            //
            var context = new RepositoryOperationContext();
            var settings = new ReadEntityRepositoryOperationSettings
            {
                ColumnSet = new ColumnSet("type")
            };
            context.Plugins.Add(settings);
            var listRepo = new XrmClientEntityRepository("list") { OrganizationService = this.OrganizationService };
            var list = listRepo.Read(marketingListEntityId, context);
            if (list == null || !list.Contains("type"))
            {
                throw new InvalidOperationException(string.Format("Unable to determine the list type for the specified marketing list. (repository type: {0}, list id: {1})", this.GetType().FullName, marketingListEntityId));
            }
            return (bool)list["type"];
        }
        private IEnumerable<Entity> GetMembersForDynamicMarketingList(Guid marketingListEntityId, RepositoryOperationContext context)
        {
            var service = this.OrganizationService;
            var listFilterExpression = new FilterExpression(LogicalOperator.And);
            listFilterExpression.Conditions.Add(GetConditionExpressionForEntity("listid", marketingListEntityId, context));
            var cols = new ColumnSet(new string[] { "query", "listname" });
            var list = service.Retrieve("list", marketingListEntityId, cols);
            if (!list.Attributes.ContainsKey("query"))
            {
                //TODO: throw exception
                return Enumerable.Empty<Entity>();
            }
            var dynamicQuery = list.Attributes["query"].ToString();
            var request = new FetchXmlToQueryExpressionRequest
            {
                FetchXml = dynamicQuery
            };
            var result = service.Execute(request) as FetchXmlToQueryExpressionResponse;
            var query = result.Query;

            var settings = context.GetReadEntityRepositoryOperationSettings();
            if (settings != null)
            {
                query.ColumnSet = settings.ColumnSet;
                if (!settings.ExcludeActiveFilter)
                {
                    query.Criteria.Conditions.Add(GetConditionExpressionForActiveEntities());
                }
            }
            query.PageInfo = new PagingInfo
            {
                Count = settings.PageSize,
                PageNumber = 1
            };

            query.NoLock = true;
            var settings2 = context.GetEntityHandlingSettings();
            if (settings2 == null)
            {
                settings2 = new EntityHandlingSettings();
            }
            if (settings2.Handle == null)
            {
                settings2.Handle = (entity) =>
                {
                    entity["list.listid"] = new AliasedValue("list", "listid", list.Id);
                    entity["list.listname"] = new AliasedValue("list", "listname", list["listname"]);
                };
            }
            var entities = ReadEntities(query, context, settings2.Handle);
            return entities;
        }
        private IEnumerable<Entity> ReadEntities(QueryExpression query, RepositoryOperationContext context, Action<Entity> convert = null)
        {
            var service = this.OrganizationService;
            if (service == null)
            {
                yield break;
            }
            var settings = context.GetReadEntityRepositoryOperationSettings();
            var numberOfEntitiesRead = 0;
            var shouldContinueReadingEntities = true;
            while (shouldContinueReadingEntities)
            {
                var request = new RetrieveMultipleRequest
                {
                    Query = query
                };
                var response = service.Execute(request) as RetrieveMultipleResponse;
                if (!response.EntityCollection.Entities.Any())
                {
                    break;
                }
                query.PageInfo.PagingCookie = response.EntityCollection.PagingCookie;
                var count = response.EntityCollection.Entities.Count;
                foreach (var entity in response.EntityCollection.Entities)
                {
                    if (convert != null)
                    {
                        convert(entity);
                    }
                    yield return entity;
                    numberOfEntitiesRead++;
                    if (settings.MaxCount > 0 && numberOfEntitiesRead >= settings.MaxCount)
                    {
                        shouldContinueReadingEntities = false;
                        break;
                    }
                }

                if (!response.EntityCollection.MoreRecords)
                {
                    break;
                }

                query.PageInfo.PageNumber++;
            }
        }
        private IEnumerable<Entity> GetMembersForStaticMarketingList(Guid marketingListEntityId, RepositoryOperationContext context)
        {
            //
            var listFilterExpression = new FilterExpression(LogicalOperator.And);
            listFilterExpression.Conditions.Add(GetConditionExpressionForEntity("listid", marketingListEntityId, context));
            //
            var listEntity = new LinkEntity
            {
                JoinOperator = JoinOperator.Inner,
                LinkCriteria = listFilterExpression,
                LinkFromAttributeName = "listid",
                LinkFromEntityName = "listmember",
                LinkToAttributeName = "listid",
                LinkToEntityName = "list",
                EntityAlias = "list",
                Columns = new ColumnSet("listid", "listname")
            };
            var listMemberEntity = new LinkEntity
            {
                JoinOperator = JoinOperator.Inner,
                LinkFromAttributeName = "contactid",
                LinkFromEntityName = "contact",
                LinkToAttributeName = "entityid",
                LinkToEntityName = "listmember"
            };
            listMemberEntity.LinkEntities.Add(listEntity);
            //
            var query = new QueryExpression
            {
                EntityName = "contact",
                PageInfo = new PagingInfo
                {
                    PageNumber = 1
                },
                NoLock = true
            };
            var settings = context.GetReadEntityRepositoryOperationSettings();
            if (settings != null)
            {
                query.ColumnSet = settings.ColumnSet;
                if (settings.PagingInfo != null)
                {
                    query.PageInfo = settings.PagingInfo;
                }
                if (!settings.ExcludeActiveFilter)
                {
                    if (query.Criteria == null)
                    {
                        query.Criteria = new FilterExpression();
                    }
                    query.Criteria.Conditions.Add(GetConditionExpressionForActiveEntities());
                }
            }
            query.LinkEntities.Add(listMemberEntity);
            //
            return ReadEntities(query, context);
        }
    }
}
