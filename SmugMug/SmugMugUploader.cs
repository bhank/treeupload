using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using OAuth;

namespace coynesolutions.treeupload
{
    public class SmugMugUploader : IUploader
    {
        private static string ApiKey = ConfigurationManager.AppSettings["SmugMug_Key"];
        private static string ApiSecret = ConfigurationManager.AppSettings["SmugMug_Secret"];
        private static string OAuthToken = ConfigurationManager.AppSettings["SmugMug_OAuthToken"];
        private static string OAuthSecret = ConfigurationManager.AppSettings["SmugMug_OAuthSecret"];

        private const string RequestTokenUrl = "http://api.smugmug.com/services/oauth/1.0a/getRequestToken";
        private const string UserAuthorizationUrl = "http://api.smugmug.com/services/oauth/1.0a/authorize";
        private const string AccessTokenUrl = "http://api.smugmug.com/services/oauth/1.0a/getAccessToken";

        private static Manager OAuthManager = new Manager(ApiKey,ApiSecret,OAuthToken,OAuthSecret);

        private Lazy<dynamic> authUserJsonLazy;
        private Lazy<IFolder> rootFolderLazy;

        public SmugMugUploader()
        {
            authUserJsonLazy = new Lazy<dynamic>(() => RequestJson("/api/v2!authuser?_filter=NickName&_filteruri=Node&_verbosity=1"));
            rootFolderLazy = new Lazy<IFolder>(() => SmugMugFolder.LoadFromNodeUri((string)AuthUserJson.Uris.Node + "?_verbosity=1"));
        }

        public bool LogIn()
        {
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

        private const string BaseUrl = "https://api.smugmug.com";
        private static HashSet<string> urlsSeen = new HashSet<string>();

        public static dynamic RequestJson(string urlFormat, params object[] args)
        {
            if (!urlFormat.Contains("_verbosity=1"))
            {
                throw new Exception("Not verbose enough for me...");
            }
            var url = BaseUrl + string.Format(urlFormat, args);
            if (urlsSeen.Contains(url))
            {
                Debug.WriteLine("DUPE URL! " + url);
            }
            else
            {
                urlsSeen.Add(url);
            }
            var request = (HttpWebRequest) WebRequest.Create(url);
            request.Accept = "application/json";
            request.Headers.Add("Authorization", OAuthManager.GenerateAuthzHeader(url, "GET"));
            Debug.WriteLine(url);
            var response = (HttpWebResponse) request.GetResponse();
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var responseJson = reader.ReadToEnd();
                //Debug.WriteLine(JsonHelper.FormatJson(responseJson));
                return JsonConvert.DeserializeObject(responseJson);
            }
        }

        public string[] SupportedExtensions
        {
            get { return new[] {".jpg", ".jpeg", ".gif", ".png", ".avi", ".mov", ".wmv", ".mpg", ".mpeg", ".mp4", ".flv"}; }
        }
    }
}
