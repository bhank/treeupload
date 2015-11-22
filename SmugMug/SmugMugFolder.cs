using System;
using System.Collections.Generic;
using coynesolutions.treeupload.SmugMug;

namespace coynesolutions.treeupload
{
    public class SmugMugFolder : IFolder
    {
        private string nodeid;
        private dynamic nodeJson;

        public SmugMugFolder(dynamic folderData)
        {
            nodeJson = folderData;
        }

        public static SmugMugFolder LoadFromNodeId(string nodeid)
        {
            var nodeJson = SmugMugUploader.RequestJson("/node/{0}?_verbosity=1", nodeid).Response.Node;
            return new SmugMugFolder(nodeJson);
        }

        private dynamic ChildrenJson
        {
            get { return new Lazy<dynamic>(() => SmugMugUploader.RequestJson("/node/{0}!children?_verbosity=1&count=100000", NodeID)).Value.Response.Node; }
        }

        private dynamic ImagesJson
        {
            get { return new Lazy<dynamic>(() => SmugMugUploader.RequestJson("/album/{0}!images?_verbosity=1&count=100000", NodeID)).Value.Response.AlbumImage; }
        }

        public string Name { get { return nodeJson.Name; }  }
        public string Type { get { return nodeJson.Type; }  }
        public string NodeID { get { return nodeJson.NodeID; }  }

        public IEnumerable<IFolder> SubFolders
        {
            get
            {
                if (Type == "Folder")
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
