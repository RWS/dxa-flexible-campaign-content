Instant Campaign 
=====================================

Introduction
--------------

The Tridion Sites Instant Campaign extension (also known as DXA Flexible Campaign Content) make it possible for digital agencies to create HTML based campaigns
in their own tool suites. When ready they can package all campaign assets (HTML, CSS,JS, images etc) into a ZIP and upload it into SDL Tridion Sites.
The benefit of this extension is that digital agencies are given the freedom (within some defined boundaries of the brand guidelines, used CSS framework etc)
to build campaign content with unique layout and interaction elements. And that without the need of creating specific templates in SDL Tridion Sites for the created campaign.

New functionality in v1.3:
* Support for DXA 2.2 and SDL Tridion Sites 9.0/9.1/9.5
* Performance improvements
* Support for 9.1/9.5 Addon Service

New functionality in v1.3.2:
* Support for updating existing campaigns with new fields from the campaign

A complete distribution is found on SDL AppStore:  https://appstore.sdl.com/web-content-management/app/instant-campaign/748/

Functionality
---------------

* A campaign is created within the digital agency’s tool suite within defined boundaries set by the customer such as branding guidelines, CSS and Javascript frameworks etc.
* When the agency is ready with the campaign, it is bundled in a ZIP with all its assets (CSS, Javascripts, images etc). The ZIP should at least include an index.html with all markup and references to the included assets. In addition the digital agency can attach a header.html and footer.html to be included on the header & footer of the DXA campaign page. These files normally consist of CSS and script includes. Example:

    ```
    <script src="scripts/jquery.animateSprite.min.js"></script>
    <script src="scripts/jquery-waypoints.all.min.js"></script>
    <script src="scripts/jquery.lazyloadxt.min.js"></script>
    ```

* The digital agency mark content that can be editable in the HTML markup by using the following HTML attribute: ‘data-content-name’. Example:

	```
		<div data-content-name="section4-quote" class="valign-middle">
		    <h1>Think layer on layer to achieve the triple denim look</h1>
		</div>
	```
* At upload of the campaign ZIP all editable content are extracted as rich text fields into SDL Tridion Sites. These fields can then be modified and localized.
* The digital agency can also mark certain images so they can be managed by the CMS using the attribute 'data-image-name'.

  ```
    <div class="hero">
        <img data-image-name="img/default-hero-image.png"></img>
    </div>
  ```
* The marked images are uploaded automatically into SDL Tridion Sites and can be replaced by an editor. This applies to all non-absolute image URLs. For absolute URLs the image URL is stored in the CMS plus eventual URL parameters. The image with absolute URL can then be replaced by the editor with a CMS/ECL image.
* Links can also be tagged so they can modified in SDL Tridion Sites either by using a static URL or a dynamic component link. SDL Tridion Sites will extract all links using the data property 'data-link-name'. Example:

  ```
   <div>
      <a href="https://en.wikipedia.org/wiki/Monument_Valley" data-link-name="monument-valley-link"><span data-content-name="monument-valley-link-text">Read more</span></a>
   </div>
  ```
* To build reusable campaigns the markup can be parameterized by using tagged properties. This done by the HTML attributes 'data-property-name' and 'data-property-target'. Currently the properties can operate on any HTML element attribute. Example:

  ```
    <div class="hero" data-property-name="hero-background-style" data-property-target="style" style="background-image: url(img/default-background-image.jpg);">
        <h1>Welcome!</h1>
    </div>
  ```
* It is possible to have several properties on one single HTML element. For the additional properties you postfix the data attribute with an index, e.g. 'data-property-name-2' and 'data-property-target-2'. Example of markup:

```
  <div
      data-property-name="hero-background-style"
      data-property-target="style"
      style="background-image: url(img/default-background-image.jpg);"
      data-property-name-2="hero-class"
      data-property-target-2="class"
      class="hero">
      <img data-image-name="img/default-hero-image.png"></img>
  </div>
```
* The image URLs used in property values can also be selected from Tridion. Then you need to use the inline property `%URL%` when you configure the value in Tridion. For example: `background-image: url(%URL%);`. It is also possible to select an CMS image to be used as value for the '%URL%'-parameter.  
* The data attribute 'data-preview-only=true' can be used for markup that should only be shown on staging sites
* The campaign component in SDL Tridion Sites can sent for translation using the translation connectors for WorldServer, TMS or BeGlobal.
* A DXA module for both DXA.Java and DXA.NET is available to be to render the uploaded campaigns.
* The DXA module will extract all campaign assets and make them available. All assets links will be rewritten in the campaign markup to unique URLs for the published campaign.
* All editable content will be merged into the campaign markup.
* If DXA runs in a staging mode additional XPM markup will be generated around the editable content to make it inline editable.

