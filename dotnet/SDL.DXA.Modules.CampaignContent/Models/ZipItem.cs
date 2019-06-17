using Sdl.Web.Common.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace SDL.DXA.Modules.CampaignContent.Models
{
    /// <summary>
    /// Zip Item.
    /// </summary>
    public class ZipItem
    {
        public StaticContentItem ContentItem { get; set; }
        public string UrlPath { get; set; }
        public string LocalFileName { get; set; }
    }
}