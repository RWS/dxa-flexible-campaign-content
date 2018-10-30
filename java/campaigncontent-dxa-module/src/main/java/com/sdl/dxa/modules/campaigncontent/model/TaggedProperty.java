package com.sdl.dxa.modules.campaigncontent.model;

import com.sdl.webapp.common.api.mapping.semantic.annotations.SemanticEntity;
import com.sdl.webapp.common.api.mapping.semantic.annotations.SemanticProperty;
import com.sdl.webapp.common.api.model.entity.AbstractEntityModel;
import com.sdl.webapp.common.api.model.entity.MediaItem;

import static com.sdl.webapp.common.api.mapping.semantic.config.SemanticVocabulary.SDL_CORE;

@SemanticEntity(entityName = "TaggedProperty",  vocabulary = SDL_CORE, prefix = "e", public_ = false)
public class TaggedProperty extends AbstractEntityModel {

    @SemanticProperty("e:name")
    private String name;

    @SemanticProperty("e:value")
    private String value;

    @SemanticProperty("e:target")
    private String target;

    @SemanticProperty("e:index")
    private Integer index;

    @SemanticProperty("e:image")
    private MediaItem image;

    @SemanticProperty("e:imageAltText")
    private String imageAltText;

    public String getName() {
        return name;
    }

    public String getValue() {
        return value;
    }

    public String getTarget() {
        return target;
    }

    public Integer getIndex() {
        return index;
    }

    public MediaItem getImage() {
        return image;
    }

    public String getImageAltText() {
        return imageAltText;
    }
}
