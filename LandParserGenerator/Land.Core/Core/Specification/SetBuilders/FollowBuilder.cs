using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Land.Core.Specification
{
	public class FollowBuilder
	{
		private Dictionary<string, HashSet<string>> FollowCache { get; set; }

		public FollowBuilder(Grammar g, FirstBuilder firstBuilder = null)
		{
			BuildFollow(g, firstBuilder ?? new FirstBuilder(g, false));
		}

		/// <summary>
		/// Построение FOLLOW
		/// </summary>
		private void BuildFollow(Grammar g, FirstBuilder firstBuilder)
		{
			FollowCache = new Dictionary<string, HashSet<string>>();

			foreach (var nt in g.Rules)
			{
				FollowCache[nt.Key] = new HashSet<string>();
			}

			FollowCache[g.StartSymbol].Add(Grammar.EOF_TOKEN_NAME);

			var changed = true;

			while (changed)
			{
				changed = false;

				/// Проходим по всем продукциям и по всем элементам веток
				foreach (var nt in g.Rules)
					foreach (var alt in nt.Value)
					{
						for (var i = 0; i < alt.Count; ++i)
						{
							var elem = alt[i];

							/// Если встретили в ветке нетерминал
							if (g.Rules.ContainsKey(elem))
							{
								var oldCount = FollowCache[elem].Count;

								/// Добавляем в его FOLLOW всё, что может идти после него
								FollowCache[elem].UnionWith(firstBuilder.First(alt.Subsequence(i + 1)));

								/// Если в FIRST(подпоследовательность) была пустая строка
								if (FollowCache[elem].Contains(null))
								{
									/// Исключаем пустую строку из FOLLOW
									FollowCache[elem].Remove(null);
									/// Объединяем FOLLOW текущего нетерминала
									/// с FOLLOW определяемого данной веткой
									FollowCache[elem].UnionWith(FollowCache[nt.Key]);
								}

								if (!changed)
								{
									changed = oldCount != FollowCache[elem].Count;
								}
							}
						}
					}
			}
		}

		public HashSet<string> Follow(string nonterminal)
		{
			return FollowCache[nonterminal];
		}
	}
}
