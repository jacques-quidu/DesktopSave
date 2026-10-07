using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using DesktopSave.Models;

namespace DesktopSave.Services
{
    /// <summary>
    /// Persiste les dispositions du bureau dans des fichiers JSON, un par résolution d'écran.
    /// </summary>
    internal static class DesktopLayoutStore
    {
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, System.Text.StringBuilder? packageFullName);

        private const int APPMODEL_ERROR_NO_PACKAGE = 15700;

        private static readonly Lazy<bool> IsPackaged = new(() =>
        {
            int length = 0;
            return GetCurrentPackageFullName(ref length, null) != APPMODEL_ERROR_NO_PACKAGE;
        });

        /// <summary>
        /// Résolution physique de l'écran principal, ex. "1920x1080".
        /// </summary>
        public static string GetCurrentResolution()
            => $"{GetSystemMetrics(SM_CXSCREEN)}x{GetSystemMetrics(SM_CYSCREEN)}";

        /// <summary>
        /// Dossier contenant les sauvegardes. En mode packagé (MSIX), %LOCALAPPDATA% est
        /// virtualisé et le chemin n'est pas navigable depuis l'Explorateur : on utilise
        /// donc le dossier local réel du package.
        /// </summary>
        public static string GetStorageFolder()
        {
            string folder = IsPackaged.Value
                ? Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "DesktopSave")
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DesktopSave");

            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// Chemin du fichier de sauvegarde associé à une résolution.
        /// </summary>
        public static string GetLayoutFilePath(string resolution)
            => Path.Combine(GetStorageFolder(), $"layout_{resolution}.json");

        public static bool HasLayout(string resolution)
            => File.Exists(GetLayoutFilePath(resolution));

        public static string Save(IReadOnlyList<DesktopIcon> icons, string resolution)
        {
            var layout = new DesktopLayout
            {
                Resolution = resolution,
                SavedAt = DateTimeOffset.Now,
                Icons = new List<DesktopIcon>(icons),
            };

            string path = GetLayoutFilePath(resolution);
            string json = JsonSerializer.Serialize(layout, DesktopLayoutJsonContext.Default.DesktopLayout);
            File.WriteAllText(path, json);
            return path;
        }

        public static DesktopLayout? Load(string resolution)
        {
            string path = GetLayoutFilePath(resolution);
            if (!File.Exists(path))
            {
                return null;
            }

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize(json, DesktopLayoutJsonContext.Default.DesktopLayout);
        }
    }
}
