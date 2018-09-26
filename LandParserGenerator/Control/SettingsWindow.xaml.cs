using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Reflection;
using System.IO;

using Microsoft.Win32;

using Land.Core.Parsing.Preprocessing;

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
			GrammarsGrid.ItemsSource = SettingsObject.Parsers;
		}

		private void DialogResult_Ok_Click(object sender, RoutedEventArgs e)
		{
			SettingsObject.Parsers = new ObservableCollection<ParserSettingsBlock>(SettingsObject.Parsers
				.GroupBy(gr => new { GrammmarPath = gr.GrammarPath.Trim(), PreprocessorPath = gr.PreprocessorPath.Trim() }).Select(g => new ParserSettingsBlock()
				{
					GrammarPath = g.Key.GrammmarPath,
					Extensions = g.SelectMany(el=>el.Extensions).Distinct().ToList(),
                    PreprocessorPath = g.Key.PreprocessorPath,
                    PreprocessorSettings = g.Select(el=>el.PreprocessorSettings).FirstOrDefault()
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
			SettingsObject.Parsers.Add(new ParserSettingsBlock());
		}

		private void GrammarsGrid_Delete_Click(object sender, RoutedEventArgs e)
		{
			if(GrammarsGrid.SelectedItem != null)
			{
				SettingsObject.Parsers.Remove((ParserSettingsBlock)GrammarsGrid.SelectedItem);
			}
		}

		private void GrammarsGrid_SelectGrammarFile_Click(object sender, RoutedEventArgs e)
		{
			for (var vis = sender as Visual; vis != null; vis = VisualTreeHelper.GetParent(vis) as Visual)
			{
				if (vis is DataGridRow)
				{
					GrammarsGrid.CommitEdit();

					var openFileDialog = new OpenFileDialog()
					{
						AddExtension = true,
						Filter = "Файлы LAND (*.land)|*.land|Все файлы (*.*)|*.*",
						Title = "Выберите файл грамматики"
					};

					if (openFileDialog.ShowDialog() == true)
					{
						var item = (ParserSettingsBlock)((DataGridRow)vis).Item;
						item.GrammarPath = openFileDialog.FileName;

						GrammarsGrid.Items.Refresh();
					}

					break;
				}
			}

			
		}

		private void GrammarsGrid_SelectPreprocessorFile_Click(object sender, RoutedEventArgs e)
		{
			for (var vis = sender as Visual; vis != null; vis = VisualTreeHelper.GetParent(vis) as Visual)
			{
				if (vis is DataGridRow)
				{
					GrammarsGrid.CommitEdit();

					var openFileDialog = new OpenFileDialog()
					{
						AddExtension = true,
						Filter = "Динамическая библиотека (*.dll)|*.dll",
						Title = "Выберите библиотеку препроцессора"
					};

					if (openFileDialog.ShowDialog() == true)
					{
						var item = (ParserSettingsBlock)((DataGridRow)vis).Item;
						item.PreprocessorPath = openFileDialog.FileName;

						GrammarsGrid.Items.Refresh();
					}

					break;
				}
			}
		}

		private void GrammarsGrid_OpenPreprocessorSettings_Click(object sender, RoutedEventArgs e)
		{
			for (var vis = sender as Visual; vis != null; vis = VisualTreeHelper.GetParent(vis) as Visual)
			{
				if (vis is DataGridRow)
				{
					GrammarsGrid.CommitEdit();

					var item = (ParserSettingsBlock)((DataGridRow)vis).Item;

					if (!String.IsNullOrEmpty(item.PreprocessorPath))
					{
						var propertiesToSet = Assembly.LoadFile(item.PreprocessorPath)
							.GetTypes().FirstOrDefault(t => t.BaseType.Equals(typeof(PreprocessorSettings)))
                            ?.GetProperties().Where(prop => Attribute.IsDefined(prop, typeof(PropertyToSet))).ToList();

                        if (propertiesToSet != null && propertiesToSet.Count > 0)
						{

						}
					}

					break;
				}
			}
		}
	}
}
