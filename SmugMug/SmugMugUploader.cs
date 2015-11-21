using System;
using System.Configuration;
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

        private dynamic AuthUserResponse
        {
            get { return new Lazy<dynamic>(() => RequestJson("!authuser?_filter=NickName&_filteruri=Node&_verbosity=1")).Value; }
        }

        public string NickName
        {
            get { return AuthUserResponse.Response.User.NickName; }
        }

        public IFolder RootFolder
        {
            get
            {
                return new Lazy<IFolder>(() => new SmugMugFolder(Path.GetFileName(AuthUserResponse.Response.User.Uris.Node))).Value;

            }
        }

        private const string BaseUrl = "https://api.smugmug.com/api/v2";

        public static dynamic RequestJson(string urlFormat, params object[] args)
        {
            var url = BaseUrl + string.Format(urlFormat, args);
            var request = (HttpWebRequest) WebRequest.Create(url);
            request.Accept = "application/json";
            request.Headers.Add("Authorization", OAuthManager.GenerateAuthzHeader(url, "GET"));
            var response = (HttpWebResponse) request.GetResponse();
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var responseJson = reader.ReadToEnd();
                return JsonConvert.DeserializeObject(responseJson);
            }
        }
    }
}
