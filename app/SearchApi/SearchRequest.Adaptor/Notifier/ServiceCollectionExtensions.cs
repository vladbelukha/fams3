using BcGov.Fams3.SearchApi.Contracts.SearchRequest;
using Microsoft.Extensions.DependencyInjection;
using SearchRequestAdaptor.Auth;

namespace SearchRequestAdaptor.Notifier
{
    public static class ServiceCollectionExtensions
    {
        public static void AddWebHooks(this IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddTransient<DynadapterTokenHandler>();
            services.AddHttpClient("dynadapter_token");
            services.AddHttpClient<ISearchRequestNotifier<SearchRequestEvent>, WebHookSearchRequestNotifier>()
                .AddHttpMessageHandler<DynadapterTokenHandler>();
        }
    }
}
