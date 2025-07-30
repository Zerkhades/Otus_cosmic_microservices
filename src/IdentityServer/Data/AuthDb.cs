using IdentityServer.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Data
{
    public class AuthDb(DbContextOptions<AuthDb> opts)
        : IdentityDbContext<ApplicationUser>(opts);
}
