using System;
using System.Collections.Generic;

namespace coynesolutions.treeupload
{
    public class SmugMugFolder : IFolder
    {
        private string nodeid;
        private Lazy<dynamic> nodeJson; 

        public SmugMugFolder(string nodeid, string name = "")
        {
            this.nodeid = nodeid;
            nodeJson = new Lazy<dynamic>(() => SmugMugUploader.RequestJson("node/{0}!children", this.nodeid));
        }

        private dynamic NodeJson
        {
            get { return nodeJson.Value; }
        }

        public string Name { get { return NodeJson.Name; }  }

        public IEnumerable<IFolder> SubFolders
        {
            get { throw new NotImplementedException(); }
        }

        public IEnumerable<IImage> Images
        {
            get { throw new NotImplementedException(); }
        }
    }
}
