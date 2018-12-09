using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace VisualStudioExtension.Highlighting
{
	public static class ColorManager
	{
		/// <summary>
		/// Список цветов, изначально есть предопределённый набор
		/// </summary>
		private static List<Color> ColorsList { get; set; } = new List<Color> {
			Color.FromArgb(30, 100, 200, 100),
			Color.FromArgb(30, Colors.Cyan.R, Colors.Cyan.G, Colors.Cyan.B),
			Color.FromArgb(30, Colors.HotPink.R, Colors.HotPink.G, Colors.HotPink.B),
			Color.FromArgb(30, Colors.Coral.R, Colors.Coral.G, Colors.Coral.B),
			Color.FromArgb(30, Colors.Gold.R, Colors.Gold.G, Colors.Gold.B),
			Color.FromArgb(30, Colors.LightSkyBlue.R, Colors.LightSkyBlue.G, Colors.LightSkyBlue.B),
			Color.FromArgb(30, Colors.Thistle.R, Colors.Thistle.G, Colors.Thistle.B)
		};

		/// <summary>
		/// ГСЧ для генерации цветов, если не хватит предопределённых
		/// </summary>
		private static Random Generator { get; set; } = new Random();

		/// <summary>
		/// Сколько цветов из массива использовано
		/// </summary>
		private static int ColorsUsed { get; set; } = 0;

		/// <summary>
		/// Какой последний цвет был запрошен
		/// </summary>
		public static Color? CurrentColor =>
			ColorsUsed != 0 ? ColorsList[ColorsUsed - 1] : (Color?)null;

		/// <summary>
		/// Получение следующего цвета
		/// </summary>
		public static Color NextColor()
		{
			if (ColorsUsed == ColorsList.Count)
				ColorsList.Add(Color.FromArgb(45, (byte)Generator.Next(100, 206), 
					(byte)Generator.Next(100, 206), (byte)Generator.Next(100, 206)));

			return ColorsList[ColorsUsed++];
		}

		/// <summary>
		/// Сброс, все цвета считаем неиспользованными
		/// </summary>
		public static void Reset()
		{
			ColorsUsed = 0;
		}
	}
}
