using BuffCore.Abstractions.Dtos;
using BuffCore.Integrations.Configurations;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuffCore.Integrations
{
    /// <summary>
    /// Provides injectable methods for uploading, downloading, and signing files in Google Cloud Storage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Construct with a <see cref="StorageConfig"/> (or register via
    /// <see cref="ServiceCollectionExtensions.AddBuffCoreIntegrations"/>) and inject where needed.
    /// Uploaded objects are made publicly readable, matching the original host behavior.
    /// </para>
    /// <para>
    /// Operations accept a <see cref="Stream"/> rather than a web-framework file abstraction,
    /// keeping this package usable outside ASP.NET Core. Hosts with <c>IFormFile</c> inputs
    /// pass <c>file.OpenReadStream()</c>.
    /// </para>
    /// </remarks>
    public class GoogleCloudStorageHelper(StorageConfig config, ILogger? logger = null)
    {
        private readonly ILogger _logger = logger ?? NullLogger.Instance;

        private readonly GoogleCredential _credential = CreateCredential(config);
        private readonly string _bucketName = config.GoogleCloudStorage.BucketName;

        private static GoogleCredential CreateCredential(StorageConfig config)
        {
            var serviceAccount = config.GoogleCloudStorage.ServiceAccount;

            if (string.IsNullOrWhiteSpace(serviceAccount?.Type) || string.IsNullOrWhiteSpace(serviceAccount.PrivateKey))
            {
                throw new InvalidOperationException(
                    "Google Cloud Storage configuration is missing a service account (type and private_key are required).");
            }

            // Serializing with Newtonsoft keeps the exact wire shape the Google SDK expects
            // (type/private_key/client_email as declared by [JsonProperty] on the config type).
            return CredentialFactory.FromJson(
                Newtonsoft.Json.JsonConvert.SerializeObject(serviceAccount),
                serviceAccount.Type);
        }

        /// <summary>
        /// Uploads a stream to the configured bucket at the given path with a generated identifier.
        /// The uploaded object will be publicly readable.
        /// </summary>
        /// <param name="stream">The content stream to upload.</param>
        /// <param name="contentType">The MIME content type of the object.</param>
        /// <param name="path">The folder path inside the bucket.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The full object name assigned in the bucket.</returns>
        public async Task<string> UploadFileAsync(Stream stream, string contentType, string path, CancellationToken cancellationToken)
        {
            var objectName = $"{path}/{Guid.NewGuid()}";

            _logger.LogInformation("Uploading file to bucket {BucketName} at path {ObjectName}.", _bucketName, objectName);

            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            using var storageClient = await StorageClient.CreateAsync(_credential);
            await storageClient.UploadObjectAsync(_bucketName, objectName, contentType, memoryStream, new UploadObjectOptions()
            {
                PredefinedAcl = PredefinedObjectAcl.PublicRead
            }, cancellationToken);

            _logger.LogInformation("File uploaded to bucket {BucketName} at path {ObjectName}.", _bucketName, objectName);
            return objectName;
        }

        /// <summary>
        /// Uploads a stream to the configured bucket under a specific identifier, skipping the upload
        /// when an object with that name already exists. The uploaded object will be publicly readable.
        /// </summary>
        /// <param name="stream">The content stream to upload.</param>
        /// <param name="fileId">The identifier for the object, used as its name inside <paramref name="path"/>.</param>
        /// <param name="contentType">The MIME content type of the object.</param>
        /// <param name="path">The folder path inside the bucket.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The full object name in the bucket.</returns>
        public async Task<string> UploadFileAsync(Stream stream, string fileId, string contentType, string path, CancellationToken cancellationToken)
        {
            var objectName = $"{path}/{fileId}";

            _logger.LogInformation("Uploading file to bucket {BucketName} at path {ObjectName}.", _bucketName, objectName);

            try
            {
                using var storageClient = await StorageClient.CreateAsync(_credential);
                var existingObject = await storageClient.GetObjectAsync(_bucketName, objectName, cancellationToken: cancellationToken);

                if (existingObject != null)
                {
                    _logger.LogInformation(
                        "File already exists in bucket {BucketName} at path {ObjectName}. Skipping upload.",
                        _bucketName,
                        objectName);

                    return objectName;
                }
            }
            catch (Google.GoogleApiException ex)
                when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // The object does not exist yet; proceed with the upload.
            }

            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);

            using var uploadClient = await StorageClient.CreateAsync(_credential);
            await uploadClient.UploadObjectAsync(_bucketName, objectName, contentType, memoryStream, new UploadObjectOptions()
            {
                PredefinedAcl = PredefinedObjectAcl.PublicRead
            }, cancellationToken: cancellationToken);

            _logger.LogInformation("File uploaded to bucket {BucketName} at path {ObjectName}.", _bucketName, objectName);
            return objectName;
        }

        /// <summary>
        /// Retrieves a signed, time-limited URL for accessing a file stored in Google Cloud Storage.
        /// </summary>
        /// <param name="objectName">The full name of the object in the bucket.</param>
        /// <param name="fileName">The file name presented to the browser on download.</param>
        /// <param name="durationInSeconds">The duration in seconds for which the signed URL should be valid. Default is 60 seconds.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>The signed URL string.</returns>
        public async Task<string> RetrieveSignedUrlFileAsync(string objectName, string fileName, int durationInSeconds = 60, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Generating signed URL for object {ObjectName} in bucket {BucketName} with expiration {Duration} seconds.",
                objectName, _bucketName, durationInSeconds);

            var queryParams = new Dictionary<string, IEnumerable<string>>
            {
                { "response-content-disposition", new List<string> { $"attachment; filename=\"{fileName}\"" } }
            };

            var requestTemplate = UrlSigner.RequestTemplate
                .FromBucket(_bucketName)
                .WithObjectName(objectName)
                .WithHttpMethod(HttpMethod.Get)
                .WithQueryParameters(queryParams);

            var options = UrlSigner.Options.FromDuration(TimeSpan.FromSeconds(durationInSeconds));

            var urlSigner = UrlSigner.FromCredential(_credential);
            var url = await urlSigner.SignAsync(requestTemplate, options, cancellationToken: cancellationToken);

            _logger.LogInformation("Signed URL generated for object {ObjectName} in bucket {BucketName}.", objectName, _bucketName);
            return url;
        }

        /// <summary>
        /// Downloads a file from the configured bucket in Google Cloud Storage.
        /// </summary>
        /// <param name="objectName">The full name of the object in the bucket to download.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
        /// <returns>A <see cref="FileDto"/> with the file stream, name, and content type.</returns>
        public async Task<FileDto> DownloadFileAsync(string objectName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Downloading file from bucket {BucketName} at path {ObjectName}.", _bucketName, objectName);

            using var storageClient = await StorageClient.CreateAsync(_credential);
            var stream = new MemoryStream();
            var downloadedFile = await storageClient.DownloadObjectAsync(_bucketName, objectName, stream, cancellationToken: cancellationToken);

            stream.Position = 0;

            _logger.LogInformation("File downloaded from bucket {BucketName} at path {ObjectName}.", _bucketName, objectName);
            return new FileDto
            {
                FileStream = stream,
                FileName = downloadedFile.Name,
                ContentType = downloadedFile.ContentType
            };
        }
    }
}
