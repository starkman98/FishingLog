using SQLite;

namespace FishingLog.Mobile.Data;

/// <summary>
/// Abstraction for the local SQLite database.
/// Manages the connection and table initialization.
/// </summary>
public interface ILocalDatabase
{
    /// <summary>
    /// Gets the underlying SQLite async connection.
    /// Repositories use this to run queries.
    /// </summary>
    SQLiteAsyncConnection Connection { get; }

    /// <summary>
    /// Creates all tables if they do not already exist.
    /// Must be called once at app startup before any data access.
    /// </summary>
    Task InitializeAsync();
}
