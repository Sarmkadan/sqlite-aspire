using Aspire.Hosting.ApplicationModel;

namespace SqliteAspire;

/// <summary>
/// A SQLite database modelled as an Aspire resource. Exposes a connection string to dependents
/// and carries the <see cref="SqliteOptions"/> that hosting extensions mutate.
/// </summary>
/// <param name="name">Resource name as it appears in the Aspire dashboard and in <c>ConnectionStrings:{name}</c>.</param>
/// <param name="options">Effective options for this database.</param>
public sealed class SqliteResource(string name, SqliteOptions options) : Resource(name), IResourceWithConnectionString
{
    /// <summary>Effective options. Mutated by <c>With*</c> extensions before the app model is frozen.</summary>
    public SqliteOptions Options { get; } = options;

    /// <summary>Path to the SQLite database file, taken from <see cref="SqliteOptions.DatabasePath"/>.</summary>
    public string DatabasePath => Options.DatabasePath;

    /// <summary>
    /// Connection string expression injected into dependent projects as <c>ConnectionStrings:{Name}</c>.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"{Options.ToConnectionString()}");
}
