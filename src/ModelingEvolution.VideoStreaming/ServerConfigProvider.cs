using System.Text.Json;
using MicroPlumberd;
using Microsoft.Extensions.Configuration;

namespace ModelingEvolution.VideoStreaming;


public class ServerConfigProvider(IConfiguration conf, IPlumber plumber)
{
    private ServerConfig? _config;

    private Guid Id = Environment.MachineName.ToGuid();
    public async Task<ServerConfig?> Get()
    {
        if (_config != null) return _config;

        var urlToMerge = conf.GetConnections();
        _config = await plumber.GetState<ServerConfig>(Id) ?? new ServerConfig() { Id=Id};
        
        if (urlToMerge == null) return _config;

        foreach(var i in urlToMerge.Select(x=> new Uri(x)))
            if(!_config.Sources.Contains(i))
                _config.Sources.Add(i);

        return _config;
    }

    public async Task Reset()
    {
        if (_config == null) _config = new();
        _config.Sources.Clear();
        var urlToMerge = conf.GetConnections();

        foreach (var i in urlToMerge.Select(x => new Uri(x)))
            if (!_config.Sources.Contains(i))
                _config.Sources.Add(i);
        await Save();
    }
    public async Task Save()
    {
        if(_config == null) return;
        await plumber.AppendState(_config);
    }
}