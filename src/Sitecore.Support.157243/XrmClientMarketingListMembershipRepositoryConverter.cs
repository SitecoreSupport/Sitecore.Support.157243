using Sitecore.Connect.Crm.DynamicsCrm.Repositories;
using Sitecore.DataExchange.Converters;
using Sitecore.Services.Core.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Sitecore.DataExchange.Repositories;
using Sitecore.DataExchange.Attributes;

namespace Sitecore.Support.DataExchange.Providers.DynamicsCrm.Converters.Repositories
{
    [SupportedIds(new string[]
    {
        "{4BA7DF95-6CFD-4DB3-B331-AFE7C5BC06F9}"
    })]
    public class XrmClientMarketingListMembershipRepositoryConverter : BaseItemModelConverter<ItemModel, XrmClientEntityRepository>
    {
        public XrmClientMarketingListMembershipRepositoryConverter(IItemModelRepository repository) : base(repository)
        {

        }

        public override XrmClientEntityRepository Convert(ItemModel source)
        {
            if (!this.CanConvert(source))
            {
                return null;
            }
            return new Sitecore.Support.Connect.Crm.DynamicsCrm.Repositories.XrmClientMarketingListMembershipRepositoryEx(base.GetStringValue(source, "EntityName"));
        }
    }
}