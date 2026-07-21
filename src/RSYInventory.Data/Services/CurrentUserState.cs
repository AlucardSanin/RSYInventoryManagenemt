using RSYInventory.Data.Enums;

namespace RSYInventory.Data.Services;

public sealed class CurrentUserState
{
    public bool Resolved { get; private set; }
    public bool IsAuthenticated => Snapshot is not null;
    public CurrentUserSnapshot? Snapshot { get; private set; }

    public void Set(CurrentUserSnapshot snapshot)
    {
        Snapshot = snapshot;
        Resolved = true;
    }

    public void Clear()
    {
        Snapshot = null;
        Resolved = true;
    }
}

public sealed record CurrentUserSnapshot(
    int UserId,
    string UserName,
    string DisplayName,
    HashSet<AppRole> Roles);
