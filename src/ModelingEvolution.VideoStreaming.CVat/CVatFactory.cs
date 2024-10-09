using Microsoft.Extensions.Configuration;

namespace ModelingEvolution.VideoStreaming.CVat;

public class CVatFactory
{
    private readonly string? _token;
    private readonly string? _url;

    public CVatFactory(IConfiguration configuration)
    {
        _url = configuration.CVatUrl();
        _token = configuration.CVatToken();
    }

    public ICVatClient Create()
    {
        return new CVatClient(_url, _token);
    }
}