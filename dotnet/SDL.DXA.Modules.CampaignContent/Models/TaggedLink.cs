using Sdl.Web.Common.Models;

namespace SDL.DXA.Modules.CampaignContent.Models
{
    [SemanticEntity(EntityName = "TaggedLink", Vocab = CoreVocabulary, Prefix = "e")]
    public class TaggedLink : EntityModel
    {
        [SemanticProperty("e:name")]
        public string Name { get; set; }

        [SemanticProperty("e:url")]
        public string Url { get; set; }

        [SemanticProperty("e:componentLink")]
        public string ComponentLink { get; set; }
    }
}