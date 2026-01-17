namespace Kode.Agent.Sdk.Core.Checkpoints;

/// <summary>
/// Interface for checkpoint persistence.
/// </summary>
public interface ICheckpointer
{
    /// <summary>
    /// Save a checkpoint.
    /// </summary>
    Task<string> SaveAsync(Checkpoint checkpoint, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Load a checkpoint by ID.
    /// </summary>
    Task<Checkpoint?> LoadAsync(string checkpointId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// List checkpoints for an agent.
    /// </summary>
    Task<IReadOnlyList<CheckpointListItem>> ListAsync(
        string agentId, 
        CheckpointListOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a checkpoint.
    /// </summary>
    Task DeleteAsync(string checkpointId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Fork a checkpoint to a new agent.
    /// </summary>
    Task<string> ForkAsync(string checkpointId, string newAgentId, CancellationToken cancellationToken = default);
}
