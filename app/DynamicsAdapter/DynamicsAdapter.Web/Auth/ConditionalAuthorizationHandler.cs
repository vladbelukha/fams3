using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Threading.Tasks;

namespace DynamicsAdapter.Web.Auth
{
    /// <summary>
    /// Authorization handler that respects the <c>auth:jwt:enabled</c> feature flag.
    /// When the flag is false, all requests are allowed through regardless of authentication state.
    /// When the flag is true, the request must carry a valid JWT bearer token.
    /// </summary>
    public class ConditionalAuthorizationHandler : AuthorizationHandler<ConditionalAuthorizationRequirement>
    {
        private readonly IConfiguration _configuration;

        public ConditionalAuthorizationHandler(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ConditionalAuthorizationRequirement requirement)
        {
            // Default to true — if no config value is present, enforce JWT (fail secure)
            var authEnabled = _configuration.GetValue<bool>("auth:jwt:enabled", defaultValue: true);

            if (!authEnabled)
            {
                Log.Debug("Authentication is disabled via feature flag. Allowing unauthenticated access.");
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            if (context.User.Identity?.IsAuthenticated == true)
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
