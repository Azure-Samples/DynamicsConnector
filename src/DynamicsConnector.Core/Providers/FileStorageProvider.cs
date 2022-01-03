using Azure.Storage;
using Azure.Storage.Files.Shares;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DynamicsConnector.Core.Providers
{
    public class FileStorageProvider
    {
        private readonly string StorageAccountName;
        private readonly string StorageAccountKey;
        private readonly string BlobServiceUri;
        private ShareServiceClient shareServiceClient;


        public FileStorageProvider(string storageAccountName, string storageAccountKey, string blobServiceUri)
        {
            this.StorageAccountName = storageAccountName;
            this.StorageAccountKey = storageAccountKey;
            this.BlobServiceUri = blobServiceUri;
            shareServiceClient = new ShareServiceClient(new Uri(blobServiceUri), new StorageSharedKeyCredential(storageAccountName, storageAccountKey), default);
        }
        public string ReadFromFile(string shareName, string fileName)
        {
            var share = shareServiceClient.GetShareClient(shareName);
            var rootdir = share.GetRootDirectoryClient();
            var data = rootdir.GetFileClient(fileName).Download();
            return data.ToString();
        }
    }
}
