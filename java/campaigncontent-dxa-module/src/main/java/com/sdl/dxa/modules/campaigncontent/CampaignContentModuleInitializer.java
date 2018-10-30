package com.sdl.dxa.modules.campaigncontent;

import com.sdl.dxa.modules.campaigncontent.model.CampaignContentZIP;
import com.sdl.webapp.common.api.mapping.views.*;
import com.sdl.webapp.common.api.model.page.DefaultPageModel;
import org.springframework.stereotype.Component;

/**
 * Campaign Content Module Initializer
 *
 * @author nic
 */
@Component
@ModuleInfo(name = "Campaign Content Module", areaName = "CampaignContent", description = "Flexible Campaign Content Module")
@RegisteredViewModels({
        @RegisteredViewModel(viewName = "CampaignContentZIP", modelClass = CampaignContentZIP.class, controllerName = "CampaignContent"),
        @RegisteredViewModel(viewName = "CampaignPage", modelClass = DefaultPageModel.class),
})
public class CampaignContentModuleInitializer extends AbstractModuleInitializer {

    private static final String AREA_NAME = "CampaignContent";

    @Override
    protected String getAreaName() {
        return AREA_NAME;
    }
}
