using System;
using coynesolutions.treeupload;

namespace treeupload
{
    class Program
    {
        static void Main(string[] args)
        {
            var x = new SmugMugUploader();
            Console.WriteLine(x.NickName);
            Console.ReadKey();
        }
    }
}
