using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Land.Core;
using Land.Core.Parsing.Tree;
using Land.Markup.Binding;
using Land.Markup.Relations;
using Land.Markup.Tree;
using Land.Markup.CoreExtension;

namespace Land.Markup
{
	public class MarkupManager
	{
		public MarkupManager(Func<string, ParsedFile> getParsed, IHeuristic remappingHeuristic)
		{
			ContextFinder.GetParsed = getParsed;
			ContextFinder.Heuristic = remappingHeuristic;

			OnMarkupChanged += InvalidateRelations;
		}

		private RelationsManager Relations { get; set; } = new RelationsManager();

		public List<RelationNotification> TryGetRelations(out RelationsManager relationsManager)
		{
			if (IsValid)
			{
				if (!Relations.IsValid)
					Relations.RefreshCache(Markup);

				relationsManager = Relations;
				return Relations.CheckConsistency();
			}

			relationsManager = null;
			return new List<RelationNotification>();
		}

		public ContextFinder ContextFinder { get; private set; } = new ContextFinder();

		/// <summary>
		/// Коллекция точек привязки
		/// </summary>
		public ObservableCollection<MarkupElement> Markup = new ObservableCollection<MarkupElement>();

		/// <summary>
		/// Событие изменения разметки
		/// </summary>
		public event Action OnMarkupChanged;

		/// <summary>
		/// Очистка разметки
		/// </summary>
		public void Clear()
		{
			Markup.Clear();

			OnMarkupChanged?.Invoke();
		}

		/// <summary>
		/// Проверка того, что вся разметка синхронизирована с кодом
		/// </summary>
		/// <returns></returns>
		public bool IsValid => !GetLinearSequenceVisitor.GetPoints(Markup).Any(p => p.HasInvalidLocation);

		/// <summary>
		/// Помечаем отношения как нерелевантные относительно разметки
		/// </summary>
		public void InvalidateRelations()
		{
			Relations.IsValid = false;
		}

		/// <summary>
		/// Сброс узлов дерева у всех точек, связанных с указанным файлом
		/// </summary>
		public void InvalidatePoints(string fileName)
		{
			var stubNode = new Node("");
			stubNode.SetLocation(new PointLocation(0, 0, 0), new PointLocation(0, 0, 0));

			DoWithMarkup((MarkupElement elem) =>
			{
				if (elem is ConcernPoint concernPoint
					&& concernPoint.Context.FileContext.Name == fileName)
				{
					concernPoint.AstNode = stubNode;
					concernPoint.HasIrrelevantLocation = true;
				}
			});

			ContextFinder.ContextManager.ClearCache(fileName);
		}

		/// <summary>
		/// Удаление элемента разметки
		/// </summary>
		public void RemoveElement(MarkupElement elem)
		{
			if (elem.Parent != null)
				elem.Parent.Elements.Remove(elem);
			else
				Markup.Remove(elem);

			OnMarkupChanged?.Invoke();
		}

		/// <summary>
		/// Добавление функциональности
		/// </summary>
		public Concern AddConcern(string name, string comment = null, Concern parent = null)
		{
			var concern = new Concern(name, comment, parent);
			AddElement(concern);

			OnMarkupChanged?.Invoke();
			return concern;
		}

		/// <summary>
		/// Добавление точки привязки
		/// </summary>
		public ConcernPoint AddConcernPoint(
			Node node, 
			ParsedFile file, 
			List<ParsedFile> searchArea,
			string name = null, 
			string comment = null, 
			Concern parent = null)
		{
			Remap(node.Type, file.Name, searchArea);

			var point = new ConcernPoint(
				node, 
				ContextFinder.ContextManager.GetContext(
					node, 
					file, 
					new SiblingsConstructionArgs(),
					new ClosestConstructionArgs
					{
						SearchArea = GetSimilarOnly(file, searchArea),
						GetParsed = ContextFinder.GetParsed,
						ContextFinder = ContextFinder
					}), 
				parent);

			if (!String.IsNullOrEmpty(name))
			{
				point.Name = name;
			}
			point.Comment = comment;

			AddElement(point);

			OnMarkupChanged?.Invoke();
			return point;
		}

