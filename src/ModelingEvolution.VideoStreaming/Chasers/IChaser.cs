namespace ModelingEvolution.VideoStreaming.Chasers;

public interface IChaser
{
    int PendingBytes { get; }
    void Start();
    string Identifier { get; }
    ulong WrittenBytes { get; }
    string Started { get; }
    Task Close();
}