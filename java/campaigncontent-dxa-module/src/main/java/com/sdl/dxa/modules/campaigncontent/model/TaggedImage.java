package com.sdl.dxa.modules.campaigncontent.model;

import com.sdl.webapp.common.api.mapping.semantic.annotations.SemanticEntity;
import com.sdl.webapp.common.api.mapping.semantic.annotations.SemanticProperty;
import com.sdl.webapp.common.api.model.entity.AbstractEntityModel;
import com.sdl.webapp.common.api.model.entity.MediaItem;

import static com.sdl.webapp.common.api.mapping.semantic.config.SemanticVocabulary.SDL_CORE;

@SemanticEntity(entityName = "TaggedImage",  vocabulary = SDL_CORE, prefix = "e", public_ = false)
public class TaggedImage extends AbstractEntityModel {

    @SemanticProperty("e:name")
    private String name;

    @SemanticProperty("e:image")
    private MediaItem image;

    public String getName() {
        return name;
    }

    public MediaItem getImage() {
        return image;
    }
}