		/// <summary>
		/// Добавление всей "суши", присутствующей в дереве разбора
		/// </summary>
		public void AddLand(
			ParsedFile file,
			List<ParsedFile> searchArea)
		{
			var visitor = new LandExplorerVisitor();
			file.Root.Accept(visitor);

			/// Группируем land-сущности по типу (символу)
			foreach (var group in visitor.Land.GroupBy(l => l.Symbol))
			{
				Remap(group.Key, file.Name, searchArea);

				var concern = AddConcern(group.Key);

				/// В пределах символа группируем по псевдониму
				var subgroups = group.GroupBy(g => g.Alias);

				/// Для всех точек, для которых указан псевдоним
				foreach (var subgroup in subgroups.Where(s => !String.IsNullOrEmpty(s.Key)))
				{
					/// создаём подфункциональность
					var subconcern = AddConcern(subgroup.Key, null, concern);

					foreach (var node in subgroup)
					{
						AddElement(new ConcernPoint(
							node, 
							ContextFinder.ContextManager.GetContext(
								node, 
								file,
								new SiblingsConstructionArgs(),
								new ClosestConstructionArgs
								{
									SearchArea = GetSimilarOnly(file, searchArea),
									GetParsed = ContextFinder.GetParsed,
									ContextFinder = ContextFinder
								}), 
							subconcern));
					}
				}

				/// Остальные добавляются напрямую к функциональности, соответствующей символу
				var nodes = subgroups.Where(s => String.IsNullOrEmpty(s.Key))
					.SelectMany(s => s).ToList();

				foreach (var node in nodes)
				{
					AddElement(new ConcernPoint(
						node, ContextFinder.ContextManager.GetContext(
							node, 
							file,
							new SiblingsConstructionArgs(),
							new ClosestConstructionArgs
							{
								SearchArea = GetSimilarOnly(file, searchArea),
								GetParsed = ContextFinder.GetParsed,
								ContextFinder = ContextFinder
							}), 
						concern)
					);
				}
			}

			OnMarkupChanged?.Invoke();
		}

		/// <summary>
		/// Получение всех узлов, к которым можно привязаться,
		/// если команда привязки была вызвана в позиции offset
		/// </summary>
		public LinkedList<Node> GetConcernPointCandidates(Node root, SegmentLocation selection)
		{
			var pointCandidates = new LinkedList<Node>();
			var currentNode = root;

			/// В качестве кандидатов на роль помечаемого участка рассматриваем узлы от корня,
			/// содержащие текущую позицию каретки
			while (currentNode != null)
			{
				if (currentNode.Options.IsSet(MarkupOption.GROUP_NAME, MarkupOption.LAND))
					pointCandidates.AddFirst(currentNode);

				currentNode = currentNode.Children
					.Where(c => c.Location != null && c.Location.Includes(selection))
					.FirstOrDefault();
			}

			return pointCandidates;
		}

		/// <summary>
		/// Смена узла, к которому привязана точка
		/// </summary>
		public void RelinkConcernPoint(
			ConcernPoint point, 
			Node node, 
			ParsedFile file,
			List<ParsedFile> searchArea)
		{
			point.Relink(node, ContextFinder.ContextManager.GetContext(
				node, 
				file,
				new SiblingsConstructionArgs(),
				new ClosestConstructionArgs
				{
					SearchArea = GetSimilarOnly(file, searchArea),
					GetParsed = ContextFinder.GetParsed,
					ContextFinder = ContextFinder
				})
			);

			OnMarkupChanged?.Invoke();
		}

