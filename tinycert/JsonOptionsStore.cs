using System.Text.Json;

namespace tinycert;

public class JsonOptionsStore : IOptionsStore
{
    private const string OptionsFileName = "appsettings.json";
    
    public TinyCertOptions Load()
    {
        if(!File.Exists(OptionsFileName))
        {
            var defaultOptions = new TinyCertOptions();
            Save(defaultOptions);
            return defaultOptions;
        }
        
        using FileStream openStream = File.OpenRead(OptionsFileName);
        var options = JsonSerializer.Deserialize<TinyCertOptions>(openStream);
        
        return options ?? throw new InvalidOperationException($"Failed to deserialize options from {OptionsFileName}");
    }

    public void Save(TinyCertOptions options)
    {
        string jsonString = JsonSerializer.Serialize(options);
        File.WriteAllText(OptionsFileName, jsonString);
    }
}