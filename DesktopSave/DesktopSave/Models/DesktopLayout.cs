using System;
using System.Collections.Generic;

namespace DesktopSave.Models
{
    /// <summary>
    /// Position d'une icône du bureau, identifiée par son libellé.
    /// </summary>
    public sealed class DesktopIcon
    {
        public string Name { get; set; } = string.Empty;

        public int X { get; set; }

        public int Y { get; set; }
    }

    /// <summary>
    /// Disposition complète du bureau pour une résolution d'écran donnée.
    /// </summary>
    public sealed class DesktopLayout
    {
        public string Resolution { get; set; } = string.Empty;

        public DateTimeOffset SavedAt { get; set; }

        public List<DesktopIcon> Icons { get; set; } = new();
    }
}
