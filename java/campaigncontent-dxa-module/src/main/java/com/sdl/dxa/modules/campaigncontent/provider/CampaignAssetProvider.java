package com.sdl.dxa.modules.campaigncontent.provider;

import com.sdl.dxa.modules.campaigncontent.model.CampaignContentZIP;
import com.sdl.webapp.common.api.content.ContentProvider;
import com.sdl.webapp.common.api.content.ContentProviderException;
import com.sdl.webapp.common.api.content.StaticContentItem;
import com.sdl.webapp.common.api.localization.Localization;
import lombok.extern.slf4j.Slf4j;
import org.apache.commons.lang3.StringUtils;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.stereotype.Component;
import org.springframework.web.context.WebApplicationContext;

import java.io.*;
import java.util.HashMap;
import java.util.Map;
import java.util.zip.ZipEntry;
import java.util.zip.ZipInputStream;

/**
 * Campaign Asset Provider
 */
@Component
@Slf4j
public class CampaignAssetProvider {

    @Autowired
    private ContentProvider contentProvider;

    @Autowired
    private WebApplicationContext webApplicationContext;

    private Map<String, CampaignContentMarkup> cachedMarkup = new HashMap<>();

    static final String STATIC_FILES_DIR = "BinaryData";

    /**
     * Get a specific asset in a campaign
     * @param localization
     * @param campaignId
     * @param assetUrl
     * @return asset input stream
     * @throws ContentProviderException
     */
    public InputStream getAsset(Localization localization, String campaignId, String assetUrl) throws ContentProviderException {

        try {
            log.debug("Getting campaign asset: " + assetUrl + " for campaign ID: " + campaignId);
            InputStream inputStream = new FileInputStream(new File(getBaseDir(localization, campaignId) + assetUrl));
            return inputStream;
        }
        catch ( IOException e ) {
            throw new ContentProviderException("Could not get asset: " + assetUrl + " for campaign: " + campaignId + " and localication ID: " + localization.getId(), e);
        }

    }

    /**
     * Get the markup (main, header, footer etc) for campaign.
     * @param campaignContentZip
     * @param localization
     * @return markup
     * @throws ContentProviderException
     */
    public CampaignContentMarkup getCampaignContentMarkup(CampaignContentZIP campaignContentZip, Localization localization) throws ContentProviderException {
        StaticContentItem zipItem = this.getZipItem(campaignContentZip, localization);

        String cacheKey = getMarkupCacheKey(campaignContentZip.getId(), localization);
        CampaignContentMarkup markup = this.cachedMarkup.get(cacheKey);
        File baseDir = this.getBaseDir(localization, campaignContentZip.getId());

        if( markup == null || !baseDir.exists() || !this.directoryHasFiles(baseDir) ) {
            try {
                markup = extractZip(zipItem, baseDir);
                markup.setLastModified(zipItem.getLastModified());
                this.cachedMarkup.put(cacheKey, markup);
            }
            catch ( IOException e ) {
                throw new ContentProviderException("Could not extract free format ZIP file.", e);
            }
        }
        return markup;
    }

    /**
     * Get last modified time on the campaign assets (not the content)
     * @param campaignId
     * @param localization
     * @return
     */
    public long getLastModified(String campaignId, Localization localization) {
        CampaignContentMarkup markup = this.cachedMarkup.get(getMarkupCacheKey(campaignId, localization));
        if ( markup != null ) {
            return markup.getLastModified();
        }
        else {
            return -1;
        }
    }

    private String getMarkupCacheKey(String campaignId, Localization localization) {
        return campaignId + "-" + localization.getId();
    }

    /**
     * Get physical base directory for the campaign content
     * @param localization
     * @param campaignId
     * @return base directory
     */
    protected File getBaseDir(Localization localization, String campaignId) {
        return new File(StringUtils.join(new String[]{
                webApplicationContext.getServletContext().getRealPath("/"), STATIC_FILES_DIR, localization.getId(), "campaign-content", campaignId
        }, File.separator));
    }

    /**
     * Get ZIP item
     * @param campaignContentZip
     * @param localization
     * @return
     * @throws ContentProviderException
     */
    protected StaticContentItem getZipItem(CampaignContentZIP campaignContentZip, Localization localization) throws ContentProviderException {
        return contentProvider.getStaticContent(campaignContentZip.getUrl(), localization.getId(), localization.getPath());
    }

    /**
     * Extract ZIP
     * @param zipItem
     * @param directory
     * @return HTML fragments
     * @throws IOException
     */
    protected CampaignContentMarkup extractZip(StaticContentItem zipItem, File directory) throws IOException {

        ByteArrayOutputStream htmlMarkup = new ByteArrayOutputStream();
        ByteArrayOutputStream headerMarkup = new ByteArrayOutputStream();
        ByteArrayOutputStream footerMarkup = new ByteArrayOutputStream();

        byte[] buffer = new byte[1024];

        directory.mkdir();

        // Get the zip file content
        //
        ZipInputStream zis =
                new ZipInputStream(zipItem.getContent());

        // Get the zipped file list entry
        //
        ZipEntry ze = zis.getNextEntry();

        while (ze!=null) {
            String fileName = ze.getName();
            File newFile = new File(directory + File.separator + fileName);

            // Create all non exists folders
            // else you will hit FileNotFoundException for compressed folder
            //
            if (ze.isDirectory()) {
                newFile.mkdirs();
            }
            else {
                newFile.getParentFile().mkdirs();
                newFile.createNewFile();
                FileOutputStream fos = new FileOutputStream(newFile);
                int len;
                while ((len = zis.read(buffer)) > 0) {
                    fos.write(buffer, 0, len);
                    if (fileName.equals("index.html") ) {
                        htmlMarkup.write(buffer, 0, len);
                    }
                    else if (fileName.equals("header.html") ) {
                        headerMarkup.write(buffer, 0, len);
                    }
                    else if (fileName.equals("footer.html") ) {
                        footerMarkup.write(buffer, 0, len);
                    }

                }
                fos.close();
            }
            ze = zis.getNextEntry();
        }
        zis.closeEntry();
        zis.close();

        CampaignContentMarkup campaignContentMarkup = new CampaignContentMarkup();
        if ( htmlMarkup.size() > 0 ) {
            campaignContentMarkup.setMainHtml(new String(htmlMarkup.toByteArray()));
        }
        if ( headerMarkup.size() > 0 ) {
            campaignContentMarkup.setHeaderHtml(new String(headerMarkup.toByteArray()));
        }
        if ( footerMarkup.size() > 0 ) {
            campaignContentMarkup.setFooterHtml(new String(footerMarkup.toByteArray()));
        }
        return campaignContentMarkup;
    }

    /**
     * Check if a specific directory has files
     * @param directory
     * @return
     */
    protected boolean directoryHasFiles(File directory) {
        String[] files = directory.list();
        return files != null && files.length > 0;

    }

}
