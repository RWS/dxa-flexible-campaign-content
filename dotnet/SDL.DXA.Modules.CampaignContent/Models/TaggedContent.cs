using Sdl.Web.Common.Models;

namespace SDL.DXA.Modules.CampaignContent.Models
{
    /// <summary>
    /// Tagged Content Entity
    /// </summary>
    [SemanticEntity(EntityName = "TaggedContent", Vocab = CoreVocabulary, Prefix = "e")]
    public class TaggedContent : EntityModel
    {
        [SemanticProperty("e:name")]
        public string Name { get; set; }

        [SemanticProperty("e:content")]
        public RichText Content { get; set; }
    }
}