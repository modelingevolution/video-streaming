using CliWrap;

namespace ModelingEvolution.VideoStreaming;

#pragma warning disable CS4014
static class CommandExtension
{

    public static Command WithArgumentsIf(this Command cmd, bool condition, string command) => condition ? cmd.WithArguments(command) : cmd;
}