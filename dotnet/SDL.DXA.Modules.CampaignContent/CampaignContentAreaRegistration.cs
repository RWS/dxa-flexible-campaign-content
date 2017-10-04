using Sdl.Web.Common.Models;
using Sdl.Web.Mvc.Configuration;
using SDL.DXA.Modules.CampaignContent.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace SDL.DXA.Modules.CampaignContent
{
    /// <summary>
    /// Area registration for the CampaignContent module
    /// </summary>
    public class CampaignContentAreaRegistration : BaseAreaRegistration
    {
        const string NAMESPACE = "SDL.DXA.Modules.CampaignContent.Controllers";

        public override string AreaName
        {
            get
            {
                return "CampaignContent";
            }
        }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            base.RegisterArea(context);

            // Register non-entity controllers
            //
            MapRoute(context.Routes, "CampaignContent_GetAsset", "assets/campaign/{campaignId}/{*assetUrl}",
                new { controller = "CampaignAsset", action = "GetAsset" });

            MapRoute(context.Routes, "CampaignContent_GetAsset_Loc", "{localization}/assets/campaign/{campaignId}/{*assetUrl}",
                new { controller = "CampaignAsset", action = "GetAsset" });

        }

        protected override void RegisterAllViewModels()
        {
            // Entity Views
            //
            RegisterViewModel("CampaignContentZIP", typeof(CampaignContentZIP), "CampaignContent");

            // Page Views
            //
            RegisterViewModel("CampaignPage", typeof(PageModel));
        }

        /// <summary>
        /// Map route for a page controller. 
        /// As this is called after the global DXA initialization we have to shuffle around the route definition so it comes before the DXA page controller.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="name"></param>
        /// <param name="url"></param>
        /// <param name="defaults"></param>
        protected static void MapRoute(RouteCollection routes, string name, string url, object defaults)
        {
            Route route = new Route(url, new MvcRouteHandler())
            {
                Defaults = CreateRouteValueDictionary(defaults),
                DataTokens = new RouteValueDictionary()
                {
                    { "Namespaces", NAMESPACE}
                }
            };
            routes.Insert(0, route);
        }

        private static RouteValueDictionary CreateRouteValueDictionary(object values)
        {
            var dictionary = values as IDictionary<string, object>;
            if (dictionary != null)
            {
                return new RouteValueDictionary(dictionary);
            }

            return new RouteValueDictionary(values);
        }
    }
}