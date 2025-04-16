namespace Tingle.Dependabot.Tests;

public sealed class DbFixture : IDisposable
{
    private readonly string filename = $"{Directory.GetCurrentDirectory()}/{Guid.NewGuid():n}";

    public DbFixture()
    {
        ConnectionString = $"Data Source={filename}";
    }

    public string ConnectionString { get; }

    #region IDisposable Support
    private bool disposed = false; // To detect redundant calls

    private void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                if (File.Exists(filename)) File.Delete(filename);
            }

            disposed = true;
        }
    }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        Dispose(true);
    }
    #endregion
}
