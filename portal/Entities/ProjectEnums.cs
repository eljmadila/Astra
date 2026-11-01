namespace AspnetCoreMvcFull.Entities;

public enum ProjectStatus
{
    NotStarted = 0,
    InProgress = 1,
    OnHold = 2,
    Completed = 3,
    Cancelled = 4
}

public enum ProjectPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

public enum ProgressMode
{
    Manual = 0,
    Derived = 1
}
