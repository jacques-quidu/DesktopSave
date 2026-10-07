using DesktopSave.Models;
using DesktopSave.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DesktopSave
{
	/// <summary>
	/// Fenêtre principale : sauvegarde et restauration des icônes du bureau par résolution.
	/// </summary>
	public sealed partial class MainWindow : Window
	{
		private readonly ObservableCollection<DesktopIcon> _icons = new();

		public MainWindow()
		{
			InitializeComponent();
			IconsListView.ItemsSource = _icons;
			RefreshIcons();
		}

		private void OnRefreshClick(object sender, RoutedEventArgs e) => RefreshIcons();

		private void OnSaveClick(object sender, RoutedEventArgs e)
		{
			try
			{
				string resolution = DesktopLayoutStore.GetCurrentResolution();
				List<DesktopIcon> icons = DesktopIconService.ReadIcons();
				string path = DesktopLayoutStore.Save(icons, resolution);

				UpdateIcons(icons, resolution);
				ShowStatus(
					InfoBarSeverity.Success,
					"Sauvegarde effectuée",
					$"{icons.Count} icône(s) enregistrée(s) dans « {path} ».");
			}
			catch (Exception ex)
			{
				ShowStatus(InfoBarSeverity.Error, "Échec de la sauvegarde", ex.Message);
			}
		}

		private void OnRestoreClick(object sender, RoutedEventArgs e)
		{
			try
			{
				string resolution = DesktopLayoutStore.GetCurrentResolution();
				DesktopLayout? layout = DesktopLayoutStore.Load(resolution);
				if (layout is null || layout.Icons.Count == 0)
				{
					ShowStatus(
						InfoBarSeverity.Warning,
						"Aucune sauvegarde",
						$"Aucune disposition enregistrée pour la résolution {resolution}.");
					return;
				}

				int applied = DesktopIconService.ApplyIcons(layout.Icons);
				RefreshIcons();
				ShowStatus(
					InfoBarSeverity.Success,
					"Restauration effectuée",
					$"{applied} icône(s) repositionnée(s) sur {layout.Icons.Count} enregistrée(s).");
			}
			catch (Exception ex)
			{
				ShowStatus(InfoBarSeverity.Error, "Échec de la restauration", ex.Message);
			}
		}

		private async void OnOpenFolderClick(object sender, RoutedEventArgs e)
		{
			string folder = string.Empty;
			try
			{
				folder = DesktopLayoutStore.GetStorageFolder();
				var storageFolder = await Windows.Storage.StorageFolder.GetFolderFromPathAsync(folder);
				await Windows.System.Launcher.LaunchFolderAsync(storageFolder);
			}
			catch (Exception ex)
			{
				ShowStatus(InfoBarSeverity.Error, "Ouverture impossible", $"{ex.Message} ({folder})");
			}
		}

		private void RefreshIcons()
		{
			try
			{
				string resolution = DesktopLayoutStore.GetCurrentResolution();
				UpdateIcons(DesktopIconService.ReadIcons(), resolution);
			}
			catch (Exception ex)
			{
				ShowStatus(InfoBarSeverity.Error, "Lecture du bureau impossible", ex.Message);
			}
		}

		private void UpdateIcons(IReadOnlyList<DesktopIcon> icons, string resolution)
		{
			_icons.Clear();
			foreach (DesktopIcon icon in icons)
			{
				_icons.Add(icon);
			}

			ResolutionText.Text = $"Résolution actuelle : {resolution} — {icons.Count} icône(s) détectée(s).";

			DesktopLayout? saved = DesktopLayoutStore.Load(resolution);
			SavedInfoText.Text = saved is null
				? "Aucune sauvegarde pour cette résolution."
				: $"Dernière sauvegarde : {saved.SavedAt.LocalDateTime:g} ({saved.Icons.Count} icône(s)).";

			RestoreButton.IsEnabled = saved is not null;
		}

		private void ShowStatus(InfoBarSeverity severity, string title, string message)
		{
			StatusBar.Severity = severity;
			StatusBar.Title = title;
			StatusBar.Message = message;
			StatusBar.IsOpen = true;
		}
	}
}
