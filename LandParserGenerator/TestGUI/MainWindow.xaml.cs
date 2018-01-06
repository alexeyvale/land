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
		private string LAST_GRAMMARS_FILE = "./last_grammars.land.ide";

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

			GrammarEditor.TextArea.TextView.BackgroundRenderers.Add(new CurrentLineHighlighter(GrammarEditor.TextArea));
			FileEditor.TextArea.TextView.BackgroundRenderers.Add(new CurrentLineHighlighter(FileEditor.TextArea));

			if(File.Exists(LAST_GRAMMARS_FILE))
			{
				var files = File.ReadAllLines(LAST_GRAMMARS_FILE);
				foreach(var filepath in files.Reverse())
				{
					if(!String.IsNullOrEmpty(filepath))
					{
						LastGrammarFiles.Items.Add(filepath);
					}
				}
			}
		}

		private void consoleWriter_WriteLineEvent(object sender, ConsoleWriterEventArgs e)
		{
			ParserBuidingLog.Items.Add(e.Value);
		}

		private void consoleWriter_WriteEvent(object sender, ConsoleWriterEventArgs e)
		{
			ParserBuidingLog.Items.Add(e.Value);
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			var listContent = new List<string>();

			foreach (var item in LastGrammarFiles.Items)
				listContent.Add(item.ToString());

			File.WriteAllLines(LAST_GRAMMARS_FILE, listContent);
		}

		#region Генерация парсера

		private void BuildParserButton_Click(object sender, RoutedEventArgs e)
		{
			Parser = null;
			var errors = new List<LandParserGenerator.ParsingMessage>();

			if (ParsingLL.IsChecked == true)
			{
				Parser = LandParserGenerator.BuilderLL.BuildParser(GrammarEditor.Text, errors);
			}
			else if (ParsingLR.IsChecked == true)
			{

			}

			//ParserBuidingLog
			ParserBuidingErrors.ItemsSource = errors;

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

		private void LoadGrammarButton_Click(object sender, RoutedEventArgs e)
		{
			var openFileDialog = new OpenFileDialog();
			if (openFileDialog.ShowDialog() == true)
			{
				OpenGrammar(openFileDialog.FileName);
				SetAsCurrentGrammar(openFileDialog.FileName);
			}
		}

		private void SetAsCurrentGrammar(string filename)
		{
			LastGrammarFiles.SelectionChanged -= LastGrammarFiles_SelectionChanged;

			if (LastGrammarFiles.Items.Contains(filename))
				LastGrammarFiles.Items.Remove(filename);

			LastGrammarFiles.Items.Insert(0, filename);
			LastGrammarFiles.SelectedIndex = 0;

			LastGrammarFiles.SelectionChanged += LastGrammarFiles_SelectionChanged;
		}

		private void OpenGrammar(string filename)
		{
			GrammarEditor.Text = File.ReadAllText(filename);
			SaveGrammarButton.IsEnabled = false;
		}

		private void SaveGrammarButton_Click(object sender, RoutedEventArgs e)
		{
			if (LastGrammarFiles.SelectedIndex != -1)
			{
				File.WriteAllText(LastGrammarFiles.SelectedItem.ToString(), GrammarEditor.Text);
				SaveGrammarButton.IsEnabled = false;
			}
			else
			{
				var saveFileDialog = new SaveFileDialog();
				if (saveFileDialog.ShowDialog() == true)
				{
					File.WriteAllText(saveFileDialog.FileName, GrammarEditor.Text);
					SaveGrammarButton.IsEnabled = false;

					SetAsCurrentGrammar(saveFileDialog.FileName);
				}
			}
		}

		private void NewGrammarButton_Click(object sender, RoutedEventArgs e)
		{
			LastGrammarFiles.SelectedIndex = -1;
			GrammarEditor.Text = String.Empty;
		}

		private void GrammarEditor_TextChanged(object sender, EventArgs e)
		{
			ParserStatus.Background = Brushes.Yellow;
			ParserStatusLabel.Content = "Текст грамматики изменился со времени последней генерации парсера";
			SaveGrammarButton.IsEnabled = true;
		}

		private void GrammarListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var lb = sender as ListBox;

			if (lb.SelectedIndex != -1)
			{
				var msg = (LandParserGenerator.ParsingMessage)lb.SelectedItem;
				if (msg.Location != null)
				{
					var start = GrammarEditor.Document.GetOffset(msg.Location.Line, msg.Location.Column);
					GrammarEditor.Focus();
					GrammarEditor.Select(start, 0);
					GrammarEditor.ScrollToLine(FileEditor.Document.GetLocation(start).Line);
				}
			}
		}

		private void LastGrammarFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if(e.AddedItems.Count > 0)
				OpenGrammar(e.AddedItems[0].ToString());
		}

		#endregion

		#region Парсинг одиночного файла

		private void ParseButton_Click(object sender, RoutedEventArgs e)
		{
            if (Parser != null)
            {
                var root = Parser.Parse(FileEditor.Text);

                ProgramStatusLabel.Content = Parser.Errors.Count == 0 ? "Разбор произведён успешно" : "Ошибки при разборе файла";
                ProgramStatus.Background = Parser.Errors.Count == 0 ? Brushes.LightGreen : Brushes.Red;

                if (root != null)
                {
                    TreeRoot = root;
                    ParseTreeView.ItemsSource = new[] { (TreeViewAdapter)root };
                }

                FileParsingLog.ItemsSource = Parser.Log;
				FileParsingErrors.ItemsSource = Parser.Errors;
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
				OpenFile(openFileDialog.FileName);
			}
		}

		private void OpenFile(string filename)
		{
			FileEditor.Text = File.ReadAllText(filename);
			TestFileName.Content = filename;
		}

		private void ClearFileButton_Click(object sender, RoutedEventArgs e)
		{
			TestFileName.Content = null;
			FileEditor.Text = String.Empty;
		}

		private void TestFileListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var lb = sender as ListBox;

			if (lb.SelectedIndex != -1)
			{
				var msg = (LandParserGenerator.ParsingMessage)lb.SelectedItem;
				if (msg.Location != null)
				{
					var start = FileEditor.Document.GetOffset(msg.Location.Line, msg.Location.Column);
					FileEditor.Focus();
					FileEditor.Select(start, 0);
					FileEditor.ScrollToLine(FileEditor.Document.GetLocation(start).Line);
				}
			}
		}

		#endregion

		#region Парсинг набора файлов

		private void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
		{
			var folderDialog = new System.Windows.Forms.FolderBrowserDialog();

			if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				var folderPath = folderDialog.SelectedPath;
				var patterns = TargetExtentions.Text.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(ext => $"*.{ext.Trim().Trim('.')}");

				List<string> files = new List<string>();
				foreach(var pattern in patterns)
				{
					files.AddRange(Directory.GetFiles(folderPath, pattern, SearchOption.AllDirectories));
				}

				if (Parser != null)
				{
					var errorCounter = 0;
					var errorFiles = new List<string>();

					foreach (var filePath in files)
					{
						try
						{
							Parser.Parse(File.ReadAllText(filePath));

							if (Parser.Errors.Count > 0)
							{
								PackageParsingLog.Items.Add(filePath);
								foreach (var error in Parser.Errors)
									PackageParsingLog.Items.Add($"\t{error}");

								++errorCounter;
								errorFiles.Add(filePath);
							}
						}
						catch (Exception ex)
						{
							PackageParsingLog.Items.Add(filePath);
							foreach (var error in Parser.Errors)
								PackageParsingLog.Items.Add($"\t{error}");
							PackageParsingLog.Items.Add($"\t{ex.ToString()}");

							++errorCounter;
							errorFiles.Add(filePath);
						}
					}

					PackageStatusLabel.Content = $"Разобрано: {files.Count}; С ошибками: {errorCounter} {Environment.NewLine}";
					PackageStatus.Background = errorCounter == 0 ? Brushes.LightGreen : Brushes.Red;
				}
			}
		}

		private void PackageParsingListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var lb = sender as ListBox;

			if (lb.SelectedIndex != -1 && lb.SelectedItem is string)
			{
				/// Открыть файл
				OpenFile(lb.SelectedItem.ToString());
				MainTabs.SelectedIndex = 1;
			}
		}

		#endregion
	}
}
