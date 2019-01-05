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
	public class MissingListIconColorConverter : IMultiValueConverter
	{
		public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			var label = (Label)values[0];
			var IsFocused = (bool)values[1];
			var IsSelectionActive = (bool)values[2];

			return IsFocused && IsSelectionActive
				? Brushes.WhiteSmoke
				: label.Name == "MissingIcon"
					? Brushes.IndianRed
					: Brushes.DarkBlue;
		}

		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
		{
			return new object[] { };
		}
	}
}
