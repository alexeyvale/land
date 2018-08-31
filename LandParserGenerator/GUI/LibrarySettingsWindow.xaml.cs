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
using System.Windows.Shapes;

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
			if(!String.IsNullOrEmpty(Input_Namespace.Text) 
				&& !String.IsNullOrEmpty(Input_OutputDirectory.Text))
				DialogResult = true;
		}

		private void Button_Cancel_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
		}
	}
}
