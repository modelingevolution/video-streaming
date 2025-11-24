using System.Buffers;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelingEvolution.VideoStreaming.Chasers;

namespace ModelingEvolution.VideoStreaming;

public class StreamMultiplexer : IStreamMultiplexer
{
    public event EventHandler Stopped;
    public event EventHandler Disconnected;
    public const int BUFFER_SIZE = 20 * 1024 * 1024;
    public const int CHUNK = 1024;
    private Memory<byte> _buffer;
    private readonly List<IChaser> _chasers;
    private readonly StreamBase _source;
    private readonly ILogger<StreamMultiplexer> _logger;
    //private Thread _reader;
    private int _readOffset;
    private bool _stopped;

    Memory<byte> IStreamMultiplexer.Buffer()
    {
        return _buffer;
    }

    int IStreamMultiplexer.ReadOffset => _readOffset;

    /// <summary>
    /// Contains number of bytes read in _buffer. This is if we written 2 times buffer size, than it will be 2x size of the buffer.
    /// It doesn't contain informaiton about current offset in the current buffer - this is in _readOffset.
    /// </summary>
    private ulong _totalBuffersRead;
    public ulong TotalReadBytes => _totalBuffersRead + (ulong)_readOffset;
    
    public IReadOnlyList<IChaser> Chasers => _chasers.AsReadOnly();
    private readonly byte[] _sharedBuffer;
    private IList<IChaser> _chasers1;
    private CancellationTokenSource _cts;
    public StreamMultiplexer(StreamBase source, ILogger<StreamMultiplexer> logger)
    {
        _sharedBuffer = ArrayPool<byte>.Shared.Rent(BUFFER_SIZE);
        _buffer = _sharedBuffer.AsMemory(0,BUFFER_SIZE);
        _chasers = new List<IChaser>();
        _source = source;
        _logger = logger;
    }

    
    public ulong BufferLength  => (ulong)_buffer.Length;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Factory.StartNew(OnReadAsync, TaskCreationOptions.LongRunning);
    }
    public int AvgPipelineExecution { get; } = 0;



    void IStreamMultiplexer.Disconnect(IChaser chaser)
    {
        _chasers.Remove(chaser);
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    public IChaser Chase(Stream destination, Func<byte, int?> validStart = null, string identifier = null)
    {
        validStart ??= x => 0;
        
        var chaser = new Chaser(this,destination, validStart, identifier);
        _chasers.Add(chaser);
        chaser.Start();
        return chaser;
    }
    public async Task Chase(HttpContext context, Func<byte, int?> validStart, string? identifier,
        CancellationToken token = default)
    {
        validStart ??= x => 0;

        var chaser = new HttpMjpegStreamChaser(this, context, validStart, identifier, token:token);
        _chasers.Add(chaser);
        await chaser.Write();
    }
    public async Task Chase(WebSocket destination, Func<byte, int?> validStart = null, string identifier = null)
    {
        validStart ??= x => 0;

        var chaser = new WebChaser(this, destination, validStart, identifier);
        _chasers.Add(chaser);
        await chaser.Write();
    }

    private async Task OnReadAsync()
    {
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                
                var left = _buffer.Length - _readOffset;
                var count = Math.Min(left, CHUNK);

                var bufferChunk = _buffer.Slice(_readOffset, count);
                var read = await _source.ReadAsync(bufferChunk, 10000);

                if (read == 0)
                {
                    await Close();
                    return;
                }

                var offset = _readOffset + read;
                if (offset == _buffer.Length)
                {
                    _readOffset = 0;
                    Interlocked.Add(ref _totalBuffersRead, (ulong)_buffer.Length);
                }
                else
                {
                    _readOffset = offset;
                }
                
            }
        }
        catch (Exception ex)
        {
            await Close();
        }
    }

    private async Task Close()
    {
        _stopped = true;
        _source?.Dispose();
        await Task.WhenAll(_chasers.Select(x => x.Close()).ToArray());
       
        _buffer = null;
        ArrayPool<byte>.Shared.Return(_sharedBuffer);
        Stopped?.Invoke(this, EventArgs.Empty);
    }

    internal void Stop()
    {
        _cts?.Cancel();
    }
}