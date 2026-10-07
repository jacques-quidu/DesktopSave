using System.Text.Json.Serialization;
using DesktopSave.Models;

namespace DesktopSave.Services
{
    /// <summary>
    /// Contexte de sérialisation source-generated (compatible avec PublishTrimmed).
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(DesktopLayout))]
    internal partial class DesktopLayoutJsonContext : JsonSerializerContext
    {
    }
}
