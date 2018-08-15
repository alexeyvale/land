using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.IO;

using Microsoft.Win32;

namespace Land.Control
{
	/// <summary>
	/// Логика взаимодействия для Settings.xaml
	/// </summary>
	public partial class SettingsWindow : Window
	{
		public LandExplorerSettings SettingsObject { get; private set; }

		public SettingsWindow(LandExplorerSettings settingsObject)
		{
			SettingsObject = settingsObject;
			InitializeComponent();
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			GrammarsGrid.ItemsSource = SettingsObject.Grammars;
		}

		private void DialogResult_Ok_Click(object sender, RoutedEventArgs e)
		{
			SettingsObject.Grammars = new ObservableCollection<ExtensionGrammarPair>(SettingsObject.Grammars
				.GroupBy(gr => Path.GetFullPath(gr.GrammarPath.Trim())).Select(g => new ExtensionGrammarPair()
				{
					GrammarPath = g.Key,
					Extensions = g.SelectMany(el=>el.Extensions).Distinct().ToList()
				})
			);

			this.DialogResult = true;
		}

		private void DialogResult_Cancel_Click(object sender, RoutedEventArgs e)
		{
			this.DialogResult = false;
		}

		private void GrammarsGrid_Add_Click(object sender, RoutedEventArgs e)
		{
			SettingsObject.Grammars.Add(new ExtensionGrammarPair());
		}

		private void GrammarsGrid_Delete_Click(object sender, RoutedEventArgs e)
		{
			if(GrammarsGrid.SelectedItem != null)
			{
				SettingsObject.Grammars.Remove((ExtensionGrammarPair)GrammarsGrid.SelectedItem);
			}
		}

		private void GrammarsGrid_SelectFile_Click(object sender, RoutedEventArgs e)
		{
			for (var vis = sender as Visual; vis != null; vis = VisualTreeHelper.GetParent(vis) as Visual)
			{
				if (vis is DataGridRow)
				{
					GrammarsGrid.CommitEdit();

					var openFileDialog = new OpenFileDialog()
					{
						AddExtension = true,
						DefaultExt = "landmark",
						Filter = "Файлы LAND (*.land)|*.land|Все файлы (*.*)|*.*",
						Title = "Выберите файл грамматики"
					};

					if (openFileDialog.ShowDialog() == true)
					{
						var item = (ExtensionGrammarPair)((DataGridRow)vis).Item;
						item.GrammarPath = openFileDialog.FileName;

						GrammarsGrid.Items.Refresh();
					}

					break;
				}
			}

			
		}
	}
}
