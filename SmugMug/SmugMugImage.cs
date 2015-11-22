namespace coynesolutions.treeupload.SmugMug
{
    public class SmugMugImage : IImage
    {
        private readonly dynamic albumImageData;

        public SmugMugImage(dynamic albumImageData)
        {
            this.albumImageData = albumImageData;
        }

        public string FileName
        {
            get { return albumImageData.FileName; }
        }
    }
}
