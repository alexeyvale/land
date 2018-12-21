using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

using Land.Core.Markup;

namespace Land.Control
{
	public class BoolToVisibilityConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			var val = (bool?)value ?? false;

			return val ? Visibility.Visible : Visibility.Collapsed;
		}

		public object ConvertBack(object value, Type targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			return null;
		}
	}
}
