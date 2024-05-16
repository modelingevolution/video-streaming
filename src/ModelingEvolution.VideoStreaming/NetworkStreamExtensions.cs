using System.Net.Sockets;

namespace ModelingEvolution.VideoStreaming;

public static class NetworkStreamExtensions
{
    public static NonBlockingNetworkStream AsNonBlocking(this NetworkStream stream)
    {
        return new NonBlockingNetworkStream(stream);
    }
   
}