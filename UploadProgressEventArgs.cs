namespace coynesolutions.treeupload
{
    public delegate void UploadProgressEventHandler(object sender, UploadProgressEventArgs e);

    public class UploadProgressEventArgs
    {
        public UploadProgressEventArgs(long bytesComplete, long bytesTotal)
        {
            this.BytesComplete = bytesComplete;
            this.BytesTotal = bytesTotal;
        }

        private long BytesComplete { get; set; }
        private long BytesTotal { get; set; }

        public double FractionComplete
        {
            get { return (double) BytesComplete/BytesTotal; }
        }

        public double PercentComplete
        {
            get { return FractionComplete*100; }
        }
    }
}