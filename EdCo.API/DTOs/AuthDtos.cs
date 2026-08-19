namespace EdCo.API.DTOs
{
    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public int GradeLevelId { get; set; }
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RevokeTokenRequest
    {
        public string? RefreshToken { get; set; }
    }

    public class UpdateProfileRequest
    {
        public string? FullName { get; set; }
        public int GradeLevelId { get; set; }
        public string? Password { get; set; }
    }
}
