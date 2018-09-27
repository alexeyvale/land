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
using Land.Control.Helpers;

namespace Land.Control
{
	/// <summary>
	/// Логика взаимодействия для Settings.xaml
	/// </summary>
	public partial class LandExplorerSettingsWindow : Window
	{
		public LandExplorerSettings SettingsObject { get; private set; }

		public LandExplorerSettingsWindow(LandExplorerSettings settingsObject)
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
			foreach (var item in SettingsObject.Parsers)
				SyncPreprocessorAndProperties(item, out string message);

			SettingsObject.Parsers = new ObservableCollection<ParserSettingsItem>(SettingsObject.Parsers
				.GroupBy(gr => new { GrammmarPath = gr.GrammarPath.Trim(), PreprocessorPath = gr.PreprocessorPath.Trim() }).Select(g => new ParserSettingsItem()
				{
					GrammarPath = g.Key.GrammmarPath,
					Extensions = g.SelectMany(el=>el.Extensions).Distinct().ToList(),
                    PreprocessorPath = g.Key.PreprocessorPath,
                    PreprocessorProperties = g.Select(el=>el.PreprocessorProperties).FirstOrDefault()
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
			SettingsObject.Parsers.Add(new ParserSettingsItem());
		}

		private void GrammarsGrid_Delete_Click(object sender, RoutedEventArgs e)
		{
			if(GrammarsGrid.SelectedItem != null)
			{
				SettingsObject.Parsers.Remove((ParserSettingsItem)GrammarsGrid.SelectedItem);
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
						var item = (ParserSettingsItem)((DataGridRow)vis).Item;
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
						var item = (ParserSettingsItem)((DataGridRow)vis).Item;
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

					/// Находим строчку, в которой нажали на кнопку настроек препроцессора
					var item = (ParserSettingsItem)((DataGridRow)vis).Item;

					if (SyncPreprocessorAndProperties(item, out string message))
					{
						var settingsWindow = new PreprocessorPropertiesWindow(item.PreprocessorProperties.Select(p => p.Clone()).ToList());
						settingsWindow.Owner = this;

						if (settingsWindow.ShowDialog() == true)
							item.PreprocessorProperties = settingsWindow.Properties;
					}
					else
					{
						MessageBox.Show(
							message,
							"Настройки препроцессора",
							MessageBoxButton.OK,
							MessageBoxImage.Information
						);
					}

					break;
				}
			}
		}

		private bool SyncPreprocessorAndProperties(ParserSettingsItem item, out string message)
		{
			message = String.Empty;

			/// Если препроцессор задан
			if (File.Exists(item.PreprocessorPath))
			{
				/// Получаем тип препроцессора из библиотеки
				var propertiesObjectType = Assembly.LoadFile(item.PreprocessorPath)
					.GetTypes().FirstOrDefault(t => t.BaseType.Equals(typeof(PreprocessorSettings)));
				/// Получаем типы свойств препроцессора
				var propertyTypes = propertiesObjectType?.GetProperties()
					.Where(prop => Attribute.IsDefined(prop, typeof(PropertyToSetAttribute))).ToList();

				/// Если свойства есть, их можно настраивать
				if (propertyTypes != null && propertyTypes.Count > 0)
				{
					/// Получаем дефолтный объект свойств препроцессора
					var defaultPropertiesObject = propertiesObjectType.GetConstructor(Type.EmptyTypes).Invoke(null);

					var newPropertiesList = new List<PreprocessorProperty>();

					foreach (var p in propertyTypes)
					{
						var converter = (PropertyConverter)(((ConverterAttribute)p.GetCustomAttribute(typeof(ConverterAttribute))).ConverterType)
							.GetConstructor(Type.EmptyTypes).Invoke(null);

						newPropertiesList.Add(new PreprocessorProperty()
						{
							DisplayedName = ((DisplayedNameAttribute)p.GetCustomAttribute(typeof(DisplayedNameAttribute))).Text,
							ValueString = item.PreprocessorProperties?.FirstOrDefault(prop => prop.PropertyName == p.Name)?.ValueString
								?? converter.ToString(p.GetValue(defaultPropertiesObject)),
							PropertyName = p.Name
						});
					}

					return true;
				}
				else
				{
					message = "Данный препроцессор не имеет настраиваемых параметров";
				}
			}
			else
			{
				message = "Указанный в качестве библиотеки препроцессора файл не существует";
			}

			return false;
		}
	}
}
