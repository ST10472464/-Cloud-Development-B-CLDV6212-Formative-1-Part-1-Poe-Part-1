namespace CoffeeNChill.ApiGateway.Dtos;

/// <summary>Metadata describing a blob in the <c>staff-documents</c> container.</summary>
public sealed class DocumentDto
{
    /// <summary>File name, including extension.</summary>
    public string Name { get; set; } = default!;

    /// <summary>File size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>Content type recorded when the blob was uploaded.</summary>
    public string ContentType { get; set; } = default!;

    /// <summary>UTC last-modified timestamp reported by Azure Blob Storage.</summary>
    public DateTimeOffset? LastModified { get; set; }
}

/// <summary>Response returned after a document is uploaded successfully.</summary>
public sealed class UploadDocumentResponse
{
    /// <summary>Name the blob was stored under.</summary>
    public string Name { get; set; } = default!;

    /// <summary>File size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>Confirmation message.</summary>
    public string Message { get; set; } = default!;
}
