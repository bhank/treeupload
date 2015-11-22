using System;
using coynesolutions.treeupload;

namespace treeupload
{
    class Program
    {
        static void Main(string[] args)
        {
            var x = new SmugMugUploader();

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