The extension has been verified both on SDL Tridion Sites 9.0, 9.1 and 9.5 using DXA 2.2.

Installation
--------------

Follow the below steps to install this extension in SDL Tridion Sites CMS and DXA.

Tridion Sites CMS:

1. Either compile the C# code in the 'cms/campaign-upload-extension' directory or download the precompiled Addon package/DLL here:
    - DLL for Tridion Sites 9.0: [campaign-upload-extension-v1.3.2.dll](https://github.com/rws/dxa-flexible-campaign-content/raw/master/cms/campaign-upload-extension/compiled/campaign-upload-extension-v1.3.2.dll)
    - Addon package for Tridion Sites 9.1/9.5: [InstantCampaign-1.3.2.zip](https://github.com/rws/dxa-flexible-campaign-content/raw/master/cms/campaign-upload-extension/compiled/InstantCampaign-1.3.2.zip)


2. If you compile the extension yourself for Sites 9.0 you need to merge the DLLs into one single DLL by using [ILMerge](https://www.microsoft.com/en-us/download/details.aspx?id=17630). Use the merge_dll.bat to generate a merged DLL. For Sites 9.1 and above an Addon package ZIP will automatically be generated when building.

3. Sites 9.1/9.5: Upload the Addon package to the Addon Service. The campaign upload extension is automatically installed in SDL Tridion Sites.
4. Sites 9.0: Upload the DLL to your SDL Tridion Sites server and place it somewhere local on the server. Do not forget to unblock the DLL to avoid assembly loading issues.
   Then add the following in your %SDLWEB_HOME%\config\Tridion.ContentManager.config in <extensions> tag:

   ```
   <add assemblyFileName="[PATH TO DLL]\campaign-upload-extension-v1.3.2.dll"/>
   ```

5. Sites 9.0: After that restart the services 'SDL Web Content Manager Service Host' and 'SDL Web Transport Distributor Service'
6. Import the needed schemas and templates by following the instructions given here: [CMS import script](./cms/import/README.md)
7. If you want to use the SDL Tridion Sites Translation Manager for translating the campaigns you have to open up the embedded schema 'Campaign Content - TaggedContent'. And there mark the field 'content' as translatable.

DXA.NET:

1. If you do not have a DXA.NET setup (for Tridion Sites 9.x) you can easily do this by following the instructions given here: [Installing the web application (.NET)](https://docs.sdl.com/784837/748556/sdl-digital-experience-accelerator-2-2/installing-the-------------dxa--net-web-application-for-------------tridion-sites)
2. Either open up the solution 'dotnet/SDL.DXA.Modules.CampaignContent.sln' or add the VS project under the directory 'dotnet' to your Visual Studio solution
4. Set the environment variable %DXA_SITE_DIR% to point to your DXA Site path (in visual studio or in your IIS instance)
5. Restart Visual studio and rebuild the solution. Verify so the CampaignContent Area and DLLs are copied to your site folder
6. Now is your DXA instance ready for rendering of flexible campaign content


DXA.Java:

1. Install the DXA module in your local Maven repository by doing the following in the directory 'java':

   ```
    mvn install
   ```

2. Add the DXA module as Maven dependency to your web application POM file:

   ```
    <dependencies>
        ...
        <dependency>
            <groupId>com.sdl.dxa.modules.campaigncontent</groupId>
            <artifactId>campaigncontent-dxa-module</artifactId>
            <version>1.3.2</version>
        </dependency>

    </dependencies>
   ```

3. Package your webapp and deploy into your JEE application server
4. Now is your DXA instance ready for rendering of flexible campaign content

In addition there is a example DXA2.2 webapp including Instant Campaign available in the directory 'java/campaigncontent-dxa-webapp'.

Getting started
------------------

To quickly getting started you can follow the steps below:

1. Create a new page type using the new 'Campaign Page' page template
2. Create a new multimedia component in your content structure (for example under Content/Campaigns) using the multimedia schema 'Campaign Content ZIP'
3. Upload the [Example Campaign ZIP](https://github.com/rws/dxa-flexible-campaign-content/raw/master/cms/example-campaign/ExampleCampaign.zip).
4. Save the multimedia component. All editable content is extracted from the campaign ZIP when saving it the first time.
5. Go to your staging site and open Experience Manager. Create a new campaign page with the newly created page type.
6. Select the uploaded campaign and drag & drop it on your page.
7. The whole campaign should now appear and all text are inline editable. It is also possible at this step to send the campaign directly for translation.


Creating campaign content
--------------------------

You can create new HTML campaigns in any HTML5 compliant design tool. A campaign ZIP package can contain the following:

* index.html - the main HTML markup for campaign content. Is not a full HTML page, only a HTML fragment for the actual campaign.
* header.html - additional CSS/JS to be included on top of the page (optional)
* footer.html - additional JS to be included on the bottom of the page (optional)
* All needed assets such as needed CSS and javascripts (both custom ones and standard 3PPs). Plus images and other assets that referred from the HTML/CSS. The assets can be in different folders if needed.

You have to add the following data attribute (on any HTML element) for all textual content that you want to editable and translatable:

```
    <h1 data-content-name="intro-text">Some texts some can be editable</h1>
```

To specify that an image is replaceable by the CMS you use the following data attribute on image elements:

```
   <div>
     <img data-image-name="booknow" class="book-image" src="images/booknow.png">
   </div>  
```

If you run into problems to make the image selectable in XPM, you can try to surround it with a div. That often solves the problem.

You can also use properties in your HTML markup that can be managed by CMS. The properties replace certain HTML element attribute values such as background image, CSS class/style etc. You use the following data attributes to accomplish that:

```
<div data-property-name="testimonials-class" data-property-target="class" class="default-testimonials">
    <p data-content-name="quote1" class="quote">"Best thing I've done in my life!"</p>
    <p class="quote-name">Andrew Smith, London</p>
  </div>
```

When uploading the campaign into SDL Tridion Sites all marked textual content, images and property variables are extracted into a list of embedded schema fields.
All non-absolute references to assets will be rewritten to unique campaign URLs on the DXA side.
The digital agency/internal web team should share the base HTML, CSS, JS which form the brand look & feel. Which is also used when develop and test
the campaign HTML. All CSS/JS not part of the base look&feel needs to be included in the campaign ZIP.

#### A simple example:

index.html:

```
   <div class="main">
      <div class="intro">
        <img src="images/image1.jpg"/>
        <h1 data-content-name="intro-text">Campaign intro</h1>
      </div>
      <div data-content-name="body-text">
        <h3>some rich content comes here</h3>
        <p>And some more text comes here. All this text can be modified in SDL Tridion Sites.</p>
      </div>   
   </div>
```

header.html:

```
    <link rel="stylesheet" href="css/jquery-ui.min.css">
    <link rel="stylesheet" href="css/example.css">
```

footer.html:
```
    <script src="scripts/jquery-ui.min.js"></script>
    <script src="scripts/example.js"></script>
```

example.css:
```
    .intro {
        background: url(../images/image2.jpg) no-repeat center center fixed;
        background-size: cover;
        padding-top: 100px;
    }
```

Files to be included the campaign ZIP:

- index.html
- header.html
- footer.html
- images/image1.jpg
- images/image2.jpg
- css/jquery-ui.css
- css/example.css
- scripts/jquery-ui.min.js
- scripts/example.js

A fully functional example campaign can be found here: [Example Campaign](./cms/example-campaign)

Other uses
-----------

The extension can possibly also be used to deploy SPA applications on web pages.


Future enhancements
--------------------

- Advanced regex expressions used in property variables
- Separate the metadata into a separate component to improve localisation
- Possibility to define a name space on the campaign data attributes

Branching model
----------------

We intend to follow Gitflow (http://nvie.com/posts/a-successful-git-branching-model/) with the following main branches:

 - master - Stable
 - develop - Unstable
 - release/x.y - Release version x.y

 All releases (including pre-releases and hotfix releases) are tagged.

 If you wish to submit a Pull Request, it should normally be submitted on the develop branch, so it can be incorporated in the upcoming release.

 Fixes for really severe/urgent issues (which qualify as hotfixes) should be submitted as Pull Request on the appropriate release branch.

 Please always submit an Issue for the problem and indicate whether you think it qualifies as a hotfix; Pull Requests on release branches will only be accepted after agreement on the severity of the issue. Furthermore, Pull Requests on release branches are expected to be extensively tested by the submitter.

 Of course, it's also possible (and appreciated) to report an Issue without associated Pull Requests.


License
---------
Copyright (c) 2015-2021 All Rights Reserved by the RWS Group for and on behalf of its affiliates and subsidiaries.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

	http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and limitations under the License.
