using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using coynesolutions.treeupload.SmugMug;

namespace coynesolutions.treeupload
{
    public class SmugMugFolder : IFolder
    {
        private readonly dynamic nodeJson;
        private Lazy<dynamic> childrenJsonLazy;
        private Lazy<dynamic> albumJsonLazy;
        private Lazy<dynamic> imagesJsonLazy;
        private Lazy<IEnumerable<IFolder>> subfoldersLazy;
        private Lazy<IEnumerable<IImage>> imagesLazy;

        public SmugMugFolder(dynamic folderData)
        {
            nodeJson = folderData;
            Debug.WriteLine("new SmugMugFolder: " + ToString());
            childrenJsonLazy = new Lazy<dynamic>(() => SmugMugUploader.RequestJson(ChildNodesUri + "?_verbosity=1&count=100000"));
            albumJsonLazy = new Lazy<dynamic>(() => SmugMugUploader.RequestJson(AlbumUri + "?_verbosity=1"));
            imagesJsonLazy = new Lazy<dynamic>(() => SmugMugUploader.RequestJson(AlbumImagesUri + "?_verbosity=1&count=100000"));
            subfoldersLazy = new Lazy<IEnumerable<IFolder>>(() =>
            {
                if (!HasChildren)
                {
                    return Enumerable.Empty<IFolder>();
                }
                return ((IEnumerable<dynamic>) ChildrenJson).Select(d => new SmugMugFolder(d)).ToArray();
            });
            imagesLazy = new Lazy<IEnumerable<IImage>>(() =>
            {
                if (Type != "Album")
                {
                    return Enumerable.Empty<IImage>();
                }
                return ((IEnumerable<dynamic>) ImagesJson).Select(d => new SmugMugImage(d)).ToArray();
                //foreach (var image in ImagesJson)
                //{
                //    yield return new SmugMugImage(image);
                //}
            });
        }

        public static SmugMugFolder LoadFromNodeUri(string nodeUri)
        {
            var nodeJson = SmugMugUploader.RequestJson(nodeUri).Response.Node;
            return new SmugMugFolder(nodeJson);
        }

        private dynamic ChildrenJson
        {
            get { return childrenJsonLazy.Value.Response.Node; }
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

        public string Name { get { return nodeJson.Name; }  }
        public string Type { get { return nodeJson.Type; }  }
        public bool HasChildren { get { return nodeJson.HasChildren; }  }
        public string NodeID { get { return nodeJson.NodeID; }  }
        public string ChildNodesUri { get { return (string)nodeJson.Uris.ChildNodes; } }
        public string AlbumUri { get { return nodeJson.Uris.Album; } }
        public string AlbumImagesUri { get { return AlbumJson.Uris.AlbumImages; } }


        public IEnumerable<IFolder> SubFolders
        {
            get { return subfoldersLazy.Value; }
        }

        public IEnumerable<IImage> Images
        {
            get { return imagesLazy.Value; }
        }

        public override string ToString()
        {
            return string.Format("{0} [{1}]", Name, NodeID);
        }
    }
}
