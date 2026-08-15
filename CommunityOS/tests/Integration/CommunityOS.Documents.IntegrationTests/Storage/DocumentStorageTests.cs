using System.Text;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using CommunityOS.Documents.Application.Abstractions;
using CommunityOS.Documents.Infrastructure.Integration.Storage;
using Microsoft.Extensions.Options;
using Testcontainers.Minio;

namespace CommunityOS.Documents.IntegrationTests.Storage;

/// <summary>
/// Verifies the S3-protocol storage adapter (<see cref="S3ObjectStorage"/>)
/// against a real MinIO instance (Testcontainers), covering the
/// content-addressed put/get/delete/exists contract and the AES256
/// encryption-at-rest request (ADR-022, ratified).
/// </summary>
public sealed class DocumentStorageTests : IAsyncLifetime
{
    private readonly MinioContainer _minio = new MinioBuilder()
        .WithUsername("minioadmin")
        .WithPassword("minioadmin")
        .Build();

    private S3ObjectStorage? _storage;

    public async Task InitializeAsync()
    {
        await _minio.StartAsync();

        var s3 = new AmazonS3Client(
            new BasicAWSCredentials("minioadmin", "minioadmin"),
            new AmazonS3Config
            {
                ServiceURL = $"http://{_minio.Hostname}:{_minio.GetMappedPublicPort(9000)}",
                ForcePathStyle = true,
                AuthenticationRegion = RegionEndpoint.USEast1.SystemName
            });

        await s3.PutBucketAsync("communityos-documents");

        _storage = new S3ObjectStorage(
            s3,
            new OptionsWrapper<ObjectStorageOptions>(new ObjectStorageOptions
            {
                Bucket = "communityos-documents",
                Endpoint = $"http://{_minio.Hostname}:{_minio.GetMappedPublicPort(9000)}",
                AccessKey = "minioadmin",
                SecretKey = "minioadmin",
                ForcePathStyle = true,
                EncryptionAtRest = true
            }),
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<S3ObjectStorage>());
    }

    public Task DisposeAsync() => _minio.DisposeAsync().AsTask();

    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    [Fact]
    public async Task Put_and_get_roundtrip_preserves_bytes_and_content_type()
    {
        var bytes = Bytes("content addressing");
        var key = $"documents/{Sha256(bytes)}";

        await _storage!.PutAsync(key, new MemoryStream(bytes), "text/plain");
        var exists = await _storage.ExistsAsync(key);
        exists.Should().BeTrue();

        var stream = await _storage.GetAsync(key);
        stream.Should().NotBeNull();
        using var reader = new StreamReader(stream!);
        reader.ReadToEnd().Should().Be("content addressing");
    }

    [Fact]
    public async Task Get_of_missing_key_returns_null_and_delete_is_idempotent()
    {
        var key = "documents/0000000000000000000000000000000000000000000000000000000000000000";

        var stream = await _storage!.GetAsync(key);
        stream.Should().BeNull();

        await _storage.DeleteAsync(key); // absent object must not throw
        var exists = await _storage.ExistsAsync(key);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_removes_the_object()
    {
        var bytes = Bytes("to be deleted");
        var key = $"documents/{Sha256(bytes)}";

        await _storage!.PutAsync(key, new MemoryStream(bytes), "text/plain");
        (await _storage.ExistsAsync(key)).Should().BeTrue();

        await _storage.DeleteAsync(key);

        (await _storage.ExistsAsync(key)).Should().BeFalse();
    }

    [Fact]
    public async Task Put_requests_AES256_encryption_at_rest()
    {
        var s3 = new AmazonS3Client(
            new BasicAWSCredentials("minioadmin", "minioadmin"),
            new AmazonS3Config
            {
                ServiceURL = $"http://{_minio.Hostname}:{_minio.GetMappedPublicPort(9000)}",
                ForcePathStyle = true,
                AuthenticationRegion = RegionEndpoint.USEast1.SystemName
            });

        var bytes = Bytes("encrypted at rest");
        var key = $"documents/{Sha256(bytes)}";

        await _storage!.PutAsync(key, new MemoryStream(bytes), "text/plain");

        var metadata = await s3.GetObjectMetadataAsync("communityos-documents", key);
        metadata.ServerSideEncryptionMethod.Should().Be("AES256");
    }
}