		/// <summary>
		/// Смена узла, к которому привязана точка
		/// </summary>
		public void RelinkConcernPoint(ConcernPoint point, RemapCandidateInfo candidate)
		{
			point.Relink(candidate);

			OnMarkupChanged?.Invoke();
		}

		/// <summary>
		/// Получение списка точек привязки для текущего дерева разметки
		/// </summary>
		public List<ConcernPoint> GetConcernPoints()
		{
			return GetLinearSequenceVisitor.GetPoints(Markup);
		}

		public HashSet<PointContext> GetPointContexts()
		{
			var contextsSet = new HashSet<PointContext>();
			var points = GetConcernPoints();

			foreach (var point in points)
			{
				contextsSet.Add(point.Context);
				contextsSet.UnionWith(point.Context.ClosestContext);
			}

			return contextsSet;
		}

		/// <summary>
		/// Перемещение элемента разметки к новому родителю
		/// </summary>
		public void MoveTo(Concern newParent, MarkupElement elem)
		{
			if (elem.Parent != null)
				elem.Parent.Elements.Remove(elem);
			else
				Markup.Remove(elem);

			elem.Parent = newParent;

			if(newParent != null)
				newParent.Elements.Add(elem);
			else
				Markup.Add(elem);

			OnMarkupChanged?.Invoke();
		}

		public void Serialize(string fileName, bool useRelativePaths)
		{
			var pointContexts = GetPointContexts();
			var fileContexts = new HashSet<FileContext>(pointContexts.Select(e => e.FileContext));
			
			if (useRelativePaths)
			{
				/// Превращаем указанные в точках привязки абсолютные пути в пути относительно файла разметки
				var directoryUri = new Uri(Path.GetDirectoryName(fileName) + "/");

				foreach (var file in fileContexts)
				{
					file.Name = Uri.UnescapeDataString(
						directoryUri.MakeRelativeUri(new Uri(file.Name)).ToString()
					);
				}
			}

			using (StreamWriter fs = new StreamWriter(fileName, false, System.Text.Encoding.UTF8))
			{
				var unit = new SerializationUnit()
				{
					Markup = Markup,
					PointContexts = pointContexts,
					FileContexts = fileContexts,
					ExternalRelatons = Relations.ExternalRelations.GetRelatedPairs()
				};

				fs.Write(JsonConvert.SerializeObject(unit, Formatting.Indented));
			}

			if (useRelativePaths)
			{
				foreach (var file in fileContexts)
				{
					file.Name = Path.GetFullPath(
						Path.Combine(Path.GetDirectoryName(fileName), file.Name)
					);
				}
			}
		}

