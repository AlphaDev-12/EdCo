using EdCo.Core.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace EdCo.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class MediaController : ControllerBase
    {
        private readonly EdCoDbContext _context;
        private readonly IConfiguration _configuration;

        public MediaController(EdCoDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet("token")]
        public async Task<IActionResult> GetMediaToken([FromQuery] int unitId)
        {
            var video = await _context.VideoAssets.FirstOrDefaultAsync(v => v.UnitId == unitId);
            if (video == null) return NotFound("Video not found for this unit.");

            var bunnyConfig = _configuration.GetSection("BunnyNet");
            var securityKey = bunnyConfig["SecurityKey"];
            var libraryId = bunnyConfig["VideoLibraryId"];
            
            if (string.IsNullOrEmpty(securityKey) || string.IsNullOrEmpty(libraryId))
                return StatusCode(500, "CDN configuration missing.");

            // Generate a token valid for 1 hour
            var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
            var tokenString = $"{securityKey}{video.BunnyVideoId}{expires}";
            
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(tokenString));
            var token = Convert.ToBase64String(hashBytes)
                .Replace("\n", "")
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");

            var streamUrl = $"https://video.bunnycdn.com/play/{libraryId}/{video.BunnyVideoId}?token={token}&expires={expires}";

            return Ok(new { streamUrl });
        }
    }
}
