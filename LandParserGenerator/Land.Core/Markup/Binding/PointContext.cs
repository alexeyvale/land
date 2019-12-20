using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using Land.Core;
using Land.Core.Specification;
using Land.Core.Parsing.Tree;
using Land.Markup.CoreExtension;

namespace Land.Markup.Binding
{
	[DataContract]
	public class SiblingsContext
	{
		[DataMember]
		public TextOrHash Before { get; set; }

		[DataMember]
		public TextOrHash After { get; set; }
	}

	[DataContract]
	public class PointContext
	{
		[DataMember]
		public ContextCore Core { get; set; }

		#region Core contexts

		public List<HeaderContextElement> HeaderContext => Core?.HeaderContext;
		public List<AncestorsContextElement> AncestorsContext => Core?.AncestorsContext;
		public InnerContext InnerContext => Core?.InnerContext;
		public FileContext FileContext => Core?.FileContext;
		public string Type => Core?.Type;

		#endregion

		/// <summary>
		/// Номер строки в файле, на которой начинается сущность
		/// </summary>
		[DataMember]
		public int Line { get; set; }

		/// <summary>
		/// Контекст уровня, на котором находится узел, к которому привязана точка разметки
		/// </summary>
		[DataMember]
		public SiblingsContext SiblingsContext { get; set; }

		/// <summary>
		/// Контекст наиболее похожих на помеченный элементов
		/// </summary>
		[DataMember]
		public List<ContextCore> ClosestContext { get; set; }

		public static PointContext Get(
			Node node, 
			ParsedFile file, 
			List<ParsedFile> searchArea, 
			Func<string, ParsedFile> getParsed,
			ContextFinder contextFinder,
			ContextCore core = null)
		{
			var context = new PointContext
			{
				Core = core ?? ContextCore.Get(node, file),
				Line = node.Location.Start.Line.Value
			};

			if (file.MarkupSettings.UseSiblingsContext)
			{
				context.SiblingsContext = GetSiblingsContext(node, file);
			}
			else
			{
				context.ClosestContext = GetClosestContext(
					node, file, context, searchArea, getParsed, contextFinder
				);
			}

			return context;
		}

		public static byte[] GetHash(Node node, ParsedFile file)
		{
			var text = file.Text.Substring(
					node.Location.Start.Offset,
					node.Location.Length.Value
				);

			return GetHash(text);
		}

		public static byte[] GetHash(string text)
		{
			/// Считаем хеш от всего текста помечаемого элемента за вычетом пробельных символов
			using (var md5 = System.Security.Cryptography.MD5.Create())
			{
				return md5.ComputeHash(Encoding.ASCII.GetBytes(
					System.Text.RegularExpressions.Regex.Replace(text.ToLower(), "[\n\r\f\t ]+", "")
				));
			}
		}

		public static SiblingsContext GetSiblingsContext(Node node, ParsedFile file)
		{
			/// Находим островного родителя
			var parentNode = node.Parent;
			while (parentNode != null && !parentNode.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
				parentNode = parentNode.Parent;

			/// Если это корень, горизонтального контекста нет
			if (parentNode == null)
				return null;

			/// Спускаемся от родителя и собираем первые в глубину потомки-острова
			var siblings = new List<Node>(parentNode.Children);
			for (var i = 0; i < siblings.Count; ++i)
			{
				if (!siblings[i].Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
				{
					var current = siblings[i];
					siblings.RemoveAt(i);
					siblings.InsertRange(i, current.Children);
				}
			}

			/// Индекс помечаемого элемента
			var markedElementIndex = siblings.IndexOf(node);
			siblings.RemoveAt(markedElementIndex);

			var context = new SiblingsContext
			{
				Before = new TextOrHash(String.Join(" ", siblings
					.Take(markedElementIndex)
					.Where(n => n.Location != null)
					.Select(n => file.Text.Substring(n.Location.Start.Offset, n.Location.Length.Value))
				)),
				After = new TextOrHash(String.Join(" ", siblings
					.Skip(markedElementIndex)
					.Where(n => n.Location != null)
					.Select(n => file.Text.Substring(n.Location.Start.Offset, n.Location.Length.Value))
				)),
			};

			return context;
		}

		public static List<ContextCore> GetClosestContext(
			Node node,
			ParsedFile file,
			PointContext nodeContext,
			List<ParsedFile> searchArea,
			Func<string, ParsedFile> getParsed,
			ContextFinder contextFinder)
		{
			const double CLOSE_ELEMENT_THRESHOLD = 0.8;

			/// Отбираем файлы, наиболее похожие на содержащий помечаемый элемент
			var similarFiles = searchArea
				.Where(f => ContextFinder.AreFilesSimilarEnough(f.BindingContext.Content, file.BindingContext.Content))
				.ToList();

			foreach (var f in similarFiles)
			{
				if (f.Root == null)
					f.Root = getParsed(f.Name)?.Root;
			};

			var candidates = new List<RemapCandidateInfo>();

			foreach (var similarFile in similarFiles)
			{
				/// Если не смогли распарсить файл, переходим к следующему
				if (similarFile.Root == null)
					continue;

				var visitor = new GroupNodesByTypeVisitor(new List<string> { node.Type });
				similarFile.Root.Accept(visitor);

				/// Для каждого элемента вычисляем основные контексты
				candidates.AddRange(visitor.Grouped[node.Type].Except(new List<Node> { node })
					.Select(n => new RemapCandidateInfo { Context = new PointContext { Core = ContextCore.Get(n, similarFile) } })
				);
			}

			candidates = contextFinder.EvalCandidates(new ConcernPoint(node, nodeContext), candidates, new LanguageMarkupSettings(null), 1)
				.TakeWhile(c => c.SiblingsSimilarity >= CLOSE_ELEMENT_THRESHOLD)
				.ToList();

			return candidates.Select(c=>c.Context.Core).ToList();
		}
	}
}
