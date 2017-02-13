using Sdl.Web.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SDL.DXA.Modules.CampaignContent.Models
{
    /// <summary>
    /// Campaign Content ZIP Media Item
    /// </summary>
    [SemanticEntity(EntityName = "CampaignContentZIP", Vocab = CoreVocabulary, Prefix = "s")]
    public class CampaignContentZIP : MediaItem
    {

        [SemanticProperty("s:taggedContent")]
        public List<TaggedContent> TaggedContent { get; set; }

        public string ProcessedContent { get; set; }

        public override string ToHtml(string widthFactor, double aspect = 0, string cssClass = null, int containerSize = 0)
        {
            return null;
        }
    }
}