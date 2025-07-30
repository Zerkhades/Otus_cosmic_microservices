using System.Diagnostics.CodeAnalysis;

namespace NotificationService.Infrastructure.Persistence
{
    public class DbInitializer
    {
        [SuppressMessage("ReSharper.DPA", "DPA0009: High execution time of DB command", MessageId = "time: 548ms")]
        public static void Initialize(AppDbContext context)
        {
            context.Database.EnsureCreated();
        }
    }
}
