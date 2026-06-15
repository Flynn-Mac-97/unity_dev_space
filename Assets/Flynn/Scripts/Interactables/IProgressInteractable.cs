/// <summary>
/// Interface for interactables that support progress-based interaction (scanning, etc.).
/// </summary>
public interface IProgressInteractable
{
    bool CanStart();
    void Begin();
    void Tick(float deltaTime);
    void Complete();
    void Cancel();
    float Progress { get; }
}
