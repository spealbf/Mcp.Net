namespace Mcp.Net.Server.ServerBuilder.Helpers;

/// <summary>
/// Helper class to run actions during application startup.
/// </summary>
internal class ActionStartupFilter : IStartupFilter
{
    private readonly Action _action;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionStartupFilter"/> class.
    /// </summary>
    /// <param name="action">The action to execute during startup</param>
    public ActionStartupFilter(Action action)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    /// <summary>
    /// Configures the application by running the specified action before the next middleware.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline</param>
    /// <returns>A function that configures the application</returns>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return builder =>
        {
            _action();
            next(builder);
        };
    }
}