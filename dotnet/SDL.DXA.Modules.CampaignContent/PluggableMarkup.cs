using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SDL.DXA.Modules.CampaignContent
{
    /// <summary>
    /// Pluggable Markup
    /// </summary>
    public static class PluggableMarkup
    {
        internal const string MARKUP_PREFIX = "CAMPAIGNCONTENT_PluggableMarkup_";

        public static void RegisterMarkup(string label, string htmlMarkup)
        {
            if ( HttpContext.Current != null )
            {
                string labelKey = MARKUP_PREFIX + label;
                string markup;
                if ( HttpContext.Current.Items.Contains(labelKey) )
                {
                    markup = HttpContext.Current.Items[labelKey] as string + htmlMarkup;
                }
                else
                {
                    markup = htmlMarkup;
                }
                HttpContext.Current.Items[labelKey] = markup;
            }
        }

        public static MvcHtmlString Markup(this HtmlHelper htmlHelper, string label)
        {
            string markup = HttpContext.Current.Items[MARKUP_PREFIX + label] as string;
            if ( markup != null )
            {
                return new MvcHtmlString(markup);
            }
            else
            {
                return new MvcHtmlString("");
            }
        }

    }
}