		public void Deserialize(string fileName)
		{
			Clear();

			using (StreamReader fs = new StreamReader(fileName))
			{
				var unit = JsonConvert.DeserializeObject<SerializationUnit>(fs.ReadToEnd(),
					new JsonSerializerSettings()
					{
						Converters = { new MarkupElementConverter() }
					});

				/// Фиксируем разметку
				Markup = unit.Markup;

				/// Восстанавливаем обратные связи между потомками и предками,
				/// восстанавливаем связи с контекстами
				var concernPoints = GetConcernPoints().ToDictionary(e=>e.Id, e=>e);

				/// Связываем контексты с точками привязки в разметке
				foreach (var context in unit.PointContexts)
				{
					foreach(var id in context.LinkedPoints)
					{
						concernPoints[id].Context = context;
					}
				}

				/// Связываем файловые контексты с контекстами точек
				foreach (var fileContext in unit.FileContexts)
				{
					foreach (var id in fileContext.LinkedPoints)
					{
						concernPoints[id].Context.FileContext = fileContext;
					}
				}

				/// Связываем контексты-описания ближайших с контекстами точек
				foreach (var context in unit.PointContexts)
				{
					foreach (var pair in context.LinkedClosestPoints)
					{
						if(concernPoints[pair.Item1].Context.ClosestContext == null)
						{
							concernPoints[pair.Item1].Context.ClosestContext = new List<PointContext>();
						}

						if(concernPoints[pair.Item1].Context.ClosestContext.Count <= pair.Item2)
						{
							concernPoints[pair.Item1].Context.ClosestContext.AddRange(Enumerable.Repeat<PointContext>(
								null, pair.Item2 - concernPoints[pair.Item1].Context.ClosestContext.Count + 1)
							);
						}

						concernPoints[pair.Item1].Context.ClosestContext[pair.Item2] = context;
					}
				}

				/// Если у каких-то точек нет ближайших, присваиваем пустой массив
				foreach(var point in concernPoints.Values)
				{
					if(point.Context.ClosestContext == null)
					{
						point.Context.ClosestContext = new List<PointContext>();
					}
				}

				DoWithMarkup(e =>
				{
					if (e is Concern c)
					{
						foreach (var elem in c.Elements)
						{
							elem.Parent = c;
						}
					}
				});

				/// Запоминаем external-отношения между функциональностями
				Relations.RefreshElements(Markup);

				foreach (var pair in unit.ExternalRelatons)
					Relations.AddExternalRelation(pair.RelationType, pair.Item0, pair.Item1);
			}

			DoWithMarkup((MarkupElement elem) =>
			{
				if (elem is ConcernPoint p && !Path.IsPathRooted(p.Context.FileContext.Name))
				{
					p.Context.FileContext.Name = Path.GetFullPath(
						Path.Combine(Path.GetDirectoryName(fileName), p.Context.FileContext.Name)
					);
				}
			});
		}

		/// <summary>
		/// Поиск узла дерева, которому соответствует заданная точка привязки
		/// </summary>
		public List<RemapCandidateInfo> Find(ConcernPoint point, ParsedFile targetInfo)
		{
			return ContextFinder.Find(
				new List<ConcernPoint> { point }, 
				new List<ParsedFile> { targetInfo }, 
				ContextFinder.SearchType.Local
			)[point];
		}

		/// <summary>
		/// Получение списка файлов, в которых есть точки привязки
		/// </summary>
		public HashSet<string> GetReferencedFiles()
		{
			return new HashSet<string>(
				GroupPointsByFileVisitor.GetGroups(Markup).Select(p => p.Key)
			);
		}

		#region Перепривязка

		/// <summary>
		/// Размер топа кандидатов, ранжированных по похожести, возвращаемого при неоднозначности
		/// </summary>
		public int AmbiguityTopCount { get; set; } = 10;

		/// <summary>
		/// Похожесть, ниже которой не рассматриваем элемент как кандидата
		/// </summary>
		public double GarbageThreshold { get; set; } = 0.4;

		/// <summary>
		/// Перепривязка всех точек разметки
		/// </summary>
		/// <param name="searchArea">
		/// Множество файлов проекта
		/// </param>
		/// <param name="allowAutoDecisions">
		/// Признак того, что разрешено проводить автоматическую перепривязку
		/// </param>
		/// <param name="searchType">
		/// Поиск точки проводится по тому же файлу или по всему проекту
		/// </param>
		/// <returns></returns>
		public Dictionary<ConcernPoint, List<RemapCandidateInfo>> Remap(
			List<ParsedFile> searchArea,
			bool allowAutoDecisions,
			ContextFinder.SearchType searchType)
		{
			var ambiguous = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();
			var points = GetConcernPoints();

			/// Локальный поиск имеет смысл проводить, если только его провести и нужно,
			/// или если нужен глобальный поиск, но разрешена автоперепривязка
			var result = allowAutoDecisions || searchType == ContextFinder.SearchType.Local
				? ContextFinder.Find(points, searchArea, ContextFinder.SearchType.Local)
				: new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();

			/// Если требуется глобальный поиск, 
			/// выполняем его для того, что не нашли локально, 
			/// потом мёржим результаты
			if(searchType == ContextFinder.SearchType.Global)
			{
				points = points.Except(result
					.Where(e => e.Value.FirstOrDefault()?.IsAuto ?? false)
					.Select(e => e.Key)
				).ToList();

				var globalResult = ContextFinder.Find(points, searchArea, ContextFinder.SearchType.Global);

				foreach(var key in globalResult.Keys)
				{
					result[key] = globalResult[key];
				}
			}


			foreach (var kvp in result)
			{
				var candidates = kvp.Value
					//.TakeWhile(c=>c.Similarity >= GarbageThreshold)
					.Take(AmbiguityTopCount).ToList();

				if (!allowAutoDecisions || 
					!ApplyCandidate(kvp.Key, candidates, searchArea))
					ambiguous[kvp.Key] = candidates;
			}

			OnMarkupChanged?.Invoke();

			return ambiguous;
		}

