using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Land.Core.Markup;

namespace Land.Control
{
	public class MarkupTreeIconColorConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			var label = (Label)values[0];
			var isFocused = (bool)values[1];

			return isFocused
				? Brushes.WhiteSmoke
				: label.Name == "MissingIcon"
					? Brushes.IndianRed
					: Brushes.DimGray;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			return new object[] { };
		}
	}
}
