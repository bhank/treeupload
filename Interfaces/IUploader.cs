namespace coynesolutions.treeupload
{
    public interface IUploader
    {
        //bool LogIn();
        //bool LogOut();
        IFolder RootFolder { get; } // If a service doesn't have a single root folder, expose a fake one, with all the root folders under it. (Make sure it doesn't mess with paths)
        string[] SupportedExtensions { get; } // File extensions supported by this service. Could get fancy and use mime types instead, but... nah
        void Upload(string file, IFolder folder);
    }
}
