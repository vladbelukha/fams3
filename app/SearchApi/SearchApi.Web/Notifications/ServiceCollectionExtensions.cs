using BcGov.Fams3.SearchApi.Contracts.PersonSearch;
using Microsoft.Extensions.DependencyInjection;
using SearchApi.Web.Auth;

namespace SearchApi.Web.Notifications
{
    public static class ServiceCollectionExtensions
    {
        public static void AddWebHooks(this IServiceCollection services)
        {
            services.AddMemoryCache();
            services.AddTransient<DynadapterTokenHandler>();
            services.AddHttpClient("dynadapter_token");
            services.AddHttpClient<ISearchApiNotifier<PersonSearchAdapterEvent>, WebHookNotifierSearchEventStatus>()
                .AddHttpMessageHandler<DynadapterTokenHandler>();
        }
    }
}