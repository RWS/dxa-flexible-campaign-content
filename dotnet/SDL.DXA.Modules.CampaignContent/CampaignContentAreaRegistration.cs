using Sdl.Web.Common.Models;
using Sdl.Web.Mvc.Configuration;
using SDL.DXA.Modules.CampaignContent.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SDL.DXA.Modules.CampaignContent
{
    /// <summary>
    /// Area registration for the CampaignContent module
    /// </summary>
    public class CampaignContentAreaRegistration : BaseAreaRegistration
    {
        public override string AreaName
        {
            get
            {
                return "CampaignContent";
            }
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
    }
}