using System;

namespace coynesolutions.treeupload
{
    public interface IImage
    {
        string FileName { get; }
        long Size { get; }
        DateTime? Date { get; }
        string MD5 { get; }
        // exif, tags, size? hash?
    }
}
