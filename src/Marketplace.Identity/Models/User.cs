using Microsoft.AspNetCore.Identity;

namespace Marketplace.Identity.Models;

public class User : IdentityUser<Guid>
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}