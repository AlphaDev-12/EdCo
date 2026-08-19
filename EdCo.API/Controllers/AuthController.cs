using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EdCo.Core.Entities;
using EdCo.API.DTOs;
using EdCo.Core.Data;
using EdCo.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;


namespace EdCo.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly EdCoDbContext _context;
        private readonly IAuditLogService _auditLogService;


        public AuthController(
            IConfiguration configuration,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            EdCoDbContext context,
            IAuditLogService auditLogService)
        {
            _configuration = configuration;
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _auditLogService = auditLogService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            int assignedGradeId = request.GradeLevelId;
            if (assignedGradeId <= 0)
            {
                var firstGrade = await _context.GradeLevels.OrderBy(g => g.Id).FirstOrDefaultAsync();
                assignedGradeId = firstGrade?.Id ?? 1;
            }

            var user = new AppUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                GradeLevelId = assignedGradeId
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                return BadRequest(new { success = false, errors = result.Errors.Select(e => e.Description) });
            }

            await _auditLogService.LogStudentActivityAsync(
                activityType: "StudentRegister",
                studentId: user.Id,
                studentEmail: user.Email,
                details: $"Registered new student account for {user.Email}",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new { success = true, message = "User registered successfully." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "Invalid email or password." });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (!result.Succeeded)
            {
                return Unauthorized(new { success = false, message = "Invalid email or password." });
            }

            // Ensure GradeLevelId is assigned
            if (!user.GradeLevelId.HasValue || user.GradeLevelId.Value <= 0)
            {
                var firstGrade = await _context.GradeLevels.OrderBy(g => g.Id).FirstOrDefaultAsync();
                if (firstGrade != null)
                {
                    user.GradeLevelId = firstGrade.Id;
                    await _userManager.UpdateAsync(user);
                }
            }

            var tokenString = await GenerateJwtTokenAsync(user);
            var refreshTokenString = await GenerateRefreshTokenAsync(user.Id, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0");

            await _auditLogService.LogStudentActivityAsync(
                activityType: "StudentLogin",
                studentId: user.Id,
                studentEmail: user.Email,
                details: $"Student logged in: {user.Email}",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

            return Ok(new 
            { 
                success = true, 
                token = tokenString, 
                refreshToken = refreshTokenString,
                user = new {
                    user.Id,
                    user.Email,
                    user.FullName,
                    user.GradeLevelId,
                    user.IsSubscribed,
                    user.SubscriptionEndDate
                }
            });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new { success = false, message = "Refresh token is required." });
            }

            var tokenHash = HashToken(request.RefreshToken);
            var existingToken = await _context.RefreshTokens
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.TokenHash == tokenHash);

            if (existingToken == null || !existingToken.IsActive || existingToken.User == null)
            {
                return Unauthorized(new { success = false, message = "Invalid or expired refresh token." });
            }

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";

            // Revoke current token
            existingToken.RevokedAt = DateTime.UtcNow;
            existingToken.RevokedByIp = ipAddress;

            // Generate new token pair
            var newAccessToken = await GenerateJwtTokenAsync(existingToken.User);
            var newRefreshToken = await GenerateRefreshTokenAsync(existingToken.User.Id, ipAddress);

            var newHash = HashToken(newRefreshToken);
            existingToken.ReplacedByTokenHash = newHash;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                token = newAccessToken,
                refreshToken = newRefreshToken
            });
        }

        [Authorize]
        [HttpPost("revoke-token")]
        public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var tokenToRevoke = request.RefreshToken;
            if (string.IsNullOrWhiteSpace(tokenToRevoke))
            {
                // Revoke all active refresh tokens for the authenticated user
                var activeTokens = await _context.RefreshTokens
                    .Where(r => r.UserId == userId && r.RevokedAt == null && r.ExpiresAt > DateTime.UtcNow)
                    .ToListAsync();

                foreach (var token in activeTokens)
                {
                    token.RevokedAt = DateTime.UtcNow;
                    token.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                }
            }
            else
            {
                var hash = HashToken(tokenToRevoke);
                var token = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash && r.UserId == userId);
                if (token != null && token.IsActive)
                {
                    token.RevokedAt = DateTime.UtcNow;
                    token.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString();
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Token(s) revoked successfully." });
        }

        [HttpGet("grade-levels")]
        public IActionResult GetGradeLevels()
        {
            var grades = _context.GradeLevels.Select(g => new { g.Id, g.Name }).ToList();
            return Ok(new { success = true, data = grades });
        }

        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized(new { success = false, message = "Not authenticated." });

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized(new { success = false, message = "User not found." });

            if (!string.IsNullOrEmpty(request.FullName)) user.FullName = request.FullName;
            if (request.GradeLevelId > 0) user.GradeLevelId = request.GradeLevelId;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new { success = false, errors = result.Errors.Select(e => e.Description) });
            }

            if (!string.IsNullOrEmpty(request.Password))
            {
                var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var passwordResult = await _userManager.ResetPasswordAsync(user, resetToken, request.Password);
                if (!passwordResult.Succeeded)
                {
                    return BadRequest(new { success = false, errors = passwordResult.Errors.Select(e => e.Description) });
                }
            }

            var tokenString = await GenerateJwtTokenAsync(user);

            return Ok(new 
            { 
                success = true, 
                message = "Profile updated successfully.",
                token = tokenString,
                user = new {
                    user.Id,
                    user.Email,
                    user.FullName,
                    user.GradeLevelId,
                    user.IsSubscribed,
                    user.SubscriptionEndDate
                }
            });
        }

        [Authorize]
        [HttpGet("ai-usage")]
        public async Task<IActionResult> GetAiUsage()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var currentMonth = DateTime.UtcNow.Month;
            var currentYear = DateTime.UtcNow.Year;

            var costUsed = await _context.AiInteractionLogs
                .Where(l => l.AppUserId == userId && l.Timestamp.Month == currentMonth && l.Timestamp.Year == currentYear)
                .SumAsync(l => l.Cost);

            decimal monthlyLimit = decimal.TryParse(_configuration["AiSettings:MonthlyStudentLimit"], out var limit) ? limit : 0.50m;

            return Ok(new
            {
                success = true,
                data = new
                {
                    costUsed = costUsed,
                    costLimit = monthlyLimit
                }
            });
        }

        private async Task<string> GenerateRefreshTokenAsync(string userId, string ipAddress)
        {
            var randomBytes = new byte[64];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            var tokenString = Convert.ToBase64String(randomBytes);
            var tokenHash = HashToken(tokenString);

            var refreshToken = new RefreshToken
            {
                TokenHash = tokenHash,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            return tokenString;
        }

        private string HashToken(string token)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(token);
                var hashBytes = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hashBytes);
            }
        }

        private async Task<string> GenerateJwtTokenAsync(AppUser user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var secretKey = jwtSettings["Key"] ?? jwtSettings["SecretKey"] ?? "Development_SecretKey_Minimum32CharsLong_EdCo_2026!";

            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.FullName ?? ""),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim("GradeLevelId", (user.GradeLevelId.HasValue && user.GradeLevelId.Value > 0) ? user.GradeLevelId.Value.ToString() : "1"),
                new Claim("IsSubscribed", user.IsSubscribed.ToString())
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"] ?? "https://edco-api.production.com",
                audience: jwtSettings["Audience"] ?? "https://edco-app.production.com",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(15), // 15-minute access token lifespan
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
