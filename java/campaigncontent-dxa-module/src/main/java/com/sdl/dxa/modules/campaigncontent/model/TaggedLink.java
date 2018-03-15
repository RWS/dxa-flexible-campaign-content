package com.sdl.dxa.modules.campaigncontent.model;

import com.sdl.webapp.common.api.mapping.semantic.annotations.SemanticEntity;
import com.sdl.webapp.common.api.mapping.semantic.annotations.SemanticProperty;
import com.sdl.webapp.common.api.model.entity.AbstractEntityModel;

import static com.sdl.webapp.common.api.mapping.semantic.config.SemanticVocabulary.SDL_CORE;

@SemanticEntity(entityName = "TaggedLink",  vocabulary = SDL_CORE, prefix = "e", public_ = false)
public class TaggedLink extends AbstractEntityModel {

    @SemanticProperty("e:name")
    private String name;

    @SemanticProperty("e:url")
    private String url;

    @SemanticProperty("e:componentLink")
    private String componentLink;

    public String getName() {
        return name;
    }

    public String getUrl() {
        return url;
    }

    public String getComponentLink() {
        return componentLink;
    }
}
