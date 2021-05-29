using Land.Core;
using Land.Core.Parsing.Tree;
using Land.Core.Specification;
using Land.Markup.CoreExtension;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Land.Markup.Binding
{
	public interface IEqualsIgnoreValue
	{
		bool EqualsIgnoreValue(object obj);

		int GetHashCodeIgnoreValue();
	}

	public class PrioritizedWord
	{
		public double Priority { get; set; }
		public string Text { get; set; }

		public override bool Equals(object obj)
		{
			if (obj is PrioritizedWord elem)
			{
				return ReferenceEquals(this, elem) || Priority == elem.Priority
					&& Text == elem.Text;
			}

			return false;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = 1685606927;
				hashCode = hashCode * -1521134295 + Priority.GetHashCode();
				hashCode = hashCode * -1521134295 + Text.GetHashCode();

				return hashCode;
			}
		}
	}

	public class HeaderContextElement: IEqualsIgnoreValue
	{
		public string Type { get; set; }

		[JsonIgnore]
		public double Priority { get; set; }

		[JsonIgnore]
		public bool ExactMatch { get; set; }

		[JsonIgnore]
		public List<PrioritizedWord> Value { get; set; }

		[JsonProperty]
		private string SerializableValue
        {
			get => String.Join(" ", Value.Select(e=>e.Text));

            set
            {
				Value = GetWords(value);
            }
        }

		/// Проверка двух контекстов на совпадение всех полей, кроме поля Value
		public bool EqualsIgnoreValue(object obj)
		{
			if (obj is HeaderContextElement elem)
			{
				return ReferenceEquals(this, elem) || Priority == elem.Priority
					&& Type == elem.Type
					&& ExactMatch == elem.ExactMatch;
			}

			return false;
		}

		public override bool Equals(object obj)
		{
			if(obj is HeaderContextElement elem)
			{
				return ReferenceEquals(this, elem) || Priority == elem.Priority 
					&& Type == elem.Type
					&& ExactMatch == elem.ExactMatch
					&& Value.SequenceEqual(elem.Value);
			}

			return false;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = 1685606927;
				hashCode = hashCode * -1521134295 + ExactMatch.GetHashCode();
				hashCode = hashCode * -1521134295 + Priority.GetHashCode();
				hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Type);

				foreach (var elem in Value)
				{
					hashCode = hashCode * -1521134295 
						+ EqualityComparer<PrioritizedWord>.Default.GetHashCode(elem);
				}

				return hashCode;
			}
		}

		public int GetHashCodeIgnoreValue()
		{
			unchecked
			{
				var hashCode = 1685606927;
				hashCode = hashCode * -1521134295 + ExactMatch.GetHashCode();
				hashCode = hashCode * -1521134295 + Priority.GetHashCode();
				hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Type);

				return hashCode;
			}
		}

		public static bool operator ==(HeaderContextElement a, HeaderContextElement b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(HeaderContextElement a, HeaderContextElement b)
		{
			return !a.Equals(b);
		}

		public static explicit operator HeaderContextElement(Node node)
		{
			var isExactMatch = node.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.EXACTMATCH);

			return new HeaderContextElement()
			{
				Type = node.Type,
				Value = isExactMatch
					? new List<PrioritizedWord> { new PrioritizedWord { Text = String.Join("", node.Value), Priority = 1 } }
					: node.Value.SelectMany(e=>GetWords(e)).ToList(),
				Priority = node.Options.GetPriority().Value,
				ExactMatch = isExactMatch
			};
		}

		public static List<PrioritizedWord> GetWords(string str)
		{
			var result = new List<PrioritizedWord>();
			var splitted = str.Split(
				new char[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries
			);

			foreach (var part in splitted)
			{
				var firstIdx = 0;

				for (var i = 1; i < part.Length; ++i)
				{
					if (Char.IsLower(part[i - 1]) && Char.IsUpper(part[i])
						|| Char.IsUpper(part[i - 1]) && Char.IsUpper(part[i]) && i < part.Length - 1 && Char.IsLower(part[i + 1])
						|| Char.IsLetterOrDigit(part[i - 1]) != Char.IsLetterOrDigit(part[i])
						|| Char.IsDigit(part[i - 1]) != Char.IsDigit(part[i]))
					{
						var word = part.Substring(firstIdx, i - firstIdx);
						result.Add(new PrioritizedWord
						{
							Text = word,
							Priority = word.All(c => char.IsLetterOrDigit(c)) ? 1 : 0.1
						});

						firstIdx = i;
					}
				}

				var lastWord = part.Substring(firstIdx);
				result.Add(new PrioritizedWord
				{
					Text = lastWord,
					Priority = lastWord.All(c=>char.IsLetterOrDigit(c)) ? 1 : 0.1
				});
			}

			return result;
		}
	}

	public class AncestorsContextElement
	{
		public string Type { get; set; }

		public HeaderContext HeaderContext { get; set; }

		public override bool Equals(object obj)
		{
			if (obj is AncestorsContextElement elem)
			{
				return ReferenceEquals(this, elem) || Type == elem.Type
					&& HeaderContext.Equals(elem.HeaderContext);
			}

			return false;
		}

		public static bool operator ==(AncestorsContextElement a, AncestorsContextElement b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(AncestorsContextElement a, AncestorsContextElement b)
		{
			return !a.Equals(b);
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public static explicit operator AncestorsContextElement(Node node)
		{
			return new AncestorsContextElement()
			{
				Type = node.Type,
				HeaderContext = PointContext.GetHeaderContext(node)
			};
		}
	}

	public class InnerContext
	{
		public TextOrHash Content { get; set; }

		public InnerContext() 
		{ 
			Content = new TextOrHash(); 
		}

		public InnerContext(List<SegmentLocation> locations, string fileText)
		{
			var text = String.Join(" ", locations.Select(l => 
				fileText.Substring(l.Start.Offset, l.Length.Value)
			));

			Content = new TextOrHash(text);
		}
	}

	public class HeaderContext
	{
		public List<HeaderContextElement> Sequence { get; set; }

		[JsonIgnore]
		public List<int> NonCoreIndices { get; set; }

		[JsonIgnore]
		public List<int> CoreIndices { get; set; }

		[JsonIgnore]
		public List<HeaderContextElement> NonCore =>
			NonCoreIndices.Select(i => Sequence[i]).ToList();

		[JsonIgnore]
		public List<HeaderContextElement> Core =>
			CoreIndices.Select(i => Sequence[i]).ToList();

		#region Old

		[JsonIgnore]
		public List<string> Sequence_old { get; set; }

		[JsonIgnore]
		public string Core_old =>
			String.Join("", Core.Select(e => String.Join("", e.Value.Select(word => word.Text))).ToList());

		#endregion

		public override bool Equals(object obj)
		{
			if (obj is HeaderContext elem)
			{
				return ReferenceEquals(this, elem) 
					|| Sequence.SequenceEqual(elem.Sequence);
			}

			return false;
		}

		public bool EqualsByCore(object obj)
		{
			if (obj is HeaderContext elem)
			{
				return ReferenceEquals(this, elem) ||
					Core.SequenceEqual(elem.Core);
			}

			return false;
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}
	}

	#region Old

	public class ContextElement
	{
		public string Type { get; set; }

		public List<string> HeaderContext { get; set; }

		public override bool Equals(object obj)
		{
			if (obj is ContextElement elem)
			{
				return ReferenceEquals(this, elem) || Type == elem.Type
					&& HeaderContext.SequenceEqual(elem.HeaderContext);
			}

			return false;
		}

		public override int GetHashCode()
		{
			unchecked
			{
				var hashCode = 1660800360;
				hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Type);

				foreach(var elem in HeaderContext)
				{
					hashCode = hashCode * -1521134295 + 
						EqualityComparer<string>.Default.GetHashCode(elem);
				}

				return hashCode;
			}
		}

		public static bool operator ==(ContextElement a, ContextElement b)
		{
			return a.Equals(b);
		}

		public static bool operator !=(ContextElement a, ContextElement b)
		{
			return !a.Equals(b);
		}

		public static explicit operator ContextElement(Node node)
		{
			return new ContextElement()
			{
				Type = node.Type,
				HeaderContext = PointContext.GetHeaderContext(node).Sequence.SelectMany(e=>e.Value.Select(valElem => valElem.Text)).ToList()
			};
		}
	}

	#endregion

	public class SiblingsContext
	{
		public SiblingsContextPart Before { get; set; }
		public SiblingsContextPart After { get; set; }
	}

	public class SiblingsContextPart
	{
		public TextOrHash All { get; set; }

		[JsonIgnore]
		public PointContext Nearest { get; set; }

		[JsonIgnore]
		public bool IsNotEmpty => All.TextLength > 0;
	}

	public class AncestorSiblingsPair
	{
		public Node Ancestor { get; set; }
		public List<Node> Siblings { get; set; }
	}

	public class PointContext
	{
		#region Links

		/// <summary>
		/// Идентификаторы связанных точек привязки
		/// </summary>
		public HashSet<Guid> LinkedPoints { get; private set; } = new HashSet<Guid>();

		/// <summary>
		/// Идентификаторы точек привязки,
		/// для которых данный контекст описывает ближайшую сущность
		/// </summary>
		public HashSet<Guid> LinkedClosestPoints { get; private set; } = new HashSet<Guid>();

		public HashSet<Guid> LinkedAfterNeighbours { get; private set; } = new HashSet<Guid>();

		public HashSet<Guid> LinkedBeforeNeighbours { get; private set; } = new HashSet<Guid>();

		public void LinkPoint(Guid pointId)
		{
			this.LinkedPoints.Add(pointId);

			if (this.ClosestContext != null)
			{
				foreach (var element in this.ClosestContext)
				{
					element.LinkedClosestPoints.Add(pointId);
				}
			}

			this.SiblingsContext.Before.Nearest?.LinkedAfterNeighbours.Add(pointId);
			this.SiblingsContext.After.Nearest?.LinkedBeforeNeighbours.Add(pointId);
		}

		#endregion

		/// <summary>
		/// Тип сущности, которой соответствует точка привязки
		/// </summary>
		public string Type { get; set; }

		/// <summary>
		/// Номер строки в файле, на которой начинается сущность
		/// </summary>
		public int Line { get; set; }

		public int StartOffset { get; set; }

		public int EndOffset { get; set; }

		public string FileName { get; set; }

		/// <summary>
		/// Контекст заголовка узла, к которому привязана точка разметки
		/// </summary>
		public HeaderContext HeaderContext { get; set; }

		/// <summary>
		/// Внутренний контекст в виде одной сущности
		/// </summary>
		public InnerContext InnerContext { get; set; }

		/// <summary>
		/// Контекст предков узла, к которому привязана точка разметки
		/// </summary>
		public List<AncestorsContextElement> AncestorsContext { get; set; }

		/// <summary>
		/// Контекст уровня, на котором находится узел, к которому привязана точка разметки
		/// </summary>
		public SiblingsContext SiblingsContext { get; set; }

		/// <summary>
		/// Контекст наиболее похожих на помеченный элементов
		/// </summary>
		[JsonIgnore]
		public HashSet<PointContext> ClosestContext { get; set; }

		#region Old

		[JsonIgnore]
		public List<ContextElement> InnerContext_old { get; set; }

		[JsonIgnore]
		public Tuple<List<ContextElement>, List<ContextElement>> SiblingsContext_old { get; set; }

		#endregion

		public static PointContext GetCoreContext(
			Node node,
			ParsedFile file)
		{
			return new PointContext
			{
				Type = node.Alias ?? node.Symbol,

				FileName = file.Name,
				Line = node.Location.Start.Line.Value,
				StartOffset = node.Location.Start.Offset,
				EndOffset = node.Location.End.Offset,

				HeaderContext = GetHeaderContext(node),
				InnerContext = GetInnerContext(node, file),
				AncestorsContext = GetAncestorsContext(node),

				#region Old
				InnerContext_old = GetInnerContext_old(node, file)
				#endregion
			};
		}

		public static PointContext GetExtendedContext(
			Node node, 
			ParsedFile file, 
			SiblingsConstructionArgs siblingsArgs,
			ClosestConstructionArgs closestArgs,
			PointContext core = null)
		{
			if (core == null)
			{
				core = PointContext.GetCoreContext(node, file);
			}

			if (siblingsArgs !=null && core.SiblingsContext == null)
			{
				core.SiblingsContext = GetSiblingsContext(node, file, siblingsArgs.ContextFinder);

				#region Old

				core.SiblingsContext_old= GetSiblingsContext_old(node, file);

				#endregion old
			}
			if (closestArgs != null && core.ClosestContext == null)
			{
				core.ClosestContext = GetClosestContext(
					node, file, core, 
					closestArgs.SearchArea, closestArgs.GetParsed, closestArgs.ContextFinder
				);
			}

			return core;
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

		public static Node GetAncestor(Node node)
		{
			var currentNode = node.Parent;

			while (currentNode != null)
			{
				if (currentNode.Symbol != Grammar.CUSTOM_BLOCK_RULE_NAME
					&& currentNode.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
					return currentNode;

				currentNode = currentNode.Parent;
			}

			return currentNode;
		}

		public static HeaderContext GetHeaderContext(Node node)
		{
			var headerCoreTypes = new HashSet<string>(node.Options.GetHeaderCore());
			List<Node> sequence;

			if (node.Value.Count > 0)
			{
				sequence = new List<Node>() { node };
			}
			else
			{
				sequence = new List<Node>();

				var stack = new Stack<Node>(Enumerable.Reverse(node.Children));

				while (stack.Any())
				{
					var current = stack.Pop();

					if ((current.Children.Count == 0 ||
						current.Children.All(c => c.Type == Grammar.CUSTOM_BLOCK_RULE_NAME)) &&
						current.Options.GetPriority() > 0)
					{
						sequence.Add(current);
					}
					else
					{
						if (current.Type == Grammar.CUSTOM_BLOCK_RULE_NAME)
						{
							for (var i = current.Children.Count - 2; i >= 1; --i)
							{
								stack.Push(current.Children[i]);
							}
						}
					}
				}
			}

			var headerSequence = sequence.Select(e => (HeaderContextElement)e).ToList();
			var grouped = sequence
				.Select((e, i) => new { elem = e, idx = i })
				.GroupBy(e => headerCoreTypes.Contains(e.elem.Symbol) || headerCoreTypes.Contains(e.elem.Alias))
				.ToDictionary(g => g.Key, g => g.ToList());

			return new HeaderContext
			{
				Sequence = headerSequence,
				NonCoreIndices = grouped.ContainsKey(false)
					? grouped[false].Select(e => e.idx).ToList()
					: new List<int>(),
				CoreIndices = grouped.ContainsKey(true)
					? grouped[true].Select(e => e.idx).ToList()
					: new List<int>(),
				Sequence_old = sequence.SelectMany(e=>e.Value).ToList(),
			};
		}

		public static List<AncestorsContextElement> GetAncestorsContext(Node node)
		{
			var context = new List<AncestorsContextElement>();
			var currentNode = node.Parent;

			while (currentNode != null)
			{
				if (currentNode.Symbol != Grammar.CUSTOM_BLOCK_RULE_NAME
					&& currentNode.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
					context.Add((AncestorsContextElement)currentNode);

				currentNode = currentNode.Parent;
			}

			return context;
		}

		public static InnerContext GetInnerContext(Node node, ParsedFile file)
		{
			var locations = new List<SegmentLocation>();
			var stack = new Stack<Node>(Enumerable.Reverse(node.Children));

			while (stack.Any())
			{
				var current = stack.Pop();

				if (current.Type != Grammar.ANY_TOKEN_NAME && current.Children.Count > 0)
				{
					if (current.Type != Grammar.CUSTOM_BLOCK_RULE_NAME)
					{
						locations.Add(current.Location);
					}
					else
					{
						for (var i = current.Children.Count - 2; i >= 1; --i)
							stack.Push(current.Children[i]);
					}
				}
			}

			return new InnerContext(locations, file.Text);
		}

		#region Old

		public static List<ContextElement> GetInnerContext_old(Node node, ParsedFile file)
		{
			const int INNER_CONTEXT_LENGTH = 10;

			var result = new List<ContextElement>();
			var stack = new Stack<Node>(Enumerable.Reverse(node.Children));

			while (stack.Any())
			{
				var current = stack.Pop();

				if (current.Children.Count > 0)
				{
					if (current.Type != Grammar.CUSTOM_BLOCK_RULE_NAME)
					{
						if (current.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
						{
							result.Add((ContextElement)current);
						}
					}
					else
					{
						for (var i = current.Children.Count - 2; i >= 1; --i)
							stack.Push(current.Children[i]);
					}
				}
			}

			return result.Take(INNER_CONTEXT_LENGTH).ToList();
		}

		public static Tuple<List<ContextElement>, List<ContextElement>> GetSiblingsContext_old(
			Node node,
			ParsedFile file,
			AncestorSiblingsPair pair = null)
		{
			Node ancestor = null;
			List<Node> siblings = null;

			if (pair?.Ancestor != null)
			{
				ancestor = pair.Ancestor;
				goto SkipParentSearch;
			}

			/// Находим островного родителя
			ancestor = PointContext.GetAncestor(node)
				?? (node != file.Root ? file.Root : null);

			/// Если при подъёме дошли до неостровного корня, 
			/// и сам элемент не является этим корнем
			if (ancestor == null)
			{
				return new Tuple<List<ContextElement>, List<ContextElement>>(
					new List<ContextElement>(),
					new List<ContextElement>()
				);
		}

			if (pair != null)
			{
				pair.Ancestor = ancestor;
			}

		SkipParentSearch:

			if (pair?.Siblings != null)
			{
				siblings = pair.Siblings.ToList();
				goto SkipSiblingsSearch;
			}

			/// Спускаемся от родителя и собираем первые в глубину потомки-острова
			siblings = new List<Node>(ancestor.Children);

			for (var i = 0; i < siblings.Count; ++i)
			{
				if (!siblings[i].Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
				{
					var current = siblings[i];
					siblings.RemoveAt(i);
					siblings.InsertRange(i, current.Children);

					--i;
				}
			}

			if (pair != null)
			{
				pair.Siblings = siblings.ToList();
			}

		SkipSiblingsSearch:

			/// Индекс помечаемого элемента
			var markedElementIndex = siblings.IndexOf(node);
			siblings.RemoveAt(markedElementIndex);

			var beforeSiblings = siblings
				.Take(markedElementIndex)
				.Where(n => n.Location != null)
				.ToList();
			var afterSiblings = siblings
				.Skip(markedElementIndex)
				.Where(n => n.Location != null)
				.ToList();

			return new Tuple<List<ContextElement>, List<ContextElement>>(
					beforeSiblings.Select(e => new ContextElement
					{
						Type = e.Type,
						HeaderContext = PointContext.GetHeaderContext(e).Sequence
							.SelectMany(el => el.Value.Select(valElem => valElem.Text)).ToList()
					}).ToList(),
					afterSiblings.Select(e => new ContextElement
					{
						Type = e.Type,
						HeaderContext = PointContext.GetHeaderContext(e).Sequence
							.SelectMany(el => el.Value.Select(valElem => valElem.Text)).ToList()
					}).ToList()
				);
		}

		#endregion

		public static SiblingsContext GetSiblingsContext(
			Node node, 
			ParsedFile file,
			ContextFinder contextFinder,
			AncestorSiblingsPair pair = null)
		{
			var checkAllSiblings = node.Options.GetNotUnique();

			Node ancestor = null;
			List<Node> siblings = null;

			if (pair?.Ancestor != null)
			{
				ancestor = pair.Ancestor;
				goto SkipParentSearch;
			}

			/// Находим островного родителя
			ancestor = PointContext.GetAncestor(node)
				?? (node != file.Root ? file.Root : null);

			/// Если при подъёме дошли до неостровного корня, 
			/// и сам элемент не является этим корнем
			if (ancestor == null)
			{ 
				return new SiblingsContext
				{
					After = new SiblingsContextPart { All = new TextOrHash() },
					Before = new SiblingsContextPart { All = new TextOrHash() }
				};
			}

			if(pair != null)
			{
				pair.Ancestor = ancestor;
			}

		SkipParentSearch:

			if(pair?.Siblings != null)
			{
				siblings = pair.Siblings.ToList();
				goto SkipSiblingsSearch;
			}

			/// Спускаемся от родителя и собираем первые в глубину потомки-острова
			siblings = new List<Node>(ancestor.Children);
			for (var i = 0; i < siblings.Count; ++i)
			{
				if (!siblings[i].Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
				{
					var current = siblings[i];
					siblings.RemoveAt(i);
					siblings.InsertRange(i, current.Children);

					--i;
				}
			}

			if(pair != null)
			{
				pair.Siblings = siblings.ToList();
			}

		SkipSiblingsSearch:

			/// Индекс помечаемого элемента
			var markedElementIndex = siblings.IndexOf(node);
			siblings.RemoveAt(markedElementIndex);

			var beforeBuilder = new StringBuilder();

			if (checkAllSiblings)
			{
				var beforeSiblings = checkAllSiblings
					? siblings
						.Take(markedElementIndex)
						.Where(n => n.Location != null)
						.ToList()
					: new List<Node>();

				foreach (var part in beforeSiblings
						.Select(n => file.Text.Substring(n.Location.Start.Offset, n.Location.Length.Value)))
				{
					beforeBuilder.Append(part);
				}
			}
			
			var afterBuilder = new StringBuilder();

			if (checkAllSiblings)
			{
				var afterSiblings = checkAllSiblings
				? siblings
					.Skip(markedElementIndex)
					.Where(n => n.Location != null)
					.ToList()
				: new List<Node>();

				foreach (var part in afterSiblings
						.Select(n => file.Text.Substring(n.Location.Start.Offset, n.Location.Length.Value)))
				{
					afterBuilder.Append(part);
				}
			}

			var beforeNeighbor = siblings
				.Take(markedElementIndex)
				.Reverse()
				.FirstOrDefault(e => e.Type == node.Type && e.Location != null);
			var afterNeighbour = siblings
				.Skip(markedElementIndex + 1)
				.FirstOrDefault(e => e.Type == node.Type && e.Location != null);

			var context = new SiblingsContext
			{
				Before = new SiblingsContextPart {
					All = new TextOrHash(beforeBuilder.ToString()),
					Nearest = !checkAllSiblings
						? beforeNeighbor != null ? contextFinder.ContextManager.GetContext(beforeNeighbor, file) : null
						: null
				},

				After = new SiblingsContextPart
				{
					All = new TextOrHash(afterBuilder.ToString()),
					Nearest = !checkAllSiblings
						? afterNeighbour != null ? contextFinder.ContextManager.GetContext(afterNeighbour, file) : null
						: null
				}
			};

			return context;
		}

		public static HashSet<PointContext> GetClosestContext(
			Node node,
			ParsedFile file,
			PointContext nodeContext,
			List<ParsedFile> searchArea,
			Func<string, ParsedFile> getParsed,
			ContextFinder contextFinder)
		{
			const double CLOSE_ELEMENT_THRESHOLD = 0.7;
			const int MAX_COUNT = 5;

			var candidates = new List<RemapCandidateInfo>();

			foreach (var f in searchArea)
			{
				if (f.Root == null)
				{
					f.Root = getParsed(f.Name)?.Root;
				}

				var visitor = new GroupNodesByTypeVisitor(new List<string> { node.Type });
				file.Root.Accept(visitor);

				candidates.AddRange(visitor.Grouped[node.Type].Except(new List<Node> { node })
					.Select(n => new RemapCandidateInfo
					{
						Node = n,
						File = file,
						Context = contextFinder.ContextManager.GetContext(n, file)
					})
				);
			};

			#region For simple rebinding

			var mayBeConfused = new List<RemapCandidateInfo>();

			mayBeConfused.AddRange(candidates
				.Where(c => c.Context.AncestorsContext.SequenceEqual(nodeContext.AncestorsContext)
					&& c.Context.HeaderContext.Core.SequenceEqual(nodeContext.HeaderContext.Core))
			);

			if (mayBeConfused.Count > 0)
			{
				var sameHeader = mayBeConfused
					.Where(c => c.Context.HeaderContext.NonCore.SequenceEqual(nodeContext.HeaderContext.NonCore))
					.ToList();

				if (sameHeader.Count > 0)
				{
					mayBeConfused = sameHeader;

					var sameInner = mayBeConfused
						.Where(c => c.Context.InnerContext.Content.Text == nodeContext.InnerContext.Content.Text
							&& (c.Context.InnerContext.Content.Hash?.SequenceEqual(nodeContext.InnerContext.Content.Hash) ?? true))
						.ToList();

					if (sameInner.Count > 0)
					{
						mayBeConfused = sameInner;
					}
				}
			}

			#endregion

			contextFinder.ComputeCoreContextSimilarities(nodeContext, candidates);
			contextFinder.ComputeTotalSimilarities(nodeContext, candidates);

			var result = candidates
				.OrderByDescending(c => c.Similarity)
				.ThenByDescending(c => c.AncestorSimilarity)
				.Take(MAX_COUNT)
				.TakeWhile(c => c.Similarity >= CLOSE_ELEMENT_THRESHOLD)
				.ToList();

			if (mayBeConfused.Any() && result.Any())
			{
				/// Если мы не захватили тот элемент, 
				/// с которым можно перепутать помечаемый
				/// из-за полной похожести предков и заголовка
				if(!result.Contains(mayBeConfused[0]))
				{
					if (result.Count == MAX_COUNT)
					{
						result.RemoveAt(result.Count - 1);
					}

					result.Add(mayBeConfused[0]);
				}
			}

			if (node.Options.GetNotUnique())
			{
				foreach (var elem in result)
				{
					elem.Context.SiblingsContext = 
						GetSiblingsContext(elem.Node, elem.File, contextFinder);
				}
			}

			return new HashSet<PointContext>(result.Select(e=>e.Context));
		}
	}

	public class TextOrHash
	{
		public const int MAX_TEXT_LENGTH = 100;

		public string Text { get; set; }

		public int TextLength { get; set; }

		public byte[] Hash { get; set; }

		public TextOrHash() { }

		public TextOrHash(string text)
		{
			text = System.Text.RegularExpressions.Regex.Replace(
				text.ToLower(), "[\n\r\f\t ]+", ""
			);

			TextLength = text.Length;

			/// Хэш от строки можем посчитать, только если длина строки
			/// больше заданной константы
			if (text.Length > FuzzyHashing.MIN_TEXT_LENGTH)
			{
				Hash = FuzzyHashing.GetFuzzyHash(text);
			}

			if (text.Length <= MAX_TEXT_LENGTH)
			{
				Text = text;
			}
		}
	}

	public class EqualsIgnoreValueComparer :
		IEqualityComparer<IEqualsIgnoreValue>
	{
		public bool Equals(IEqualsIgnoreValue e1,
			IEqualsIgnoreValue e2) => e1.EqualsIgnoreValue(e2);

		public int GetHashCode(IEqualsIgnoreValue e)
			=> e.GetHashCodeIgnoreValue();
	}

	public class SiblingsConstructionArgs 
	{
		public ContextFinder ContextFinder { get; set; }
	}

	public class ClosestConstructionArgs
	{
		public List<ParsedFile> SearchArea { get; set; }
		public Func<string, ParsedFile> GetParsed { get; set; }
		public ContextFinder ContextFinder { get; set; }
	}
}
