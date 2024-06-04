using System;

namespace coynesolutions.treeupload.SmugMug
{
    public class SmugMugImage : SmugMugBase, IImage
    {
        private readonly dynamic albumImageData;
        private readonly Lazy<dynamic> metadataJsonLazy;

        public SmugMugImage(dynamic albumImageData)
        {
            this.albumImageData = albumImageData;
            metadataJsonLazy = new Lazy<dynamic>(() => GetJson(MetadataUri + "?_verbosity=1"));
        }

        public string FileName
        {
            get { return albumImageData.FileName; }
        }

        public long Size
        {
            get { return (long) albumImageData.OriginalSize; }
        }

        public string MD5
        {
            get { return albumImageData.ArchivedMD5; }
        }

        public long ArchivedSize
        {
            get { return (long) albumImageData.ArchivedSize; }
        }

        public DateTime UploadedDate
        {
            get { return (DateTime) albumImageData.Date; }
        }

        public string MetadataUri { get { return (string)albumImageData.Uris.ImageMetadata; } }
        public string ImageUri { get { return (string)albumImageData.Uris.Image; } }
        public string AlbumImageUri { get { return (string)albumImageData.Uri; } }

        private dynamic MetadataJson
        {
            get { return metadataJsonLazy.Value.Response.ImageMetadata; }
        }

        public DateTime? ExifDateTime
        {
            get
            {
                foreach (var dateTimePropertyName in new[] {"DateDigitized", "DateTimeCreated", "DateCreated", "DateTimeModified", "DateTime"})
                {
                    var jsonValue = MetadataJson[dateTimePropertyName];
                    if (jsonValue != null && jsonValue.Value is DateTime)
                    {
                        return (DateTime) jsonValue.Value;
                    }
                }
                return null;
            }
        }

        public string Camera
        {
            get { return (string) MetadataJson.Camera; }
        }

        public string Model
        {
            get { return (string)MetadataJson.Model; }
        }

        /// <summary>Delete image (removing it from all albums)</summary>
        public void Delete()
        {
            var responseJson = DeleteJson(ImageUri + "?_verbosity=1");
            if (responseJson.Code != 200 || responseJson.Message != "Ok")
            {
                throw new Exception("Unexpected response to image delete request: " + responseJson.Code + ", " + responseJson.Message);
            }
        }
    }
}
