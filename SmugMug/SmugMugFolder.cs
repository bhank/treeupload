using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace coynesolutions.treeupload.SmugMug
{
    public class SmugMugFolder : SmugMugBase, IFolder
    {
        private readonly dynamic nodeJson;
        private Lazy<IEnumerable<dynamic>> childrenJsonLazy;
        private Lazy<dynamic> albumJsonLazy;
        private Lazy<dynamic> imagesJsonLazy;
        private Lazy<IEnumerable<IFolder>> subfoldersLazy;
        private Lazy<IEnumerable<IImage>> imagesLazy;
        internal SmugMugUploader Uploader;

        private SmugMugFolder(SmugMugUploader uploader, dynamic folderData)
        {
            Uploader = uploader;
            nodeJson = folderData;
            Debug.WriteLine("new SmugMugFolder: " + ToString());
            ResetAlbumLazy();
            ResetChildrenLazy();
            ResetImagesLazy();
        }

        internal static SmugMugFolder LoadFromNodeUri(SmugMugUploader uploader, string nodeUri)
        {
            var nodeJson = GetJson(nodeUri).Response.Node;
            return new SmugMugFolder(uploader, nodeJson);
        }

        private void ResetAlbumLazy()
        {
            albumJsonLazy = new Lazy<dynamic>(() => GetJson(AlbumUri + "?_verbosity=1"));
        }

        private void ResetChildrenLazy()
        {
            childrenJsonLazy = new Lazy<IEnumerable<dynamic>>(() => GetPagedItems(json => json.Response.Node, ChildNodesUri + "?_verbosity=1&count=100000"));
            subfoldersLazy = new Lazy<IEnumerable<IFolder>>(() =>
            {
                if (!HasChildren)
                {
                    return Enumerable.Empty<IFolder>();
                }
                return ((IEnumerable<dynamic>) ChildrenJson).Select(d => new SmugMugFolder(Uploader, d)).ToArray();
            });
        }

        private void ResetImagesLazy()
        {
            imagesJsonLazy = new Lazy<dynamic>(() => GetJson(AlbumImagesUri + "?_verbosity=1&count=100000"));
            imagesLazy = new Lazy<IEnumerable<IImage>>(() =>
            {
                if (Type != "Album")
                {
                    return Enumerable.Empty<IImage>();
                }
                var imagesJson = (IEnumerable<dynamic>) ImagesJson;
                if (imagesJson == null)
                {
                    return Enumerable.Empty<IImage>();
                }
                return imagesJson.Select(d => new SmugMugImage(d)).ToArray();
            });
        }

        //public static SmugMugFolder LoadFromNodeUri(string nodeUri)
        //{
        //    var nodeJson = GetJson(nodeUri).Response.Node;
        //    return new SmugMugFolder(nodeJson);
        //}

        private dynamic ChildrenJson
        {
            get { return childrenJsonLazy.Value; }
        }

        private dynamic AlbumJson
        {
            get { return albumJsonLazy.Value.Response.Album; }
        }

        private dynamic ImagesJson
        {
            get
            {
                return imagesJsonLazy.Value.Response.AlbumImage;
            }
        }

        public string Name { get { return (string)nodeJson.Name; }  }
        public string Type { get { return (string)nodeJson.Type; }  }
        public bool HasChildren { get { return (bool)nodeJson.HasChildren; }  }
        public string NodeID { get { return (string)nodeJson.NodeID; }  }
        public string ChildNodesUri { get { return GetUri(nodeJson.Uris.ChildNodes); } }
        public string AlbumUri { get { return GetUri(nodeJson.Uris.Album); } }

        public string AlbumImagesUri
        {
            get
            {
                //return (string)AlbumJson.Uris.AlbumImages; // oh, this is null on a newly-created album!
                // because it's expanding the uri metadata, even though I put _verbosity=1 on the querystring.
                // put it in the post data instead?
                // just in case that doesn't work:
                return GetUri(AlbumJson.Uris.AlbumImages);
            }
        }

        public string SortAlbumImagesUri { get { return GetUri(AlbumJson.Uris.SortAlbumImages); } }


        public IEnumerable<IFolder> SubFolders
        {
            get { return subfoldersLazy.Value; }
        }

        public IEnumerable<IImage> Images
        {
            get { return imagesLazy.Value; }
        }

        public bool Upload(string file)
        {
            return Upload(file, this);
        }

        public IFolder CreateSubFolder(string name, bool hasImages)
        {
            const int maxUrlNameLength = 30;
            var urlName = name.Replace(" ", "-").Replace("\\", "-").Replace("'", "-"); // TODO: remove all other url-unfriendly characters
            if (urlName.Length > maxUrlNameLength)
            {
                urlName = urlName.Substring(0, maxUrlNameLength/2 - 1) + "-" + urlName.Substring(urlName.Length - (maxUrlNameLength/2 - 1));
            }
            var newFolderData = new
            {
                Name = name,
                UrlName = urlName,
                Type = hasImages ? "Album" : "Folder",
            };
            var responseJson = PostJson(newFolderData, ChildNodesUri + "?_verbosity=1");
            if (responseJson.Message != "Created")
            {
                throw new Exception("Unexpected response message: " + responseJson.Message);
            }

            // Customize new album settings
            if (hasImages)
            {
                PatchJson(new {SquareThumbs = false}, GetUri(responseJson.Response.Node.Uris.Album) + "?_verbosity=1");
            }

            ResetChildrenLazy(); // so subfolders will refresh when next accessed, and include this new folder
            return new SmugMugFolder(Uploader, responseJson.Response.Node);
        }

        private class ImageSortComparer : IComparer<IImage>
        {
            private static readonly Lazy<ImageSortComparer> InstanceLazy = new Lazy<ImageSortComparer>(() => new ImageSortComparer());
            public static ImageSortComparer Instance
            {
                get { return InstanceLazy.Value; }
            }

            private static readonly Regex fileNumberRegex = new Regex(@"^(?:MVI_|IMG_|DSCN|[Pf]|FILE)(\d+)\.[A-Za-z0-9]{3}$", RegexOptions.Compiled);
            public int Compare(IImage x, IImage y)
            {
                var isIphone = x.Model != null && x.Model.Contains("iPhone");
                var hasDates = x.ExifDateTime.HasValue && y.ExifDateTime.HasValue;
                var hasWeirdKind = hasDates && (x.ExifDateTime.Value.Kind == DateTimeKind.Local || y.ExifDateTime.Value.Kind == DateTimeKind.Local); // MOV files taken in another time zone have Local and sort wrong

                // I'll trust the timestamp on iPhones. Some of my older cameras weren't so trustworthy (I guess).
                if (!isIphone || hasWeirdKind)
                {
                    var matchX = fileNumberRegex.Match(x.FileName);
                    if (matchX.Success)
                    {
                        var matchY = fileNumberRegex.Match(y.FileName);
                        if (matchY.Success)
                        {
                            var fileNumberX = int.Parse(matchX.Groups[1].Value);
                            var fileNumberY = int.Parse(matchY.Groups[1].Value);
                            return fileNumberX.CompareTo(fileNumberY);
                        }
                    }
                }

                if (hasDates && !hasWeirdKind)
                {
                    return DateTime.Compare(x.ExifDateTime.Value, y.ExifDateTime.Value);
                }
                return string.Compare(x.FileName, y.FileName, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public void Sort(bool resetImageList)
        {
            if (resetImageList)
            {
                ResetImagesLazy();
            }
            var currentOrderImagesWithIndexes = Images.Cast<SmugMugImage>().Select((image, index) => new {image, index}).ToList();
            var sortedImages = Images.Cast<SmugMugImage>().OrderBy(i => i, ImageSortComparer.Instance).ToList();
            //var sortedImages = Images.Cast<SmugMugImage>().OrderBy(i => Guid.NewGuid()).ToList(); // random sort for testing

            //Trace.WriteLine("------------- new order ------------");
            //foreach (var i in sortedImages)
            //{
            //    Debug.WriteLine(i.FileName);
            //}

            var imagesOutOfOrder = currentOrderImagesWithIndexes.Where(c => sortedImages[c.index].ImageUri != c.image.ImageUri).ToArray(); // getting the list, for debugging purposes
            var needsSort = imagesOutOfOrder.Any();
            if (!needsSort)
            {
                Trace.WriteLine("Already sorted.");
                return;
            }
/*
            //var debugUnsortedImages = currentOrderImagesWithIndexes.Where(c => sortedImages[c.index].ImageUri != c.image.ImageUri).ToArray();
            for(var i = 0; i < sortedImages.Count; i++)
            {
                //Debug.WriteLine(currentOrderImagesWithIndexes[i].image.ImageUri + "\t" + sortedImages[i].ImageUri);
                Debug.WriteLine(currentOrderImagesWithIndexes[i].image.FileName + "\t" + sortedImages[i].FileName);
            }
*/

            if(AlbumJson.SortMethod != "Position")
            {
                PatchJson(new {SortMethod = "Position"}, AlbumUri + "?_verbosity=1");
                ResetAlbumLazy();
            }

            //// TODO: go through and move images into order as necessary...
            //// maybe rearrange the local collection as I go, so I think I know the order on the server
            //var localImages = Images.Cast<SmugMugImage>().ToList();

            //for (var n = 0; n < sortedImages.Count; n++)
            //{
            //    if (localImages[n].ImageUri != sortedImages[n].ImageUri)
            //    {
            //        var sortedPosition = sortedImages.FindIndex(i => i.ImageUri == localImages[n].ImageUri);

            //    }
            //}

            // Try and cheat to make it easy -- specify the order of all of them, and move them all before the first.
            //var commaDelimitedAlbumImageUris = sortedImages[0].AlbumImageUri;// string.Join(",", sortedImages.Select(i => i.AlbumImageUri));
            // moving one at a time seems to work.
            // maybe it doesn't like having the target Uri in the MoveUris? It doesn't seem to really move them.
            var commaDelimitedAlbumImageUris = string.Join(",", sortedImages.Take(sortedImages.Count - 1).Select(i => i.AlbumImageUri));
            // yup, seems to work.
            
            var moveData = new
            {
                MoveLocation = "Before",
                MoveUris = commaDelimitedAlbumImageUris,
                Uri = sortedImages.Last().AlbumImageUri, // currentOrderImagesWithIndexes[0].image.AlbumImageUri,
            };
            var responseJson = PostJson(moveData, SortAlbumImagesUri + "?_verbosity=1");
            if (responseJson.Code != 200 || responseJson.Message != "Ok")
            {
                throw new Exception("Unexpected response to sort request: " + responseJson.Code + ", " + responseJson.Message);
            }

            ResetImagesLazy(); // reload list of images so it loads the new order from the server
            // maybe compare against that order to make sure it matches what I think it should be 
            currentOrderImagesWithIndexes = Images.Cast<SmugMugImage>().Select((image, index) => new {image, index}).ToList();
            imagesOutOfOrder = currentOrderImagesWithIndexes.Where(c => sortedImages[c.index].ImageUri != c.image.ImageUri).ToArray();
            needsSort = imagesOutOfOrder.Any();
            if (needsSort)
            {
                throw new Exception("Still needs sort after sorting!");
            }
        }

        public override sealed string ToString() // just for debugging. only sealed because it's called in the constructor.
        {
            return string.Format("{0} [{1}]", Name, NodeID);
        }
    }
}
