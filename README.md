DXA Flexible Campaign Content
===============================

Introduction
--------------

The flexible campaign content extension make it possible for digital agencies to create campaigns (HTML, CSS,JS, images etc)
in their own tool suites. When ready they can package all campaign assets into a ZIP and upload it into SDL Web.
The benefit of this extension is that digital agencies are given the freedom (within some defined boundaries of the brand guidelines, used CSS framework etc)
to build campaign content with unique layout and interaction elements. And that without the need of creating specific templates in SDL Web for the created campaign.

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
* At upload of the campaign ZIP all editable content are extracted as rich text fields into SDL Web. These fields can then be modified and localized.
* The campaign component in SDL Web can sent for translation using the SDL Web translation connectors for WorldServer, TMS or BeGlobal. 
* A DXA module for both DXA.Java and DXA.NET is available to be to render the uploaded campaigns.
* The DXA module will extract all campaign assets and make them available. All assets links will be rewritten in the campaign markup to unique URLs for the published campaign.
* All editable content will be merged into the campaign markup.
* If DXA runs in a staging mode additional XPM markup will be generated around the editable content to make it inline editable.


Installation
--------------

Follow the below steps to install this extension in SDL Web CMS and DXA.

CMS:
1. Either compile the C# code in the 'cms/campaign-upload-extension' directory or download the pre-compiled DDL here:
   [campaign-upload-extension-v1.0.0.dll](https://github.com/sdl/dxa-flexible-campaign-content/raw/master/cms/campaign-upload-extension/compiled/campaign-upload-extension-v1.0.0.dll)
   
2. Upload the DLL to your SDL Web server and place it somewhere local on the server.
   Then add the following in your %SDLWEB_HOME%\config\Tridion.ContentManager.config in <extensions> tag:
   
   ```
   <add assemblyFileName="[PATH TO DLL]\campaign-upload-extension-v1.0.0.dll"/>
   ```
   
3. After that restart the services 'SDL Web Content Manager Service Host' and 'SDL Web Transport Distributor Service'
4. Import the needed schemas and templates by following the instructions given here: [CMS import script](./cms/import/README.md)
5. If you want to use the SDL Web Translation Manager for translating the campaigns you have to open up the embedded schema 'TaggedContent'. And there mark all fields as translatable.

DXA.NET:

T.B.D.


DXA.Java:

T.B.D.


Branching model
----------------

We intend to follow Gitflow (http://nvie.com/posts/a-successful-git-branching-model/) with the following main branches:

 - master - Stable 
 - develop - Unstable
 - release/x.y - Release version x.y

Please submit your pull requests on develop. In the near future we intend to push our changes to develop and master from our internal repositories, so you can follow our development process.


License
---------
Copyright (c) 2017 SDL Group.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

	http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and limitations under the License.

