namespace ModelingEvolution.VideoStreaming;

public class ChaserWriteFailedException : Exception
{
    public int Write { get; }
    
    public ChaserWriteFailedException(Exception ex, int toWrite, DateTime started) : base($"Could not write {toWrite}B to underlying stream after {DateTime.Now.Subtract(started)}",ex)
    {
        Write = toWrite;
    }
    public ChaserWriteFailedException(string msg) : base(msg){}
}