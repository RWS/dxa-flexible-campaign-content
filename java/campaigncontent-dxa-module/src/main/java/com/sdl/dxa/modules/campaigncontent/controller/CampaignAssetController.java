package com.sdl.dxa.modules.campaigncontent.controller;


import com.sdl.dxa.modules.campaigncontent.provider.CampaignAssetProvider;
import com.sdl.webapp.common.api.WebRequestContext;
import com.sdl.webapp.common.api.content.ContentProviderException;
import com.sdl.webapp.common.api.localization.Localization;
import lombok.extern.slf4j.Slf4j;
import org.apache.commons.io.IOUtils;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Controller;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RequestMethod;

import javax.servlet.ServletContext;
import javax.servlet.http.HttpServletRequest;
import javax.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.io.InputStream;

import static com.google.common.net.HttpHeaders.*;
import static javax.servlet.http.HttpServletResponse.SC_NOT_MODIFIED;
import static javax.servlet.http.HttpServletResponse.SC_OK;

@Controller
//TODO: When on Spring3.2+ use variables in the request mapping
//@RequestMapping("${instantcampaign.asset.baseUrl:/assets/campaign}")
@RequestMapping("/assets/campaign")
@Slf4j
public class CampaignAssetController {

    private static final String CACHE_CONTROL_PREFIX = "public, max-age=";

    // TODO: Need to handle localization URLs here as well!!!!!!!

    @Autowired
    private WebRequestContext webRequestContext;

    @Autowired
    private ServletContext servletContext;

    @Autowired
    private CampaignAssetProvider campaignAssetProvider;

    @Value("${instantcampaign.assetCacheMaxAge:3600}")
    private long assetCacheMaxAge;

    /**
     * Get campaign asset
     * @param request
     * @param response
     * @throws ContentProviderException
     * @throws IOException
     */
    @RequestMapping(method = RequestMethod.GET, value = "/**")
    public void getAsset(HttpServletRequest request, HttpServletResponse response) throws ContentProviderException, IOException {

        // TODO: Find a better solution for this!!!!
        String url = request.getRequestURI().replace("/assets/campaign", "");
        int index = url.indexOf("/", 1);
        String campaignId = url.substring(1, index);
        String assetUrl = url.substring(index);

        Localization localization = webRequestContext.getLocalization();
        String mimeType = servletContext.getMimeType(assetUrl);
        response.setContentType(mimeType);
        long lastModified = this.campaignAssetProvider.getLastModified(campaignId, localization);
        if ( isToBeRefreshed(request, response, lastModified) ) {
            InputStream inputStream = this.campaignAssetProvider.getAsset(localization, campaignId, assetUrl);
            IOUtils.copy(inputStream, response.getOutputStream());
            response.flushBuffer();
        }

    }

    /**
     * Check if the campaign asset needs to refresh or not (based on the campaign item last modified date)
     * @param request
     * @param response
     * @param lastModified
     * @return
     */
    private  boolean isToBeRefreshed(HttpServletRequest request, HttpServletResponse response, long lastModified) {

        long notModifiedSince = request.getDateHeader(IF_MODIFIED_SINCE);

        response.setHeader(CACHE_CONTROL, CACHE_CONTROL_PREFIX + this.assetCacheMaxAge);
        response.setDateHeader(EXPIRES, System.currentTimeMillis() + this.assetCacheMaxAge * 1000L);
        response.setDateHeader(LAST_MODIFIED, lastModified);

        if (lastModified == -1 || lastModified > notModifiedSince + 1000L) {
            response.setStatus(SC_OK);
            return true;
        } else {
            response.setStatus(SC_NOT_MODIFIED);
            return false;
        }
    }

}
