using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using coynesolutions.treeupload.SmugMug;
using System.Net;

namespace coynesolutions.treeupload
{
    class Program
    {
        private static string logFile;

        static void Main(string[] args)
        {
            logFile = "treeupload.log.txt";
            Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.Listeners.Add(new TextWriterTraceListener(logFile));

            Upload<SmugMugUploader>(args?.ElementAtOrDefault(0));

            Console.WriteLine("All done. Press a key to exit...");
            Console.ReadKey();
        }

        private static int dupeIndent;
        private static void SmugMugDupeFoldersTest(IFolder folder = null)
        {
            folder = folder ?? new SmugMugUploader().RootFolder;
            Trace.WriteLine(new string('\t', dupeIndent++) + folder.Name);
            var subFolders = folder.SubFolders.ToArray();
            foreach (var dupe in subFolders.GroupBy(f => f.Name, f => f).Where(g => g.Count() > 1).Select(g => new {Name = g.Key, Count = g.Count()}))
            {
                Trace.WriteLine(new string('\t', dupeIndent) + dupe.Name + ": " + dupe.Count);
            }
            foreach (var subFolder in subFolders)
            {
                SmugMugDupeFoldersTest(subFolder);
            }
            dupeIndent--;
        }

        private static void KeywordTest()
        {
            var uploader = new SmugMugUploader();
            uploader.RemoveBadKeywords();
        }

        private static void SmugMugTestSort()
        {
            var u = new SmugMugUploader();
            foreach (var folder in u.RootFolder.SubFolders.Single(f => f.Name == "2016").SubFolders.Where(f => f.Name.StartsWith("2016\\20160312")))
            {
                folder.Sort();
            }
        }

