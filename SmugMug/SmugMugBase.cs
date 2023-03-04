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

        private const string UploadUrl = "https://upload.smugmug.com/";

        private static readonly Manager OAuthManager = new Manager(ApiKey,ApiSecret,OAuthToken,OAuthSecret);


        private const string BaseUrl = "https://api.smugmug.com";
        private static readonly HashSet<string> urlsSeen = new HashSet<string>(); // TODO: remove this debugging thing... it'll suck up some memory on huge uploads

        protected static dynamic GetJson(string urlFormat, params object[] args)
        {
            return MakeRequest("GET", true, false, null, urlFormat, args);
        }

        protected static dynamic GetJsonWithRedirect(string urlFormat, params object[] args)
        {
            return MakeRequest("GET", true, true, null, urlFormat, args);
        }

        protected static IEnumerable<dynamic> GetPagedItems(Func<dynamic, IEnumerable<dynamic>> selector, string urlFormat, params object[] args)
        {
            var url = string.Format(urlFormat, args);
            while (true)
            {
                dynamic result = GetJson(url);
                foreach (var item in selector(result))
                {
                    yield return item;
                }
                if (result.Response.Pages == null || result.Response.Pages.NextPage == null)
                {
                    break;
                }
                url = (string)result.Response.Pages.NextPage + "&_verbosity=1";
            }
        }

        protected static dynamic PostJson(object postData, string urlFormat, params object[] args)
        {
            return MakeRequest("POST", true, false, postData, urlFormat, args);
        }

        protected static dynamic PatchJson(object patchData, string urlFormat, params object[] args)
        {
            return MakeRequest("PATCH", false, false, patchData, urlFormat, args);
        }

        protected static dynamic DeleteJson(string urlFormat, params object[] args)
        {
            return MakeRequest("DELETE", false, false, null, urlFormat, args);
        }

        private static dynamic MakeRequest(string requestMethod, bool allowAutoRedirect, bool resubmitOnRedirect, object requestJson, string urlFormat, params object[] args)
        {
            if (!urlFormat.Contains("_verbosity=1"))
            {
                throw new Exception("Not verbose enough for me...");
            }

            var url = BaseUrl + string.Format(urlFormat, args);
            Debug.WriteLine(url);

            var request = (HttpWebRequest) WebRequest.Create(url);
            request.AllowAutoRedirect = allowAutoRedirect;
            request.Accept = "application/json";

            request.Method = requestMethod;

            if (requestJson != null)
            {
                request.ContentType = "application/json";

                using (var writer = new StreamWriter(request.GetRequestStream()))
                {
                    writer.Write(JsonConvert.SerializeObject(requestJson));
                }
            }

            if(requestMethod == "GET")
            {
                if (urlsSeen.Contains(url))
                {
                    Debug.WriteLine("DUPE GET URL: " + url);
                }
                else
                {
                    urlsSeen.Add(url);
                }
            }

            request.Headers.Add("Authorization", OAuthManager.GenerateAuthzHeader(url, request.Method));


            var response = (HttpWebResponse) request.GetResponse();

            if(resubmitOnRedirect && (int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
            {
                var location = response.GetResponseHeader("Location");
                if (string.IsNullOrEmpty(location))
                {
                    return null;
                }
                //var newUrl = new Uri(new Uri(url), location).ToString(); // oops, I don't want/need it fully qualified
                return MakeRequest(requestMethod, true, false, requestJson, location);
            }

            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                var responseJson = reader.ReadToEnd();
                return JsonConvert.DeserializeObject(responseJson);
            }
        }

        public IEnumerable<string> SupportedExtensions
        {
            get { return new[] {".jpg", ".jpeg", ".gif", ".png", ".avi", ".mov", ".wmv", ".mpg", ".mpeg", ".mp4", ".flv", ".heic"}; }
        }

        protected static bool Upload(string file, SmugMugFolder folder)
        {
            var albumUri = folder.AlbumUri;
            var fileInfo = new FileInfo(file);
            if (fileInfo.Length == 0)
            {
                Debug.WriteLine("Zero-length file -- skipping");
                return false;
            }
            const Int64 maximumSmugmugSize = 3L * 1024 * 1024 * 1024;
            if(fileInfo.Length > maximumSmugmugSize)
            {
                Debug.WriteLine($"File too large for SmugMug ({fileInfo.Length} > {maximumSmugmugSize}) -- skipping");
                return false;
            }

            var request = (HttpWebRequest) WebRequest.Create(UploadUrl);
            request.Method = "POST";
            //request.Method = "PUT"; // maybe? nope...
            request.Accept = "application/json";
            request.ContentLength = fileInfo.Length;
            if (fileInfo.Length > 500000000)
            {
                request.AllowWriteStreamBuffering = false; // http://stackoverflow.com/questions/11640844/out-of-memory-exception-reading-large-text-file-for-httpwebrequest // https://support.microsoft.com/en-us/kb/908573
                // without this, it would freeze forever on request.GetRequestStream() below on an 817 MB file
            }

            request.ReadWriteTimeout = 6 * 60 * 60 * 1000; // six hours, in milliseconds
            request.Timeout = 6 * 60 * 60 * 1000; // six hours, in milliseconds
            request.KeepAlive = false;


            request.Headers.Add("Authorization", OAuthManager.GenerateAuthzHeader(UploadUrl, request.Method));
            request.Headers.Add("Content-MD5", GetMd5(file));
            request.Headers.Add("X-Smug-Version", "v2");
            request.Headers.Add("X-Smug-ResponseType", "JSON");
            request.Headers.Add("X-Smug-AlbumUri", albumUri);
            request.Headers.Add("X-Smug-FileName", fileInfo.Name);

            Debug.WriteLine("{0} - uploading {1} ({2} bytes) starting at {3}", UploadUrl, file, fileInfo.Length, DateTime.Now);
            
            using (var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 0x2000))
            {
                using (var requestStream = request.GetRequestStream())
                {
                    //fileStream.CopyTo(requestStream); // TODO: it would be cool to show progress here... time countdown and all
                    CopyStream(fileStream, requestStream, fileInfo.Length, folder.Uploader);
                    //requestStream.Close(); // dispose should handle that
                }
            }

            Debug.WriteLine("Upload complete -- getting response at {0}", DateTime.Now);

            HttpWebResponse response;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            catch(WebException e)
            {
                Debug.WriteLine("WebException at {0}", DateTime.Now);
                Debug.WriteLine(request.RequestUri.ToString());
                Debug.WriteLine("Request headers: " + request.Headers.ToString());
                Debug.WriteLine(e);
                if (e.Response != null)
                {
                    Debug.WriteLine("Response headers: " + e.Response.Headers.ToString());
                    try
                    {
                        using (var reader = new StreamReader(e.Response.GetResponseStream()))
                        {
                            var responseBody = reader.ReadToEnd();
                            Debug.WriteLine("Error response body: " + responseBody);
                        }
                    }
                    catch (Exception e2)
                    {
                        Debug.WriteLine("Failed to read response body: " + e2.Message);
                    }
                }
                throw;
            }

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

        

        private static void CopyStream(Stream fromStream, Stream toStream, long bytesTotal, SmugMugUploader uploader)
        {
            const int bufferSize = 81920; // good enough for Stream.CopyTo
            var buffer = new byte[bufferSize];
            long bytesTransferred = 0;
            int count;
            while ((count = fromStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                toStream.Write(buffer, 0, count);
                bytesTransferred += count;
                if (bytesTransferred > bytesTotal)
                {
                    bytesTransferred = bytesTotal;
                }
                uploader.RaiseUploadProgress(bytesTransferred, bytesTotal);
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

        protected static string GetUri(dynamic uriJson)
        {
            // Handle uris with or without metadata
            //return (string) (uriJson.Uri ?? uriJson);
            //return (string) (uriJson.HasValues ? uriJson.Uri : uriJson);
            var value = uriJson.Value as string;
            if (value == null)
            {
                value = uriJson.Uri.Value as string;
            }
            return value;
        }
    }
}
