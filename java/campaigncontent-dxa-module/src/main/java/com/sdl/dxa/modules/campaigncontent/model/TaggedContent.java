package com.sdl.dxa.modules.campaigncontent.model;

import com.sdl.webapp.common.api.mapping.semantic.annotations.SemanticEntity;
import com.sdl.webapp.common.api.mapping.semantic.annotations.SemanticProperty;
import com.sdl.webapp.common.api.model.RichText;
import com.sdl.webapp.common.api.model.entity.AbstractEntityModel;

import static com.sdl.webapp.common.api.mapping.semantic.config.SemanticVocabulary.SDL_CORE;

/**
 * Tagged Content
 *
 * @author nic
 */
@SemanticEntity(entityName = "TaggedContent",  vocabulary = SDL_CORE, prefix = "e", public_ = false)
public class TaggedContent extends AbstractEntityModel {

    @SemanticProperty("e:name")
    private String name;

    @SemanticProperty("e:content")
    private RichText content;

    public RichText getContent() {
        return content;
    }

    public void setContent(RichText content) {
        this.content = content;
    }

    public String getName() {
        return name;
    }
}
