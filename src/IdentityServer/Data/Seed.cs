using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Data
{
    public static class Seed
    {
        public static async Task CreateTestUser(IServiceProvider sp)
        {
            await using var scope = sp.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AuthDb>();
            var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            await ctx.Database.MigrateAsync();

            const string userName = "demo";
            const string pass = "Pass123$";

            if (await userMgr.FindByNameAsync(userName) is null)
            {
                var user = new ApplicationUser { UserName = userName, Email = "demo@local" };
                var res = await userMgr.CreateAsync(user, pass);
                if (res.Succeeded)
                    Console.WriteLine($"Seed user '{userName}/{pass}' created");
                else
                    Console.WriteLine(string.Join(';', res.Errors.Select(e => e.Description)));
            }
        }
    }
}
