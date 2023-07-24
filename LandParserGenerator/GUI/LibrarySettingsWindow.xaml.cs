using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows;

using Microsoft.Win32;

namespace Land.GUI
{
	/// <summary>
	/// Логика взаимодействия для LibrarySettingsWindow.xaml
	/// </summary>
	public partial class LibrarySettingsWindow : Window
	{
		public LibrarySettingsWindow()
		{
			InitializeComponent();
		}

		private void Button_Ok_Click(object sender, RoutedEventArgs e)
		{
			if(!String.IsNullOrWhiteSpace(Input_Namespace.Text) 
				&& !String.IsNullOrWhiteSpace(Input_OutputDirectory.Text))
				DialogResult = true;
		}

		private void Button_Cancel_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
		}

		//private void Button_KeysFileSelect_Click(object sender, RoutedEventArgs e)
		//{
		//	var openFileDialog = new OpenFileDialog()
		//	{
		//		AddExtension = true,
		//		Filter = "Файл ключей (*.snk)|*.snk|Все файлы (*.*)|*.*",
		//		Title = "Выберите файл ключей"
		//	};

		//	if (Directory.Exists(Input_OutputDirectory.Text))
		//		openFileDialog.InitialDirectory = Input_OutputDirectory.Text;

		//	if (openFileDialog.ShowDialog() == true)
		//	{
		//		Input_KeysFile.Text = openFileDialog.FileName;
		//	}
		//}

		//private void Input_IsSignedAssembly_Checked(object sender, RoutedEventArgs e)
		//{
		//	if(String.IsNullOrWhiteSpace(Input_KeysFile.Text) 
		//		&& !String.IsNullOrWhiteSpace(Input_OutputDirectory.Text) 
		//		&& !String.IsNullOrWhiteSpace(Input_Namespace.Text))
		//	{
		//		Input_KeysFile.Text = Path.Combine(Input_OutputDirectory.Text, $"{Input_Namespace.Text}.snk");
		//	}
		//}
	}
}
