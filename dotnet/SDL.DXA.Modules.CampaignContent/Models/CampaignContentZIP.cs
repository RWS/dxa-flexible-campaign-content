using Sdl.Web.Common.Models;
using System.Collections.Generic;

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

        [SemanticProperty("s:taggedImages")]
        public List<TaggedImage> TaggedImages { get; set; }

        [SemanticProperty("s:taggedProperties")]
        public List<TaggedProperty> TaggedProperties { get; set; }

        [SemanticProperty("s:taggedLinks")]
        public List<TaggedLink> TaggedLinks { get; set; }

        public string ProcessedContent { get; set; }

        public override string ToHtml(string widthFactor, double aspect = 0, string cssClass = null, int containerSize = 0)
        {
            return null;
        }
    }
}