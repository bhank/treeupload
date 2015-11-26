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
    public abstract class SmugMugBase
    {
        private static readonly string ApiKey = ConfigurationManager.AppSettings["SmugMug_Key"];
        private static readonly string ApiSecret = ConfigurationManager.AppSettings["SmugMug_Secret"];
        private static readonly string OAuthToken = ConfigurationManager.AppSettings["SmugMug_OAuthToken"];
        private static readonly string OAuthSecret = ConfigurationManager.AppSettings["SmugMug_OAuthSecret"];

        private const string RequestTokenUrl = "http://api.smugmug.com/services/oauth/1.0a/getRequestToken";
        private const string UserAuthorizationUrl = "http://api.smugmug.com/services/oauth/1.0a/authorize";
        private const string AccessTokenUrl = "http://api.smugmug.com/services/oauth/1.0a/getAccessToken";

        private const string UploadUrl = "http://upload.smugmug.com/";

        private static readonly Manager OAuthManager = new Manager(ApiKey,ApiSecret,OAuthToken,OAuthSecret);


        private const string BaseUrl = "https://api.smugmug.com";
        private static readonly HashSet<string> urlsSeen = new HashSet<string>(); // TODO: remove this debugging thing... it'll suck up some memory on huge uploads

        protected static dynamic RequestJson(string urlFormat, params object[] args)
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
            request.Headers.Add("Authorization", OAuthManager.GenerateAuthzHeader(url, request.Method));
            Debug.WriteLine(url);
            var response = (HttpWebResponse) request.GetResponse();
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var responseJson = reader.ReadToEnd();
                //Debug.WriteLine(JsonHelper.FormatJson(responseJson));
                return JsonConvert.DeserializeObject(responseJson);
            }
        }

        protected static dynamic PostJson(object postData, string urlFormat, params object[] args)
        {
            var url = BaseUrl + string.Format(urlFormat, args);
            Debug.WriteLine(url);

            var request = (HttpWebRequest) WebRequest.Create(url);
            request.Method = "POST";
            request.Accept = "application/json";
            request.ContentType = "application/json";
            request.Headers.Add("Authorization", OAuthManager.GenerateAuthzHeader(url, request.Method));

            using (var writer = new StreamWriter(request.GetRequestStream()))
            {
                writer.Write(JsonConvert.SerializeObject(postData));
            }

            var response = (HttpWebResponse) request.GetResponse();
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var responseJson = reader.ReadToEnd();
                return JsonConvert.DeserializeObject(responseJson);
            }
        }

        public IEnumerable<string> SupportedExtensions
        {
            get { return new[] {".jpg", ".jpeg", ".gif", ".png", ".avi", ".mov", ".wmv", ".mpg", ".mpeg", ".mp4", ".flv"}; }
        }

        public bool Upload(string file, SmugMugFolder folder)
        {
            var albumUri = folder.AlbumUri;
            var fileInfo = new FileInfo(file);

            var request = (HttpWebRequest) WebRequest.Create(UploadUrl);
            request.Method = "POST";
            //request.Method = "PUT"; // maybe? nope...
            request.Accept = "application/json";
            request.ContentLength = fileInfo.Length;

            request.ReadWriteTimeout = 6 * 60 * 60 * 1000; // six hours, in milliseconds
            request.Timeout = 6 * 60 * 60 * 1000; // six hours, in milliseconds
            request.KeepAlive = false;


            request.Headers.Add("Authorization", OAuthManager.GenerateAuthzHeader(UploadUrl, request.Method));
            request.Headers.Add("Content-MD5", GetMd5(file));
            request.Headers.Add("X-Smug-Version", "v2");
            request.Headers.Add("X-Smug-ResponseType", "JSON");
            request.Headers.Add("X-Smug-AlbumUri", albumUri);
            request.Headers.Add("X-Smug-FileName", fileInfo.Name);

            Debug.WriteLine("{0} - uploading {1} ({2} bytes)", UploadUrl, file, fileInfo.Length);

            using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 0x2000))
            {
                using (var requestStream = request.GetRequestStream())
                {
                    fileStream.CopyTo(requestStream); // TODO: it would be cool to show progress here... time countdown and all
                    requestStream.Close();
                }
            }

            Debug.WriteLine("Upload complete -- getting response");

            var response = (HttpWebResponse) request.GetResponse();
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var responseJson = reader.ReadToEnd();
                dynamic responseData = JsonConvert.DeserializeObject(responseJson);
                if (responseData.stat == "ok")
                {
                    Debug.WriteLine("Upload succeeded");
                    return true;
                }
                else
                {
                    // http://dgrin.com/showthread.php?t=253383 - bad permissions for the app (from the initial oauth request) will cause it to fail with code 5, message "system error"
                    Debug.WriteLine("WARNING: Upload failed!");
                    Debug.WriteLine(JsonHelper.FormatJson(responseJson));
                    return false;
                }
            }
        }

        private static string GetMd5(string file)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(file))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-","").ToLower();
                }
            }
        }
    }
}
