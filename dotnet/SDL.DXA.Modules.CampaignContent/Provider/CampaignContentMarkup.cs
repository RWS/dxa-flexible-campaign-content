﻿using System;

namespace SDL.DXA.Modules.CampaignContent.Provider
{
    /// <summary>
    /// Campaign Content Markup
    /// </summary>
    public class CampaignContentMarkup
    {
        public string HeaderHtml { get; set; }
        public string MainHtml { get; set; }
        public string FooterHtml { get; set; }
        public DateTime LastModified { get; set; }
    }
}