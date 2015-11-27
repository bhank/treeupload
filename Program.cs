using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using coynesolutions.treeupload.SmugMug;

namespace coynesolutions.treeupload
{
    class Program
    {
        static void Main(string[] args)
        {
            Test();
            //TestSort();
            Console.WriteLine("All done. Press a key to exit...");
            Console.ReadKey();
        }

        private static void TestSort()
        {
                        Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.Listeners.Add(new TextWriterTraceListener("treeupload.log.txt"));

            const string otherTempNodeUri = "/api/v2/node/PnHR2K";
            var folder = SmugMugFolder.LoadFromNodeUri(otherTempNodeUri + "?_verbosity=1");
            Trace.WriteLine("------------- before ------------");
            foreach (var i in folder.Images)
            {
                Debug.WriteLine(i.FileName);
            }
            folder.Sort();
            Trace.WriteLine("------------- after ------------");
            foreach (var i in folder.Images)
            {
                Debug.WriteLine(i.FileName);
            }
        }

        private static void Test()
        {
            const bool dryRun = false; // TODO: make this apply to folder creation too... maybe make it a parameter
            Trace.Listeners.Add(new ConsoleTraceListener());
            Trace.Listeners.Add(new TextWriterTraceListener("treeupload.log.txt"));

            var uploader = new SmugMugUploader();
            var rootImagesFolder = ConfigurationManager.AppSettings["ImageFolder"]; // move that to uploader?
            var folderToUpload = Path.Combine(rootImagesFolder, "2015"); // upload only from this subdirectory of the rootImagesFolder
            if (!folderToUpload.StartsWith(rootImagesFolder, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception("Folder to upload must be under ImageFolder!");
            }

            if (string.IsNullOrWhiteSpace(rootImagesFolder) || !Directory.Exists(rootImagesFolder))
            {
                throw new Exception("Configured images folder does not exist: " + rootImagesFolder);
            }
            string lastDirectory = null;
            var directoryNeedsSort = false;
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

                    Trace.WriteLine("directoryNeedsSort? " + directoryNeedsSort);
                    if (directoryNeedsSort)
                    {
                        folder.Sort();
                        directoryNeedsSort = false;
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
                if (directory != lastDirectory)
                {
                    //// clean up from last directory
                    //if (albumImages != null && albumImages.Count > 0)
                    //{
                    //    Trace.WriteLine("WARNING: Extra images in album " + folder.Name);
                    //    foreach (var extra in albumImages.Keys)
                    //    {
                    //        Trace.WriteLine("\t" + extra);
                    //    }
                    //    albumImages.Clear(); // not really necessary since it's reassigned below, but no point in keeping this around
                    //}

                    //Trace.WriteLine("directoryNeedsSort? " + directoryNeedsSort);
                    //if (directoryNeedsSort)
                    //{
                    //    // TODO: sort the modified album
                    //    folder.Sort();
                    //    directoryNeedsSort = false;
                    //}
                    postDirectoryCleanup();

                    if (!directory.StartsWith(rootImagesFolder))
                    {
                        throw new Exception("Directory isn't in images folder?");
                    }
                    var relativeDirectory = directory.Substring(rootImagesFolder.Length).Trim('\\');
                    var parts = relativeDirectory.Split('\\');

                    if (parts.Length > 0 && parts[0] == "Other People's Cameras")
                    {
                        parts[0] = "Other People"; // put them in there instead
                    }
                    if (!uploader.RootFolder.SubFolders.Any(f => f.Name == parts[0]))
                    {
                        // TODO: abstract out my silly folder logic and maybe make it customizable

                        const string DefaultFolderName = "Other"; // move this to SmugMugUploader? Config setting
                        if (!uploader.RootFolder.SubFolders.Any(f => f.Name == DefaultFolderName))
                        {
                            throw new Exception("Default folder name doesn't exist!");
                            // Maybe I should just create it?
                        }
                        // Stick it under the default top-level folder
                        var temp = new List<string>(parts);
                        temp.Insert(0, "Other");
                        parts = temp.ToArray();
                    }

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
                            nextFolder = currentFolder.CreateSubFolder(relativeDirectory, true); // TODO: this is totally specific to my config... normally you might want to have node folders with just parts[i] for their names, and leaf albums with either parts[i] or relativeDirectory for their names
                            // TODO: maybe have a few configurable options...
                            // my smugmug style, where it's always folder/category "tldname", album "tldname\dirname[\dirname]" (except one level deeper under "Other People"!)
                            // albums for directories containing images, otherwise folders
                            // folders for all directories; extra albums (maybe with the full relativePath as their name) inside of those containing images
                        }
                        currentFolder = nextFolder;
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

                //var matchingImages = folder.Images.Where(i => i.FileName == Path.GetFileName(file)).ToArray();
                //if (matchingImages.Length == 1)
                IImage matchingImage;
                if(albumImages.TryGetValue(Path.GetFileName(file), out matchingImage))
                {
                    albumImages.Remove(Path.GetFileName(file));

                    // I guess a filename-only match was good enough for me in my old uploader.

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
                    // upload it?
                    //uploader.Upload(file, folder);
                    if (dryRun)
                    {
                        Trace.WriteLine(string.Format("DRY RUN: Would be uploading {0} to {1}", file, folder.Name));
                    }
                    else
                    {
                        Trace.WriteLine(string.Format("Uploading {0} to {1}", file, folder.Name));
                        if (folder.Upload(file))
                        {
                            directoryNeedsSort = true;
                        }
                    }
                }
            }

            postDirectoryCleanup();
        }

        //private static string GetMd5(string file)
        //{
        //    using (var md5 = MD5.Create())
        //    {
        //        using (var stream = File.OpenRead(file))
        //        {
        //            return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-","").ToLower();
        //        }
        //    }
        //}
    }
}
