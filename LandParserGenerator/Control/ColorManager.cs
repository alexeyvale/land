using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Controls;
using System.IO;
using System.Windows.Media;

namespace Land.Control
{
	public class ColorManager
	{
		public enum Mode { OneColor, MultiColor }

		public static ColorManager Instance { get; private set; } = new ColorManager();

		/// <summary>
		/// Список цветов, изначально есть предопределённый набор
		/// </summary>
		private List<Color> ColorsList { get; set; } = new List<Color> {
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
		private Random Generator { get; set; } = new Random();

		/// <summary>
		/// Какие цвета из массива использованы
		/// </summary>
		private HashSet<int> ColorsUsed { get; set; } = new HashSet<int>();

		/// <summary>
		/// Метод для установки пользовательского набора цветов
		/// </summary>
		public void SetCustomColors(List<Color> CustomColors = null)
		{
			if (CustomColors?.Count > 0)
				ColorsList = CustomColors;
		}

		/// <summary>
		/// Текущий режим работы менеджера цветов
		/// </summary>
		private Mode CurrentMode { get; set; }

		public void SwitchMode(Mode mode)
		{
			Reset();
			CurrentMode = mode;
		}

		/// <summary>
		/// Получение следующего цвета
		/// </summary>
		public Color GetColor()
		{
			switch(CurrentMode)
			{
				case Mode.OneColor:
					return ColorsList[0];

				case Mode.MultiColor:
				default:
					for (var i = 0; i < ColorsList.Count; ++i)
						if (!ColorsUsed.Contains(i))
						{
							ColorsUsed.Add(i);
							return ColorsList[i];
						}

					/// Если все имеющиеся цвета использованы, добавляем новый
					ColorsList.Add(Color.FromArgb(45, (byte)Generator.Next(100, 206),
						(byte)Generator.Next(100, 206), (byte)Generator.Next(100, 206)));

					ColorsUsed.Add(ColorsList.Count - 1);
					return ColorsList[ColorsList.Count - 1];
			}
		}

		/// <summary>
		/// Считаем переданный цвет неиспользуемым
		/// </summary>
		public void FreeColor(Color color)
		{
			ColorsUsed.Remove(ColorsList.IndexOf(color));
		}

		/// <summary>
		/// Сброс, все цвета считаем неиспользованными
		/// </summary>
		public void Reset()
		{
			ColorsUsed.Clear();
		}
	}
}
