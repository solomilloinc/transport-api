namespace Transport.Domain.Users;

public class RefreshToken
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByIp { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }

    public string? ReplacedByToken { get; set; }

    public bool IsActive => RevokedAt == null && !IsExpired;
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
}

