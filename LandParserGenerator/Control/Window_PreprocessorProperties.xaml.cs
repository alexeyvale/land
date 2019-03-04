using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

using Microsoft.Win32;

using Land.Control.Helpers;

namespace Land.Control
{
	public partial class Window_PreprocessorProperties : Window
	{
		public List<PreprocessorProperty> Properties { get; private set; }

		public Window_PreprocessorProperties(List<PreprocessorProperty> properties)
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
