using CoffeeNChill.ApiGateway.Dtos;
using CoffeeNChill.ApiGateway.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.ApiGateway.Controllers;

// Microsoft Learn - Quickstart: Azure Blob Storage client library for .NET
// https://learn.microsoft.com/en-us/azure/storage/blobs/storage-quickstart-blobs-dotnet?tabs=azure-portal%2Cidentity-based-access%2Cnodejs-v1%2Cpivots=programming-language-csharp
// Microsoft Learn - ASP.NET Core authorization with roles
// https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-10.0
[ApiController]
[Route("api/documents")]
[Tags("Documents")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly ILogger<DocumentController> _logger;

    public DocumentController(IDocumentService documentService, ILogger<DocumentController> logger)
    {
        _documentService = documentService;
        _logger = logger;
    }

    /// <summary>Lists every stored staff document.</summary>
    /// <response code="200">The document list was returned.</response>
    /// <response code="401">No valid Bearer token was supplied.</response>
    /// <response code="403">The caller is not an Admin or Staff member.</response>
    /// <response code="500">The blob container could not be reached.</response>
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> List(CancellationToken cancellationToken)
    {
        var documents = await _documentService.ListAsync(cancellationToken);
        return Ok(documents);
    }

    /// <summary>Uploads a staff document, overwriting any blob with the same name.</summary>
    /// <param name="file">The file to store, sent as <c>multipart/form-data</c>.</param>
    /// <response code="201">The document was uploaded.</response>
    /// <response code="400">No file, or an unusable file name, was supplied.</response>
    /// <response code="401">No valid Bearer token was supplied.</response>
    /// <response code="403">The caller is not an Admin or Staff member.</response>
    /// <response code="500">The blob container could not be reached.</response>
    [Authorize(Roles = "Admin,Staff")]
    [HttpPost]
    [Consumes("multipart/form-data")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(UploadDocumentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UploadDocumentResponse>> Upload(
        IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "A non-empty file is required." });

        await using var stream = file.OpenReadStream();
        var result = await _documentService.UploadAsync(
            file.FileName, stream, file.ContentType, cancellationToken);

        _logger.LogInformation("Uploaded document {Name} as {User}.", result.Name, User.Identity?.Name);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Downloads a staff document.</summary>
    /// <param name="fileName">Blob name, including extension.</param>
    /// <response code="200">The file was returned.</response>
    /// <response code="401">No valid Bearer token was supplied.</response>
    /// <response code="403">The caller is not an Admin or Staff member.</response>
    /// <response code="404">No such document.</response>
    /// <response code="500">The blob container could not be reached.</response>
    [Authorize(Roles = "Admin,Staff")]
    [HttpGet("download/{fileName}")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Download(
        [FromRoute] string fileName, CancellationToken cancellationToken)
    {
        var (content, contentType, _, name) =
            await _documentService.DownloadAsync(fileName, cancellationToken);

        return File(content, contentType, name);
    }

    /// <summary>Deletes a staff document.</summary>
    /// <param name="fileName">Blob name, including extension.</param>
    /// <response code="204">The document was deleted.</response>
    /// <response code="401">No valid Bearer token was supplied.</response>
    /// <response code="403">The caller is not an Admin or Staff member.</response>
    /// <response code="404">No such document.</response>
    /// <response code="500">The blob container could not be reached.</response>
    [Authorize(Roles = "Admin,Staff")]
    [HttpDelete("{fileName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(
        [FromRoute] string fileName, CancellationToken cancellationToken)
    {
        await _documentService.DeleteAsync(fileName, cancellationToken);
        _logger.LogInformation("Deleted document {FileName} as {User}.", fileName, User.Identity?.Name);
        return NoContent();
    }
}
