using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Win32;

using LandParserGenerator.Parsing.Tree;

namespace TestGUI
{
	/// <summary>
	/// Логика взаимодействия для MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private Node TreeRoot { get; set; }

		public MainWindow()
		{
			InitializeComponent();

			using (var consoleWriter = new ConsoleWriter())
			{
				consoleWriter.WriteEvent += consoleWriter_WriteEvent;
				consoleWriter.WriteLineEvent += consoleWriter_WriteLineEvent;
				Console.SetOut(consoleWriter);
			}
		}

		private void consoleWriter_WriteLineEvent(object sender, ConsoleWriterEventArgs e)
		{
			ConsoleOutputText.Text += e.Value + System.Environment.NewLine;
		}

		private void consoleWriter_WriteEvent(object sender, ConsoleWriterEventArgs e)
		{
			ConsoleOutputText.Text += e.Value;
		}

		private void ParseButton_Click(object sender, RoutedEventArgs e)
		{
			if (LanguageSharpRadio.IsChecked == true)
			{
				var parser = LandParserGenerator.Builder.BuildExpressionGrammar();

				var errorMessage = String.Empty;
				var root = parser.Parse(Editor.Text, out errorMessage);

				ProgramStatusLabel.Content = errorMessage;
				ProgramStatus.Background = String.IsNullOrEmpty(errorMessage) ? Brushes.Green : Brushes.Red;

				if (root != null)
				{
					TreeRoot = root;
					ParseTreeView.ItemsSource = new[] { (TreeViewAdapter)root };
				}
				OutputList.ItemsSource = parser.Log;
			}
			else if (LanguageYaccRadio.IsChecked == true)
			{
				var parser = LandParserGenerator.Builder.BuildYacc();

				var errorMessage = String.Empty;
				var root = parser.Parse(Editor.Text, out errorMessage);

				ProgramStatusLabel.Content = errorMessage;
				ProgramStatus.Background = String.IsNullOrEmpty(errorMessage) ? Brushes.Green : Brushes.Red;

				if (root != null)
				{
					TreeRoot = root;
					ParseTreeView.ItemsSource = new[] { (TreeViewAdapter)root };
				}
				OutputList.ItemsSource = parser.Log;
			}
		}

		private void ParseTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			var treeView = (TreeView)sender;
			var node = ((TreeViewAdapter)treeView.SelectedItem).Source;

			if (node.StartOffset.HasValue && node.EndOffset.HasValue)
			{
				var start = node.StartOffset.Value;
				var end = node.EndOffset.Value;
				Editor.Select(start, end - start + 1);
			}
			else
			{
				Editor.Select(0, 0);
			}
		}

		private void OpenFileButton_Click(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new OpenFileDialog();
			if (openFileDialog.ShowDialog() == true)
				Editor.Text = File.ReadAllText(openFileDialog.FileName);
		}
	}
}
