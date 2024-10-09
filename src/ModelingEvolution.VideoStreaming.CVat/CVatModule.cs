using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModelingEvolution.VideoStreaming.CVat;

public static class CVatModule
{
    public const string C_VAT_URL_KEY = "CVatUrl";
    public const string C_VAT_TOKEN_KEY = "CVatToken";
    public const string PUBLIC_URL_KEY  = "PublicUrl";

    public static IServiceCollection AddCVat(this IServiceCollection container)
    {
        container.AddSingleton<CVatFactory>();
        container.AddTransient<ICVatClient>(sp => sp.GetRequiredService<CVatFactory>().Create());
        return container;
    }

    public static string PublicUrl(this IConfiguration configuration)
    {
        return configuration.GetValue<string>(PUBLIC_URL_KEY) ?? "-";
    }
    public static string CVatUrl(this IConfiguration configuration)
    {
        return configuration.GetValue<string>(C_VAT_URL_KEY) ?? "-";
    }

    public static string CVatToken(this IConfiguration configuration)
    {
        return configuration.GetValue<string>(C_VAT_TOKEN_KEY) ?? "-";
    }
}