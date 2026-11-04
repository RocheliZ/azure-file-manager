using FileManager.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FileManager.Api.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController(IFileStorageService storage) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("A non-empty multipart field 'file' is required.");

        var name = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Invalid file name.");

        await using var stream = file.OpenReadStream();
        await storage.UploadAsync(name, stream, file.ContentType ?? "application/octet-stream", ct);

        return CreatedAtAction(nameof(Download), new { name }, new { name, size = file.Length });
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FileItem>>> List(CancellationToken ct)
        => Ok(await storage.ListAsync(ct));

    [HttpGet("{name}")]
    public async Task<IActionResult> Download(string name, CancellationToken ct)
    {
        var safeName = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(safeName))
            return BadRequest("Invalid file name.");

        var result = await storage.DownloadAsync(safeName, ct);
        if (result is null)
            return NotFound();

        return File(result.Content, result.ContentType, safeName);
    }
}
