﻿using System;
using System.Collections.Specialized;
using System.Linq;
using System.Web;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Entities.Portals;
using Newtonsoft.Json;
using ToSic.Eav.BLL;
using ToSic.SexyContent.DataSources;
using ToSic.SexyContent.Engines;
using ToSic.SexyContent.Environment.Dnn7;
using ToSic.SexyContent.Environment.Interfaces;
using ToSic.SexyContent.Interfaces;

namespace ToSic.SexyContent
{
    /// <summary>
    /// This is an instance-context of a Content-Module. It basically encapsulates the instance-state, incl.
    /// IDs of Zone & App, the App, EAV-Context, Template, Content-Groups (if available), Environment and OriginalModule (in case it's from another portal)
    /// It is needed for just about anything, because without this set of information
    /// it would be hard to get anything done .
    /// Note that it also adds the current-user to the state, so that the system can log data-changes to this user
    /// </summary>
    public class SxcInstance :ISxcInstance
    {
        #region App-level information

        /// <summary>
        /// The Content Data Context pointing to a full EAV, pre-configured for this specific App
        /// </summary>
        public EavDataController EavAppContext => App.EavContext;

        internal int? ZoneId => ContentBlock.ZoneId;

        internal int? AppId => ContentBlock.AppId;

        public App App => ContentBlock.App;

        public bool IsContentApp => ContentBlock.IsContentApp;

        public TemplateManager AppTemplates => App.TemplateManager; // todo: remove, use App...

        public ContentGroupManager AppContentGroups => App.ContentGroupManager; // todo: remove, use App...

        #endregion

        #region Info for current runtime instance
        public ContentGroup ContentGroup => ContentBlock.ContentGroup;


        /// <summary>
        /// Environment - should be the place to refactor everything into, which is the host around 2sxc
        /// </summary>
        public Environment.Environment Environment = new Environment.Environment();

        internal ModuleInfo ModuleInfo { get; }

        internal IContentBlock ContentBlock { get; }


        /// <summary>
        /// This returns the PS of the original module. When a module is mirrored across portals,
        /// then this will be different from the PortalSettingsOfVisitedPage, otherwise they are the same
        /// </summary>
        internal PortalSettings AppPortalSettings => ContentBlock.PortalSettings; // maybe pass in

        public ViewDataSource Data => ContentBlock.Data;


        #endregion

        #region  template helpers 

        public Template Template
        {
            get { return ContentBlock.Template; }
            set { ContentBlock.Template = value; }
        }

        private void CheckTemplateOverrides()
        {
            // skif if not relevant or not yet initialized
            if (IsContentApp || App == null) return;

            // #2 Change Template if URL contains the part in the metadata "ViewNameInUrl"
            var urlParams = HttpContext.Current.Request.QueryString; // todo: reduce dependency on context-current...
            var templateFromUrl = TryToGetTemplateBasedOnUrlParams(urlParams);
            if (templateFromUrl != null)
                Template = templateFromUrl;
        }

        private Template TryToGetTemplateBasedOnUrlParams(NameValueCollection urlParams)
        {
            var urlParameterDict = urlParams.AllKeys.ToDictionary(key => key?.ToLower() ?? "", key =>
                $"{key}/{urlParams[key]}".ToLower());

            foreach (var template in App.TemplateManager.GetAllTemplates().Where(t => !string.IsNullOrEmpty(t.ViewNameInUrl)))
            {
                var desiredFullViewName = template.ViewNameInUrl.ToLower();
                if (desiredFullViewName.EndsWith("/.*"))   // match details/.* --> e.g. details/12
                {
                    var keyName = desiredFullViewName.Substring(0, desiredFullViewName.Length - 3);
                    if (urlParameterDict.ContainsKey(keyName))
                        return template;
                }
                else if (urlParameterDict.ContainsValue(desiredFullViewName)) // match view/details
                    return template;
            }

            return null;
        }

        #endregion

        #region Constructor
        internal SxcInstance(IContentBlock cb, ModuleInfo runtimeModuleInfo)
        {
            ContentBlock = cb;
            ModuleInfo = runtimeModuleInfo;
            // Build up the environment. If we know the module context, then use permissions from there
            Environment.Permissions = (ModuleInfo != null)
                ? (IPermissions)new Permissions(ModuleInfo)
                : new Environment.None.Permissions();

            // url-override of view / data
            CheckTemplateOverrides();   // allow view change on apps
        }

        internal SxcInstance(IContentBlock cb, SxcInstance runtimeInstance)
        {
            ContentBlock = cb;
            ModuleInfo = runtimeInstance.ModuleInfo;
            // Build up the environment. If we know the module context, then use permissions from there
            Environment.Permissions = runtimeInstance.Environment.Permissions;

            // url-override of view / data
            CheckTemplateOverrides();   // allow view change on apps
        }
        #endregion

        #region RenderEngine
        private bool RenderWithDiv = true;
        private bool RenderWithEditMetadata => Environment.Permissions.UserMayEditContent;
        public HtmlString Render()
        {
            var renderHelp = new RenderingHelpers(this);

            try
            {
                string innerContent = null;

                #region do pre-check to see if system is stable & ready
                var notReady = Installer.CheckUpgradeMessage(PortalSettings.Current.UserInfo.IsSuperUser);
                if (!string.IsNullOrEmpty(notReady))
                    innerContent = renderHelp.DesignErrorMessage(new Exception(notReady), true, "Error - needs admin to fix this", false, false);

                #endregion

                #region try to render the block or generate the error message
                if(innerContent == null)
                    try
                    {
                        if (Template != null) // when a content block is still new, there is no definition yet
                        {
                            var engine = GetRenderingEngine(InstancePurposes.WebView);
                            innerContent = engine.Render();
                        }
                        else innerContent = "";
                    }
                    catch (Exception ex)
                    {
                        innerContent = renderHelp.DesignErrorMessage(ex, true, "Error rendering template", false, true);
                    }
                #endregion

                #region Wrap it all up into a nice wrapper tag
                var editInfos = JsonConvert.SerializeObject(renderHelp.GetClientInfosAll());
                var startTag = (RenderWithDiv
                    ? $"<div class=\"sc-viewport sc-content-block\" data-cb-instance=\"{ContentBlock.ParentId}\" " +
                      $" data-cb-id=\"{ContentBlock.ContentBlockId}\""
                      +
                      (RenderWithEditMetadata
                          ? " data-edit-context=\'" + editInfos + "'"
                          : "")
                      + ">\n"
                    : "");
                var endTag = (RenderWithDiv ? "\n</div>" : "");
                string result = startTag + innerContent + endTag;
                #endregion

                return new HtmlString(result);
            }
            catch (Exception ex)
            {
                // todo: i18n
                return new HtmlString(renderHelp.DesignErrorMessage(ex, true, null, true, true));
            }
        }

        public IEngine GetRenderingEngine(InstancePurposes renderingPurpose)
        {
            var engine = EngineFactory.CreateEngine(Template);
            engine.Init(Template, App, ModuleInfo, Data, renderingPurpose, this);
            return engine;
        }

        #endregion
    }
}