using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Content.Domain.Entities;

public sealed class MediaAttachment : Entity<Guid>
{
    public string FileName { get; }
    public string MimeType { get; }
    public long SizeBytes { get; }
    public string StorageUri { get; }

    private MediaAttachment(Guid id, string fileName, string mimeType,
        long sizeBytes, string storageUri) : base(id)
    {
        FileName = fileName;
        MimeType = mimeType;
        SizeBytes = sizeBytes;
        StorageUri = storageUri;
    }

    public static MediaAttachment Create(string fileName, string mimeType,
        long sizeBytes, string storageUri)
    {
        Guard.NotNullOrWhiteSpace(fileName, nameof(fileName));
        Guard.NotNullOrWhiteSpace(mimeType, nameof(mimeType));
        Guard.NotNullOrWhiteSpace(storageUri, nameof(storageUri));
        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        return new MediaAttachment(Guid.NewGuid(), fileName.Trim(), mimeType.Trim(),
            sizeBytes, storageUri.Trim());
    }
}
