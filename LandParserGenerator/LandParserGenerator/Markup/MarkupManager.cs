﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using Land.Core.Parsing;
using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	[Serializable]
	public class MarkupManager
	{
		/// <summary>
		/// Коллекция точек привязки
		/// </summary>
		public ObservableCollection<MarkupElement> Markup = new ObservableCollection<MarkupElement>();

		/// <summary>
		/// Исходные тексты файлов, к которым осуществлена привязка
		/// </summary>
		public Dictionary<string, string> Sources = new Dictionary<string, string>();

		/// <summary>
		/// Деревья для файлов, к которым осуществлена привязка
		/// </summary>
		[NonSerialized]
		public Dictionary<string, Node> AstRoots = new Dictionary<string, Node>();

		/// <summary>
		/// Словарь парсеров, ключ - расширение файла, к которому парсер можно применить
		/// </summary>
		[NonSerialized]
		public Dictionary<string, BaseParser> Parsers = new Dictionary<string, BaseParser>();

		/// <summary>
		/// Контроль количества ссылок на файлы
		/// </summary>
		[NonSerialized]
		public Dictionary<string, int> Links = new Dictionary<string, int>();

		[NonSerialized]
		public List<Message> Log = new List<Message>();

		[NonSerialized]
		public GetTextDelegate GetText;
		public delegate string GetTextDelegate(string fileName);

		public void Clear()
		{
			Markup.Clear();
			AstRoots.Clear();
			Sources.Clear();
			Links.Clear();
			Log.Clear();
		}

		public void RemoveElement(MarkupElement elem)
		{
			if (elem.Parent != null)
				elem.Parent.Elements.Remove(elem);
			else
				Markup.Remove(elem);

			if (elem is ConcernPoint)
			{
				var point = (ConcernPoint)elem;

				Links[point.FileName] -= 1;

				if(Links[point.FileName] == 0)
				{
					Links.Remove(point.FileName);
					AstRoots.Remove(point.FileName);
					Sources.Remove(point.FileName);
				}
			}
		}

		public Concern AddConcern(string name, Concern parent = null)
		{
			var concern = new Concern(name, parent);
			AddElement(concern);
			return concern;
		}

		public ConcernPoint AddConcernPoint(string fileName, Node node, string name = null, Concern parent = null)
		{
			var point = new ConcernPoint(fileName, node, parent);
			AddElement(point);
			return point;
		}

		public void AddLand(string fileName)
		{
			if (!AstRoots.ContainsKey(fileName) && Parse(fileName, GetText(fileName)))
			{
				var visitor = new LandExplorerVisitor();
				AstRoots[fileName].Accept(visitor);

				/// Группируем land-сущности по типу (символу)
				foreach (var group in visitor.Land.GroupBy(l => l.Symbol))
				{
					var concern = AddConcern(group.Key);

					/// В пределах символа группируем по псевдониму
					var subgroups = group.GroupBy(g => g.Alias);

					/// Для всех точек, для которых указан псевдоним
					foreach (var subgroup in subgroups.Where(s => !String.IsNullOrEmpty(s.Key)))
					{
						/// создаём подфункциональность
						var subconcern = AddConcern(subgroup.Key, concern);

						foreach (var point in subgroup)
							AddElement(new ConcernPoint(fileName, point, subconcern));
					}

					/// Остальные добавляются напрямую к функциональности, соответствующей символу
					var points = subgroups.Where(s => String.IsNullOrEmpty(s.Key))
						.SelectMany(s => s).ToList();

					foreach (var point in points)
						AddElement(new ConcernPoint(fileName, point, concern));
				}
			}
		}

		public LinkedList<Node> GetConcernPointCandidates(string fileName, int offset)
		{
			if (AstRoots.ContainsKey(fileName) || Parse(fileName, GetText(fileName)))
			{
				var pointCandidates = new LinkedList<Node>();
				var currentNode = AstRoots[fileName];

				/// В качестве кандидатов на роль помечаемого участка рассматриваем узлы от корня,
				/// содержащие текущую позицию каретки
				while (currentNode != null)
				{
					if (currentNode.Options.IsLand)
						pointCandidates.AddFirst(currentNode);

					currentNode = currentNode.Children.Where(c => c.StartOffset.HasValue && c.EndOffset.HasValue
						&& c.StartOffset <= offset && c.EndOffset >= offset).FirstOrDefault();
				}

				return pointCandidates;
			}

			return new LinkedList<Node>();
		}

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
		}

		public void Remap(string fileName, Node newRoot, Dictionary<Node, Node> mapping)
		{
			AstRoots[fileName] = newRoot;

			var visitor = new RemapVisitor(mapping);
			for (int i = 0; i < Markup.Count; ++i)
			{
				Markup[i].Accept(visitor);
			}
		}

		public void Serialize(string fileName)
		{
			using (FileStream fs = new FileStream(fileName, FileMode.Create))
			{
				using (var gZipStream = new GZipStream(fs, CompressionLevel.Optimal))
				{
					BinaryFormatter serializer = new BinaryFormatter();
					serializer.Serialize(gZipStream, this);
				}
			}
		}

		public void Deserialize(string filename)
		{
			Clear();

			using (FileStream fs = new FileStream(filename, FileMode.Open))
			{
				using (var gZipStream = new GZipStream(fs, CompressionMode.Decompress))
				{
					BinaryFormatter serializer = new BinaryFormatter();
					var result = (MarkupManager)serializer.Deserialize(gZipStream);

					this.Sources = result.Sources;
					this.Markup = result.Markup;
				}
			}

			var visitor = new GroupPointsVisitor();

			foreach (var elem in this.Markup)
				elem.Accept(visitor);

			foreach(var pathGroup in visitor.Points)
			{
				Links[pathGroup.Key] = pathGroup.Value.Sum(v=>v.Value.Count);

				if(Parse(pathGroup.Key, Sources[pathGroup.Key]))
				{
					var recoveryVisitor = new LinkRecoveryVisitor(pathGroup.Value);
					recoveryVisitor.Visit(AstRoots[pathGroup.Key]);
				}
			}
		}

		private void AddElement(MarkupElement elem)
		{
			if (elem.Parent == null)
				Markup.Add(elem);
			else
				elem.Parent.Elements.Add(elem);

			if (elem is ConcernPoint)
			{
				var point = (ConcernPoint)elem;

				if (!Links.ContainsKey(point.FileName))
					Links[point.FileName] = 1;
				else
					Links[point.FileName] += 1;
			}
		}

		private bool Parse(string fileName, string text)
		{
			if (!String.IsNullOrEmpty(fileName))
			{
				var extension = Path.GetExtension(fileName);

				if (Parsers.ContainsKey(extension) && Parsers[extension] != null)
				{
					AstRoots[fileName] = Parsers[extension].Parse(text);
					Sources[fileName] = text;

					Log.AddRange(Parsers[extension].Log);

					return Parsers[extension].Log.All(l=>l.Type != MessageType.Error && l.Type != MessageType.Warning);
				}
				else
				{
					Log.Add(Message.Error($"Отсутствует парсер для файлов с расширением '{extension}'", null));
				}
			}

			return false;
		}
	}
}
