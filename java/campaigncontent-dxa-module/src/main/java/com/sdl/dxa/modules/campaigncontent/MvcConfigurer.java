package com.sdl.dxa.modules.campaigncontent;

import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.context.annotation.Configuration;
import org.springframework.web.servlet.config.annotation.InterceptorRegistry;
import org.springframework.web.servlet.config.annotation.WebMvcConfigurerAdapter;

/**
 * MVC Configurer.
 * Sets up additional configuration such as interceptors etc.
 *
 * @author nic
 */
@Configuration
public class MvcConfigurer extends WebMvcConfigurerAdapter {

    @Autowired
    private CampaignAssetLocalizationRedirectInterceptor campaignAssetLocalizationRedirectInterceptor;

    @Override
    public void addInterceptors(InterceptorRegistry registry) {
        registry.addInterceptor(campaignAssetLocalizationRedirectInterceptor);
    }
}
