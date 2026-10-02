namespace tinycert;

public interface IOptionsStore
{
    /// <summary>
    /// Loads persisted TinyCert application options.
    /// </summary>
    /// <returns>
    /// A <see cref="TinyCertOptions"/> instance containing the current saved configuration.
    /// </returns>
    TinyCertOptions Load();

    /// <summary>
    /// Persists TinyCert application options.
    /// </summary>
    /// <param name="options"> The <see cref="TinyCertOptions"/> values to save. </param>
    void Save(TinyCertOptions options);
}