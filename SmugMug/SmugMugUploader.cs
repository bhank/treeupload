using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using Newtonsoft.Json;
using OAuth;

namespace coynesolutions.treeupload.SmugMug
{
    public class SmugMugUploader : SmugMugBase, IUploader
    {

        private Lazy<dynamic> authUserJsonLazy;
        private Lazy<IFolder> rootFolderLazy;

        public SmugMugUploader()
        {
            authUserJsonLazy = new Lazy<dynamic>(() => RequestJson("/api/v2!authuser?_filter=NickName&_filteruri=Node&_verbosity=1"));
            rootFolderLazy = new Lazy<IFolder>(() => SmugMugFolder.LoadFromNodeUri((string)AuthUserJson.Uris.Node + "?_verbosity=1"));
        }

        public bool LogIn()
        {
            // TODO: enable initial OAuth request and all

            throw new NotImplementedException();
            //if (string.IsNullOrEmpty(ApiKey))
            //{
            //    throw new Exception("API key is not defined in config!");
            //}
            //if (string.IsNullOrEmpty(ApiSecret))
            //{
            //    throw new Exception("API secret is not defined in config!");
            //}

            //var oauthManager = new OAuth.Manager(ApiKey,ApiSecret,OAuthToken,OAuthSecret);
            //if (string.IsNullOrEmpty(OAuthToken) || string.IsNullOrEmpty(OAuthSecret))
            //{
            //    // authorize!
            //    var requestToken = oauthManager.AcquireRequestToken(RequestTokenUrl, "POST");
            //    var url = oauthManager.GenerateCredsHeader("","",)
            //}
        }

        public bool LogOut()
        {
            throw new NotImplementedException();
        }

        private dynamic AuthUserJson
        {
            get { return authUserJsonLazy.Value.Response.User; }
        }

        public string NickName
        {
            get { return AuthUserJson.NickName; }
        }

        public IFolder RootFolder
        {
            get
            {
                return rootFolderLazy.Value;
            }
        }
    }
}
