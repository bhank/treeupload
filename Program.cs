using System;
using System.Linq;
using coynesolutions.treeupload;

namespace treeupload
{
    class Program
    {
        static void Main(string[] args)
        {
            var x = new SmugMugUploader();

            var album = x.RootFolder.SubFolders.Single(f => f.Name == "2015").SubFolders.Single(f => f.Name == "2015\\20150928");
            foreach (var image in album.Images)
            {
                Console.WriteLine(image.FileName);
            }
            Console.WriteLine("Press a key...");
            Console.ReadKey();
            return;

            foreach (var folder in x.RootFolder.SubFolders)
            {
                Console.WriteLine(folder.Name);
                foreach (var subfolder in folder.SubFolders)
                {
                    Console.WriteLine("\t" + subfolder.Name + "\t" + ((SmugMugFolder)subfolder).Type);
                }
            }
            Console.ReadKey();
        }
    }
}
