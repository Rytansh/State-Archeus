using Archeus.Core.Debugging;
using UnityEngine;
public sealed class ValidationContext
{
    public readonly Object asset;
    public bool HasErrors => errorCount > 0;
    public bool HasWarnings => warningCount > 0;

    private int errorCount;
    private int warningCount;

    public ValidationContext(Object asset)
    {
        this.asset = asset;
    }

    public void Error(string message)
    {
        errorCount++;
        Logging.Warn(LogCategory.System,$"ERROR: [{asset.name}] {message}");
    }

    public void Warning(string message)
    {
        warningCount++;
        Logging.Warn(LogCategory.System,$"WARNING: [{asset.name}] {message}");
    }
}

