using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

using Microsoft.Win32;

using Land.Control.Helpers;

namespace Land.Control
{
	public partial class PreprocessorPropertiesWindow : Window
	{
		public List<LandExplorerSettingsWindow.PreprocessorProperty> Properties { get; private set; }

		public PreprocessorPropertiesWindow(List<LandExplorerSettingsWindow.PreprocessorProperty> properties)
		{
			Properties = properties;
			InitializeComponent();
		}

		private void DialogResult_Ok_Click(object sender, RoutedEventArgs e)
		{
			this.DialogResult = true;
		}

		private void DialogResult_Cancel_Click(object sender, RoutedEventArgs e)
		{
			this.DialogResult = false;
		}
	}
}
