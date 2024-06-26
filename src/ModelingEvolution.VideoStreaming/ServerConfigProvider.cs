﻿using System.Text.Json;
using EventPi.Abstractions;
using MicroPlumberd;
using Microsoft.Extensions.Configuration;

namespace ModelingEvolution.VideoStreaming;


public class ServerConfigProvider(IConfiguration conf, IPlumber plumber, IEnvironment env)
{
    private ServerConfig? _config;

    private readonly Guid _id = env.HostName.ToString().ToGuid();
    public async Task<ServerConfig?> Get()
    {
        if (_config != null) return _config;

        var urlToMerge = conf.GetConnections();
        _config = await plumber.GetState<ServerConfig>(_id) ?? new ServerConfig() { Id=_id};
        
        
        foreach(var i in urlToMerge.Select(x=> new Uri(x)))
            if(!_config!.Sources.Contains(i))
                _config.Sources.Add(i);

        return _config;
    }

    public async Task Reset()
    {
        _config ??= new ServerConfig();
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
        await plumber.AppendState(_config, _id);
    }
}