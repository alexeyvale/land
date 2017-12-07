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
using System.Windows.Forms;

namespace PackageTestGUI
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderDialog = new FolderBrowserDialog();

            if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var folderPath = folderDialog.SelectedPath;
                var files = Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories);

                var parser = LandParserGenerator.BuilderLL.BuildSharp();

                var errorCounter = 0;
                var errorFiles = new List<string>();

                foreach(var filePath in files)
                {
                    try
                    {
                        var errorMessage = String.Empty;

                        parser.Parse(File.ReadAllText(filePath), out errorMessage);

                        FilesListText.Text += $"{filePath} - {parser.ErrorRecoveriesCounter} - {errorMessage} {Environment.NewLine}";

                        if (!String.IsNullOrEmpty(errorMessage) || parser.ErrorRecoveriesCounter > 0)
                        {
                            ++errorCounter;
                            errorFiles.Add(filePath);
                        }
                    }
                    catch(Exception ex)
                    {
                        ++errorCounter;
                        errorFiles.Add(filePath);
                        FilesListText.Text += $"{filePath} - {parser.ErrorRecoveriesCounter} -  {ex.ToString()}";
                    }
                }

                FilesListText.Text += $"Разобрано: {files.Length}; С ошибками: {errorCounter} {Environment.NewLine}";

                foreach(var filePath in errorFiles)
                    FilesListText.Text += $"{filePath}{Environment.NewLine}";
            }
        }
    }
}
