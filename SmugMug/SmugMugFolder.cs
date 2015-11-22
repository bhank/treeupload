using System;
using System.Collections.Generic;
using coynesolutions.treeupload.SmugMug;

namespace coynesolutions.treeupload
{
    public class SmugMugFolder : IFolder
    {
        private readonly dynamic nodeJson;

        public SmugMugFolder(dynamic folderData)
        {
            nodeJson = folderData;
        }

        public static SmugMugFolder LoadFromNodeUri(string nodeUri)
        {
            var nodeJson = SmugMugUploader.RequestJson(nodeUri).Response.Node;
            return new SmugMugFolder(nodeJson);
        }

        private dynamic ChildrenJson
        {
            get { return new Lazy<dynamic>(() => SmugMugUploader.RequestJson(ChildNodesUri + "?_verbosity=1&count=100000")).Value.Response.Node; }
        }

        private dynamic AlbumJson
        {
            get { return new Lazy<dynamic>(() => SmugMugUploader.RequestJson(AlbumUri + "?_verbosity=1")).Value.Response.Album; }
        }

        private dynamic ImagesJson
        {
            get
            {
                //return new Lazy<dynamic>(() => SmugMugUploader.RequestJson(AlbumUri +  "!images?_verbosity=1&count=100000")).Value.Response.AlbumImage;
                return new Lazy<dynamic>(() => SmugMugUploader.RequestJson(AlbumImagesUri +  "?_verbosity=1&count=100000")).Value.Response.AlbumImage;
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
            get
            {
                if (HasChildren)
                {
                    foreach (var childNode in ChildrenJson)
                    {
                        yield return new SmugMugFolder(childNode);
                    }
                }
            }
        }

        public IEnumerable<IImage> Images
        {
            get
            {
                if (Type == "Album")
                {
                    foreach (var image in ImagesJson)
                    {
                        yield return new SmugMugImage(image);
                    }
                }
            }
        }
    }
}
