namespace OpenTheWindows.App.ViewModels;

/// <summary>The stage of the apply flow, driving which section the dialog shows.</summary>
internal enum ApplyFlowStage
{
    /// <summary>Showing the what-if plan; the user can apply or cancel.</summary>
    Preview,

    /// <summary>The apply is running on a background thread.</summary>
    Applying,

    /// <summary>The run finished successfully (or was a no-op).</summary>
    Completed,

    /// <summary>The run failed and was rolled back.</summary>
    Failed,
}
