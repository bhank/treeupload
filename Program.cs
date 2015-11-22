﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using coynesolutions.treeupload;

namespace treeupload
{
    class Program
    {
        static void Main(string[] args)
        {
            Test();
            Console.WriteLine("All done. Press a key to exit...");
            Console.ReadKey();
        }

        private static void Test()
        {
            var uploader = new SmugMugUploader();
            var imagesFolder = ConfigurationManager.AppSettings["ImageFolder"]; // move that to uploader?
            if (string.IsNullOrWhiteSpace(imagesFolder) || !Directory.Exists(imagesFolder))
            {
                throw new Exception("Configured images folder does not exist: " + imagesFolder);
            }
            string lastDirectory = null;
            IFolder folder = null;
            Dictionary<string, IImage> albumImages = null;
            foreach (var file in Directory.EnumerateFiles(imagesFolder, "*", SearchOption.AllDirectories))
            {
                if (!uploader.SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                {
                    continue;
                }
                Debug.WriteLine(file);
                var directory = Path.GetDirectoryName(file);
                if (directory != lastDirectory)
                {
                    // clean up from last directory
                    if (albumImages != null && albumImages.Count > 0)
                    {
                        Console.WriteLine("WARNING: Extra images in album " + folder.Name);
                        foreach (var extra in albumImages.Keys)
                        {
                            Console.WriteLine("\t" + extra);
                        }
                        albumImages.Clear(); // not really necessary since it's reassigned below, but no point in keeping this around
                    }

                    if (!directory.StartsWith(imagesFolder))
                    {
                        throw new Exception("Directory isn't in images folder?");
                    }
                    var relativeDirectory = directory.Substring(imagesFolder.Length).Trim('\\');
                    var parts = relativeDirectory.Split('\\');
                    var currentFolder = uploader.RootFolder;
                    for (var i = 0; i < parts.Length; i++)
                    {
                        var nextFolder = currentFolder.SubFolders.SingleOrDefault(f => f.Name == parts[i] || f.Name == string.Join("\\", parts.Take(i + 1)) || f.Name == relativeDirectory);
                        if (nextFolder == null)
                        {
                            // create new one inside folder
                            throw new NotImplementedException();
                        }
                        currentFolder = nextFolder;
                        if (currentFolder.Name == relativeDirectory)
                        {
                            break;
                        }
                    }
                    folder = currentFolder;
                    Console.WriteLine(directory + "\t->\t" + folder.Name);

                    // do stuff... check and see if the album exists and so forth
                    lastDirectory = directory;

                    // any duplicate filenames in this album?
                    var imagesByFilename = folder.Images.GroupBy(i => i.FileName).ToArray();
                    var duplicateFilenames = imagesByFilename.Where(g => g.Count() > 1).ToArray();
                    if (duplicateFilenames.Any())
                    {
                        Console.WriteLine("WARNING: duplicate filenames in album " + folder.Name);
                        foreach (var dupe in duplicateFilenames.Select(g => g.Key))
                        {
                            Console.WriteLine("\t" + dupe);
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

                    // see if it matches the image on disk
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Length != matchingImage.Size)
                    {
                        Console.WriteLine("WARNING: Size doesn't match for " + file + ": " + fileInfo.Length + " <> " + matchingImage.Size);
                    }
                    //if (matchingImage.Date.HasValue && fileInfo.LastWriteTime != matchingImage.Date)
                    //{
                    //    Debug.WriteLine("Date doesn't match!");
                    //}
                    var md5 = GetMd5(file);
                    if (md5 != matchingImage.MD5)
                    {
                        Console.WriteLine("WARNING: MD5 doesn't match for " + file + ": " + md5 + " <> " + matchingImage.MD5);
                    }

                }
                else
                {
                    // upload it?
                }
            }
        }

        private static string GetMd5(string file)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(file))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-","").ToLower();
                }
            }
        }
    }
}
