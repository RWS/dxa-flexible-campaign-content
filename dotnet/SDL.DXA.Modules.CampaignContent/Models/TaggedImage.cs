using Sdl.Web.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SDL.DXA.Modules.CampaignContent.Models
{
    [SemanticEntity(EntityName = "TaggedImage", Vocab = CoreVocabulary, Prefix = "e")]
    public class TaggedImage : EntityModel
    {
        [SemanticProperty("e:name")]
        public string Name { get; set; } 

        [SemanticProperty("e:image")]
        public MediaItem Image { get; set; }

        [SemanticProperty("e:imageUrl")]
        public string ImageUrl { get; set; }

        [SemanticProperty("e:parameters")]
        public string Parameters { get; set; }
    }
}