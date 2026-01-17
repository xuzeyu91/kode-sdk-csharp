using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Files;

namespace Kode.Agent.Tools.Builtin;

internal static class ToolContextFilePool
{
    public static FilePool? TryGetFilePool(ToolContext context)
    {
        return context.Services?.GetService(typeof(FilePool)) as FilePool;
    }
}

