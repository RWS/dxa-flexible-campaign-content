using Sdl.Web.Common.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SDL.DXA.Modules.CampaignContent.Models
{
    [SemanticEntity(EntityName = "TaggedProperty", Vocab = CoreVocabulary, Prefix = "e")]
    public class TaggedProperty : EntityModel
    {
        [SemanticProperty("e:name")]
        public string Name { get; set; }

        [SemanticProperty("e:value")]
        public string Value { get; set; }

        [SemanticProperty("e:target")]
        public string Target { get; set; }
    }
}