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
		private LandParserGenerator.Parsing.BaseParser Parser { get; set; }

		public MainWindow()
		{
			InitializeComponent();

			using (var consoleWriter = new ConsoleWriter())
			{
				consoleWriter.WriteEvent += consoleWriter_WriteEvent;
				consoleWriter.WriteLineEvent += consoleWriter_WriteLineEvent;
				Console.SetOut(consoleWriter);
			}

			GrammarEditor.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.Xshd.HighlightingLoader.Load(
				new System.Xml.XmlTextReader(new StreamReader($"../../land.xshd", Encoding.Default)), 
				ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance);
		}

		private void consoleWriter_WriteLineEvent(object sender, ConsoleWriterEventArgs e)
		{
			ConsoleOutputText.Text += e.Value + System.Environment.NewLine;
		}

		private void consoleWriter_WriteEvent(object sender, ConsoleWriterEventArgs e)
		{
			ConsoleOutputText.Text += e.Value;
		}

		private void BuildParserButton_Click(object sender, RoutedEventArgs e)
		{
			Parser = null;
			List<string> errors = new List<string>();

			if (ParsingLL.IsChecked == true)
			{
				Parser = LandParserGenerator.BuilderLL.BuildParser(GrammarEditor.Text, errors);
			}
			else if (ParsingLR.IsChecked == true)
			{

			}

			ParserBuidingOutput.ItemsSource = errors;

			if (Parser == null || errors.Count > 0)
			{
				ParserStatusLabel.Content = "Обнаружены ошибки в грамматике языка";
				ParserStatus.Background = Brushes.Red;
			}
			else
			{
				ParserStatusLabel.Content = "Парсер успешно сгенерирован";
				ParserStatus.Background = Brushes.LightGreen;
			}
		}

		private void ParseButton_Click(object sender, RoutedEventArgs e)
		{
            if (Parser != null)
            {
                var errorMessage = String.Empty;
                var root = Parser.Parse(FileEditor.Text, out errorMessage);

                ProgramStatusLabel.Content = errorMessage;
                ProgramStatus.Background = String.IsNullOrEmpty(errorMessage) ? Brushes.LightGreen : Brushes.Red;

                if (root != null)
                {
                    TreeRoot = root;
                    ParseTreeView.ItemsSource = new[] { (TreeViewAdapter)root };
                }
                FileParsingOutput.ItemsSource = Parser.Log;
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
				FileEditor.Select(start, end - start + 1);
				FileEditor.ScrollToLine(FileEditor.Document.GetLocation(start).Line);
			}
			else
			{
				FileEditor.Select(0, 0);
			}
		}

		private void OpenFileButton_Click(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new OpenFileDialog();
			if (openFileDialog.ShowDialog() == true)
			{
				FileEditor.Text = File.ReadAllText(openFileDialog.FileName);
				TestFileName.Content = openFileDialog.FileName;
			}
		}

		private void LoadGrammarButton_Click(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new OpenFileDialog();
			if (openFileDialog.ShowDialog() == true)
			{
				GrammarFileName.Content = openFileDialog.FileName;
				GrammarEditor.Text = File.ReadAllText(openFileDialog.FileName);
				SaveGrammarButton.IsEnabled = false;
			}
		}

		private void SaveGrammarButton_Click(object sender, RoutedEventArgs e)
		{
			if(GrammarFileName.Content != null)
			{
				File.WriteAllText(GrammarFileName.Content.ToString(), GrammarEditor.Text);
				SaveGrammarButton.IsEnabled = false;
			}
			else
			{
				var saveFileDialog = new SaveFileDialog();
				if (saveFileDialog.ShowDialog() == true)
				{
					GrammarFileName.Content = saveFileDialog.FileName;
					File.WriteAllText(saveFileDialog.FileName, GrammarEditor.Text);
					SaveGrammarButton.IsEnabled = false;
				}
			}
		}

		private void NewGrammarButton_Click(object sender, RoutedEventArgs e)
		{
			GrammarFileName.Content = null;
			GrammarEditor.Text = String.Empty;
		}

		private void ClearFileButton_Click(object sender, RoutedEventArgs e)
		{
			TestFileName.Content = null;
			FileEditor.Text = String.Empty;
		}

		private void GrammarEditor_TextChanged(object sender, EventArgs e)
		{
			ParserStatus.Background = Brushes.Yellow;
			ParserStatusLabel.Content = "Текст грамматики изменился со времени последней генерации парсера";
			SaveGrammarButton.IsEnabled = true;
		}
	}
}
