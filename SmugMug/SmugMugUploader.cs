﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace coynesolutions.treeupload.SmugMug
{
    public class SmugMugUploader : SmugMugBase, IUploader
    {

        private Lazy<dynamic> authUserJsonLazy;
        private Lazy<IFolder> rootFolderLazy;
        public event UploadProgressEventHandler UploadProgress;

        public SmugMugUploader()
        {
            authUserJsonLazy = new Lazy<dynamic>(() => GetJson("/api/v2!authuser?_filter=NickName%2CUri&_filteruri=Node&_verbosity=1"));
            rootFolderLazy = new Lazy<IFolder>(() => SmugMugFolder.LoadFromNodeUri(this, (string)AuthUserJson.Uris.Node + "?_verbosity=1"));
        }

        public void RaiseUploadProgress(long bytesTransferred, long bytesTotal)
        {
            var args = new UploadProgressEventArgs(bytesTransferred, bytesTotal);
            var e = UploadProgress;
            if (e != null)
            {
                e(this, args);
            }
        }

        public static bool LogIn()
        {
            throw new NotImplementedException();
            //GetOauthToken();
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

        private string AuthUserUri
        {
            get { return AuthUserJson.Uri; }
        }

        public IFolder RootFolder
        {
            get
            {
                return rootFolderLazy.Value;
            }
        }

        public void RemoveBadKeywords()
        {
            var topKeywordJson = GetJson(AuthUserUri + "!topkeywords?NumKeywords=100000&_verbosity=1");
            var topKeywordArray = (from object x in (IEnumerable)topKeywordJson.Response.UserTopKeywords.TopKeywords select x.ToString()).ToArray();
            var badKeywords = topKeywordArray.Where(IsBadKeyword).ToArray();
            foreach (var badKeyword in badKeywords)
            {
                // 100 is the max count it will accept. I'll have to do paging.
                const int itemsPerPage = 100;
                var items = GetPagedItems(json => json.Response.ImageSearchResult, "/api/v2/image!search?Keywords={0}&Scope={1}&count={2}&_verbosity=1", badKeyword, Uri.EscapeDataString(AuthUserUri), itemsPerPage);
                //var searchResultJson = GetJson("/api/v2/image!search?Keywords={0}&Scope={1}&count=1000&_verbosity=1", badKeyword, Uri.EscapeDataString(AuthUserUri));
                foreach (var searchResult in items)
                {
                    //// I'm getting a redirect after the PATCH. The patch still works, but I don't get the nice response. I guess I'll make an extra call to get the new image uri first. -- Nah, I'll skip it.
                    //var imageJson = GetJsonWithRedirect((string) searchResult.Uris.Image + "?_verbosity=1");
                    //var imageUri = (string) imageJson.Response.Uri;

                    var keywordArray = (from object x in (IEnumerable)searchResult.KeywordArray select x.ToString()).ToArray();
                    if (keywordArray.Length > 0)
                    {
                        var newKeywords = keywordArray.Where(k => !IsBadKeyword(k)).ToArray(); // remove any other bad keywords too
                        if (newKeywords.Length < keywordArray.Length)
                        {
                            var patchData = new {KeywordArray = newKeywords};
                            var patchJson = PatchJson(patchData, (string) searchResult.Uris.Image + "?_verbosity=1"); // that URL will get a redirect after the PATCH, but still work.
                            // can't parse the response since there isn't one due to the redirect.
                            //var newKeywordArray = (from object x in (IEnumerable)patchJson.Response.Image.KeywordArray select x.ToString()).ToArray();
                            //if (newKeywordArray.Any(k => IsBadKeyword(k)))
                            //{
                            //    throw new Exception("Still bad keywords!");
                            //}
                        }
                        else
                        {
                            // no keywords removed
                        }
                    }
                }
            }
        }

        private static readonly Regex badKeywordRegex = new Regex("^(HPIM)?\\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static bool IsBadKeyword(string keyword)
        {
            if(badKeywordRegex.IsMatch(keyword))
            {
                int i;
                if(int.TryParse(keyword, out i))
                {
                    if(i >= 1999 && i <= DateTime.Now.Year)
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        public void RemoveKeyword(string keyword)
        {
            var resultJson = GetJson("/api/v2/image!search?Keywords={0}&Scope={1}&count=100&_verbosity=1", keyword, Uri.EscapeDataString(AuthUserUri));
            if (resultJson.Response.ImageSearchResult != null)
            {
                foreach (var searchResult in resultJson.Response.ImageSearchResult)
                {
                    // I'm getting a redirect after the PATCH. The patch still works, but I don't get the nice response. I guess I'll make an extra call to get the new image uri first.
                    var imageJson = GetJsonWithRedirect((string) searchResult.Uris.Image + "?_verbosity=1");
                    var imageUri = (string) imageJson.Response.Uri;

                    var keywordArray = (from object x in (IEnumerable)searchResult.KeywordArray select x.ToString()).ToArray();
                    var newKeywords = keywordArray.Where(k => !keyword.Equals(k, StringComparison.InvariantCultureIgnoreCase)).ToArray();
                    var patchData = new {KeywordArray = newKeywords};
                    var response = PatchJson(patchData, imageUri + "?_verbosity=1");
                }
            }
        }
    }
}