		/// <summary>
		/// Перепривязка всех точек заданного типа в заданном файле
		/// </summary>
		public Dictionary<ConcernPoint, List<RemapCandidateInfo>> Remap(
			string pointType,
			string fileName,
			List<ParsedFile> searchArea)
		{
			var points = GetConcernPoints()
				.Where(p => p.Context.Type == pointType
					&& p.Context.FileContext.Name == fileName)
				.ToList();

			var ambiguous = new Dictionary<ConcernPoint, List<RemapCandidateInfo>>();

			var result = ContextFinder.Find(points, searchArea, ContextFinder.SearchType.Local);
			var keys = result.Keys.ToList();

			foreach (var key in keys)
			{
				result[key] = result[key]
					.TakeWhile(c => c.Similarity >= GarbageThreshold)
					.Take(AmbiguityTopCount)
					.ToList();

				if (!ApplyCandidate(key, result[key], searchArea))
					ambiguous[key] = result[key];
			}

			OnMarkupChanged?.Invoke();

			return ambiguous;
		}

		private bool ApplyCandidate(
			ConcernPoint point, 
			IEnumerable<RemapCandidateInfo> candidates,
			List<ParsedFile> searchArea)
		{
			var first = candidates.FirstOrDefault();

			if (first?.IsAuto ?? false)
			{
				point.Context = ContextFinder.ContextManager.GetContext(
					first.Node, 
					first.File, 
					new SiblingsConstructionArgs(),
					new ClosestConstructionArgs
					{
						SearchArea = GetSimilarOnly(first.File, searchArea),
						GetParsed = ContextFinder.GetParsed,
						ContextFinder = ContextFinder
					}
				);

				point.AstNode = first.Node;
				return true;
			}
			else
			{
				point.AstNode = null;
				return false;
			}
		}

		#endregion

		/// <summary>
		/// Обобщённое добавление элемента разметки
		/// </summary>
		/// <param name="elem"></param>
		private void AddElement(MarkupElement elem)
		{
			if (elem.Parent == null)
				Markup.Add(elem);
			else
				elem.Parent.Elements.Add(elem);
		}

		/// <summary>
		/// Совершение заданного действия со всеми элементами разметки
		/// </summary>
		public void DoWithMarkup(Action<MarkupElement> action)
		{
			foreach (var elem in Markup)
				DoWithMarkupSubtree(action, elem);
		}

		/// <summary>
		/// Совершение заданного действия со всеми элементами поддерева разметки
		/// </summary>
		private void DoWithMarkupSubtree(Action<MarkupElement> action, MarkupElement root)
		{
			var elements = new Queue<MarkupElement>();
			elements.Enqueue(root);

			while (elements.Count > 0)
			{
				var elem = elements.Dequeue();

				if (elem is Concern concern)
					foreach (var child in concern.Elements)
						elements.Enqueue(child);

				action(elem);
			}
		}

		private List<ParsedFile> GetSimilarOnly(ParsedFile source, List<ParsedFile> searchArea) => 
			searchArea.Where(f => ContextFinder.AreFilesSimilarEnough(source.BindingContext, f.BindingContext)).ToList();
	}
}