        private static void Upload<T>(string subdir = null) where T : IUploader, new()
        {
            IUploader uploader = new T();
            uploader.UploadProgress += (sender, args) => Console.Write("\r" + args.FractionComplete.ToString("P"));
            var rootImagesFolder = ConfigurationManager.AppSettings["ImageFolder"]; // move that to uploader?
            var folderToUpload = Path.Combine(rootImagesFolder, subdir ?? ""); // upload only from this subdirectory of the rootImagesFolder. The arg default is null rather than "", coalesced here, so it'll work if null is passed in.
            if (!folderToUpload.StartsWith(rootImagesFolder, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception("Folder to upload must be under ImageFolder!");
            }

            if (string.IsNullOrWhiteSpace(rootImagesFolder) || !Directory.Exists(rootImagesFolder))
            {
                throw new Exception("Configured images folder does not exist: " + rootImagesFolder);
            }
            string lastDirectory = null;
            var anyImagesUploaded = false;
            IFolder folder = null;
            Dictionary<string, IImage> albumImages = null;

            // Specify actions to be run when we move to a new directory, plus when we are all finished (for the last directory).
            // I want "access to modified closure" -- that is, I want the latest values of all the variables accessed within this Action-- so I will ignore resharper's warning.
// ReSharper disable AccessToModifiedClosure
            Action postDirectoryCleanup = () =>
            {
                // clean up from last directory
                if (albumImages != null && albumImages.Count > 0)
                {
                    Trace.WriteLine("WARNING: Extra images in album " + folder.Name);
                    foreach (var extra in albumImages.Keys)
                    {
                        Trace.WriteLine("\t" + extra);
                    }
                    albumImages.Clear(); // not really necessary since it's reassigned below, but no point in keeping this around
                }

                if (folder != null)
                {
                    Trace.WriteLine("anyImagesUploaded? " + anyImagesUploaded);
                    folder.Sort(anyImagesUploaded);
                    anyImagesUploaded = false;
                }
            };
// ReSharper restore AccessToModifiedClosure

            foreach (var file in Directory.EnumerateFiles(folderToUpload, "*", SearchOption.AllDirectories))
            {
                if (!uploader.SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                {
                    continue;
                }
                Trace.WriteLine(file);
                var directory = Path.GetDirectoryName(file);
                if(!directory.Equals(lastDirectory, StringComparison.InvariantCultureIgnoreCase))
                {
                    postDirectoryCleanup();

                    if (!directory.StartsWith(rootImagesFolder, StringComparison.InvariantCultureIgnoreCase))
                    {
                        throw new Exception("Directory isn't in images folder?");
                    }
                    var relativeDirectory = directory.Substring(rootImagesFolder.Length).Trim('\\');
                    var parts = relativeDirectory.Split('\\');

                    TransformPath(uploader, ref parts);

                    var currentFolder = uploader.RootFolder;
                    for (var i = 0; i < parts.Length; i++)
                    {
                        var nextFolder = currentFolder.SubFolders.SingleOrDefault(f => f.Name == relativeDirectory);
                        if (nextFolder == null)
                        {
                            nextFolder = currentFolder.SubFolders.SingleOrDefault(f => f.Name == parts[i] || f.Name == string.Join("\\", parts.Take(i + 1)));
                        }
                        if (nextFolder == null)
                        {
                            // create new one inside folder?
                            var newFolderName = relativeDirectory;
                            var folderContainsImages = true;
                            if (i == 1 && parts[0] == OtherPeopleSmugMugCategory && parts.Length > 2)
                            {
                                newFolderName = parts[1];
                                folderContainsImages = false;
                            }
                            nextFolder = currentFolder.CreateSubFolder(newFolderName, folderContainsImages); // TODO: this is totally specific to my config... normally you might want to have node folders with just parts[i] for their names, and leaf albums with either parts[i] or relativeDirectory for their names
                            // TODO: maybe have a few configurable options...
                            // my smugmug style, where it's always folder/category "tldname", album "tldname\dirname[\dirname]" (except one level deeper under "Other People"!)
                            // albums for directories containing images, otherwise folders
                            // folders for all directories; extra albums (maybe with the full relativePath as their name) inside of those containing images
                        }
                        currentFolder = nextFolder;
                        Trace.WriteLine(string.Format("Folder {0} has {1} subfolders", currentFolder.Name, currentFolder.SubFolders.Count()));
                        if (currentFolder.Name == relativeDirectory)
                        {
                            break;
                        }
                    }
                    folder = currentFolder;
                    Trace.WriteLine(directory + "\t->\t" + folder.Name);

                    // do stuff... check and see if the album exists and so forth
                    lastDirectory = directory;

                    // any duplicate filenames in this album?
                    var imagesByFilename = folder.Images.GroupBy(i => i.FileName).ToArray();
                    var duplicateFilenames = imagesByFilename.Where(g => g.Count() > 1).ToArray();
                    if (duplicateFilenames.Any())
                    {
                        Trace.WriteLine("WARNING: duplicate filenames in album " + folder.Name);
                        foreach (var dupe in duplicateFilenames.Select(g => g.Key))
                        {
                            Trace.WriteLine("\t" + dupe);
                        }
                    }
                    // make a list of filenames. we'll take out each one as we see it, and any left over are extra, in the album but not on disk.
                    albumImages = imagesByFilename.ToDictionary(g => g.Key, g => g.First());
                }

                IImage matchingImage;
                if(albumImages.TryGetValue(Path.GetFileName(file), out matchingImage))
                {
                    albumImages.Remove(Path.GetFileName(file));

                    //// see if it matches the image on disk
                    //var fileInfo = new FileInfo(file);
                    //if (fileInfo.Length != matchingImage.Size)
                    //{
                    //    Console.WriteLine("WARNING: Size doesn't match for " + file + ": " + fileInfo.Length + " <> " + matchingImage.Size);
                    //}
                    ////if (matchingImage.Date.HasValue && fileInfo.LastWriteTime != matchingImage.Date) // checking the date causes another http request, for image metadata
                    ////{
                    ////    Debug.WriteLine("Date doesn't match!");
                    ////}
                    //var md5 = GetMd5(file);
                    //if (md5 != matchingImage.MD5)
                    //{
                    //    Console.WriteLine("WARNING: MD5 doesn't match for " + file + ": " + md5 + " <> " + matchingImage.MD5);
                    //}

                }
                else
                {
                    Trace.WriteLine(string.Format("Uploading {0} to {1}", file, folder.Name));
                    if (UploadWithRetry(folder, file))
                    {
                        anyImagesUploaded = true;
                    }
                }
            }

            postDirectoryCleanup();
        }

        private static bool UploadWithRetry(IFolder folder, string file)
        {
            const int attempts = 3;
            var attempt = 1;
            while(true)
            {
                try
                {
                    return folder.Upload(file);
                }
                catch (Exception e)
                {
                    Trace.WriteLine("Upload failed: " + e.Message);

                    var webEx = e as WebException;
                    if(webEx != null)
                    {
                        var response = webEx.Response as HttpWebResponse;
                        if(response != null)
                        {
                            if(response.StatusCode == HttpStatusCode.Unauthorized)
                            {
                                Trace.WriteLine("Got a 401. It probably worked... returning true!");
                                return true;
                            }
                        }
                        Trace.WriteLine("WebException");
                    }
                    
                    if (attempt < attempts)
                    {
                        attempt++;
                        Trace.WriteLine("Retrying...");
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        private const string OtherPeopleSmugMugCategory = "Other People";
        private static void TransformPath(IUploader uploader, ref string[] directories)
        {
            // TODO: make this transform logic customizable somehow

            if (directories.Length == 0)
            {
                return;
            }
            var firstPart = directories[0];

            if (directories.Length == 1 && firstPart == "Other")
            {
                var temp2 = new List<string>(directories);
                temp2.Insert(0, "Test Junk");
                directories = temp2.ToArray();
                return;
            }
            if (directories[0] == "MotoPhotoCD")
            {
                var temp2 = new List<string>(directories);
                temp2.Insert(0, "2003");
                directories = temp2.ToArray();
                return;
            }

            const string otherPeopleDiskDirectory = "Other People's Cameras";

            if (firstPart == otherPeopleDiskDirectory)
            {
                directories[0] = OtherPeopleSmugMugCategory; // put them in there instead
            }
            else if (uploader.RootFolder.SubFolders.All(f => f.Name != firstPart))
            {
                // this file's top-level folder doesn't exist on smugmug.
                var pathString = string.Join("\\", directories);
                if (uploader.RootFolder.SubFolders.Single(f => f.Name == OtherPeopleSmugMugCategory).SubFolders.Any(f => f.Name == firstPart || f.Name == pathString))
                {
                    var temp2 = new List<string>(directories);
                    temp2.Insert(0, OtherPeopleSmugMugCategory);
                    directories = temp2.ToArray();
                    return;
                }

                int i;
                if (int.TryParse(firstPart, out i) && 1999 <= i && i <= DateTime.Now.Year)
                {
                    // year directory... it's OK for it to create it
                    return;
                }

                const string DefaultFolderName = "Other"; // move this to SmugMugUploader? Config setting
                if (uploader.RootFolder.SubFolders.All(f => f.Name != DefaultFolderName))
                {
                    throw new Exception("Default folder name doesn't exist!");
                    // Maybe I should just create it?
                }

                // Stick it under the default top-level folder
                var temp = new List<string>(directories);
                temp.Insert(0, DefaultFolderName);
                directories = temp.ToArray();
            }
        }
    }
}
