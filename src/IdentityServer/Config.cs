using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace IdentityServer
{
    public class Config
    {
        public static IEnumerable<IdentityResource> IdentityResources =>
            new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile()
            };
        public static IEnumerable<ApiResource> ApiResources =>
    new List<ApiResource>
    {
        new ApiResource("player-api", "Player API") { Scopes = { "player-api" } },
        new ApiResource("tournament-api", "Tournament API") { Scopes = { "tournament-api" } },
        new ApiResource("battle-api", "Battle API") { Scopes = { "battle-api" } },
        new ApiResource("notification-api", "Notification API") { Scopes = { "notification-api" } },
    };
        public static IEnumerable<ApiScope> ApiScopes =>
            new List<ApiScope>
        {
            new ApiScope("player-api", "Player API"),
            new ApiScope("tournament-api", "Tournament API"),
            new ApiScope("battle-api", "Battle API"),
            new ApiScope("notification-api", "Notification API")
        };

        public static IEnumerable<Client> Clients =>
            new List<Client>
        {
        new Client {
          ClientId = "cosmic-web",
          AllowedGrantTypes = GrantTypes.Code,
          RequirePkce = true,
          RequireClientSecret = false,
          RedirectUris = { "http://localhost:5173/callback", "http://192.168.9.142:5173/callback" },
          PostLogoutRedirectUris = { "http://localhost:5173", "http://192.168.9.142:5173" },
          AllowedCorsOrigins = { "http://localhost:5173", "http://192.168.9.142:5173" },
          AllowedScopes = {
            IdentityServerConstants.StandardScopes.OpenId,
            IdentityServerConstants.StandardScopes.Profile,
            "player-api","tournament-api","battle-api","notification-api"
          },
          AllowOfflineAccess = true,
          AccessTokenType = AccessTokenType.Jwt   // ← явно задаём JWT
        }
    };
    }
}
