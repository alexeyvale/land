using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

using Microsoft.Win32;

using Land.Control.Helpers;

namespace Land.Control
{
	public partial class Window_Exception : Window
	{
		public Window_Exception(string exceptionInfo)
		{
			InitializeComponent();
			ExceptionInfoText.Text = exceptionInfo;
		}

		private void DialogResult_Cancel_Click(object sender, RoutedEventArgs e)
		{
			this.DialogResult = true;
		}
	}
}
