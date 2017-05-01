using System.Collections.Generic;

namespace coynesolutions.treeupload.SmugMug
{
    public interface IFolder
    {
        string Name { get; }
        IEnumerable<IFolder> SubFolders { get; }
        IEnumerable<IImage> Images { get; }
        bool Upload(string file);
        IFolder CreateSubFolder(string name, bool hasImages);
        void Sort(bool reloadImageList = false);
    }
}
