using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using CommunityOS.Documents.Application.Abstractions;
using CommunityOS.Documents.Infrastructure.Integration.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CommunityOS.Documents.Infrastructure.Integration.Storage;

/// <summary>
/// S3-protocol implementation of <see cref="IDocumentObjectStorage"/> backed by
/// the AWSSDK.S3 client (ADR-022, ratified: MinIO in development, any
/// S3-compatible endpoint in production). Content is stored under
/// content-addressed keys (<c>documents/{sha256}</c>) so duplicate uploads are
/// cheap and integrity is verifiable on download.
/// </summary>
public sealed class S3ObjectStorage(
    IAmazonS3 s3Client,
    IOptions<ObjectStorageOptions> options,
    ILogger<S3ObjectStorage> logger) : IDocumentObjectStorage
{
    private readonly ObjectStorageOptions _options = options.Value;

    public async Task PutAsync(string objectKey, Stream content, string contentType, CancellationToken ct = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };

        if (_options.EncryptionAtRest)
            request.ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256;

        await s3Client.PutObjectAsync(request, ct);
        logger.ObjectStored(objectKey);
    }

    public async Task<Stream?> GetAsync(string objectKey, CancellationToken ct = default)
    {
        try
        {
            var response = await s3Client.GetObjectAsync(_options.Bucket, objectKey, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string objectKey, CancellationToken ct = default)
    {
        try
        {
            await s3Client.DeleteObjectAsync(_options.Bucket, objectKey, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Already absent — deletion is best effort cleanup.
        }
    }

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken ct = default)
    {
        try
        {
            await s3Client.GetObjectMetadataAsync(_options.Bucket, objectKey, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}

internal static partial class S3ObjectStorageLogging
{
    [LoggerMessage(
        EventId = 0,
        Level = LogLevel.Debug,
        Message = "Object {ObjectKey} stored in bucket.")]
    public static partial void ObjectStored(this ILogger logger, string objectKey);
}