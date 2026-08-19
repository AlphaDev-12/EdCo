using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EdCo.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [AllowAnonymous]
    public class DocumentsController : ControllerBase
    {
        private readonly ILocalFileStorageService _storageService;

        public DocumentsController(ILocalFileStorageService storageService)
        {
            _storageService = storageService;
        }

        [HttpGet("{fileName}")]
        public IActionResult GetDocument(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return NotFound();

            try
            {
                var (stream, contentType) = _storageService.GetFileStream(fileName, "documents");
                return File(stream, contentType, fileName);
            }
            catch (FileNotFoundException)
            {
                return NotFound("Document not found.");
            }
            catch (UnauthorizedAccessException)
            {
                return BadRequest("Invalid filename specified.");
            }
            catch (Exception)
            {
                return StatusCode(500, "Error retrieving document.");
            }
        }
    }
}
