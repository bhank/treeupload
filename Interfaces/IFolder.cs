using System.Collections.Generic;

namespace coynesolutions.treeupload
{
    public interface IFolder
    {
        string Name { get; }
        IEnumerable<IFolder> SubFolders { get; }
        IEnumerable<IImage> Images { get; }
    }
}
