using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	[Serializable]
	public class MarkupManager
	{
		public ObservableCollection<MarkupElement> Markup = new ObservableCollection<MarkupElement>();

		[NonSerialized]
		public Dictionary<string, Node> AstRoots = new Dictionary<string, Node>();

		public Dictionary<string, string> AstSources = new Dictionary<string, string>();

		public void Clear()
		{
			Markup.Clear();
			AstRoots.Clear();
		}

		public void Remove(MarkupElement elem)
		{
			if (elem.Parent != null)
				elem.Parent.Elements.Remove(elem);
			else
				Markup.Remove(elem);
		}

		public void Add(MarkupElement elem)
		{
			if (elem.Parent == null)
				Markup.Add(elem);
			else
				elem.Parent.Elements.Add(elem);
		}

		public void Remap(string fileName, Node newRoot, Dictionary<Node, Node> mapping)
		{
			AstRoots[fileName] = newRoot;

			var visitor = new MarkupRemapVisitor(mapping);
			for (int i = 0; i < Markup.Count; ++i)
			{
				if (!visitor.Visit(Markup[i]))
				{
					Markup.RemoveAt(i);
					--i;
				}
			}
		}

		public static void Serialize(string filename, MarkupManager target)
		{
			using (FileStream fs = new FileStream(filename, FileMode.Create))
			{
				using (var gZipStream = new GZipStream(fs, CompressionLevel.Optimal))
				{
					BinaryFormatter serializer = new BinaryFormatter();
					serializer.Serialize(gZipStream, target);
				}
			}
		}

		public static MarkupManager Deserialize(string filename)
		{
			/// Здесь нужно построить деревья и связать дерево с разметкой

			using (FileStream fs = new FileStream(filename, FileMode.Open))
			{
				using (var gZipStream = new GZipStream(fs, CompressionMode.Decompress))
				{
					BinaryFormatter serializer = new BinaryFormatter();
					return (MarkupManager)serializer.Deserialize(gZipStream);
				}
			}
		}
	}

	public class MarkupRemapVisitor
	{
		public Dictionary<Node, Node> Mapping;

		public MarkupRemapVisitor(Dictionary<Node, Node> mapping)
		{
			Mapping = mapping;
		}

		public bool Visit(MarkupElement element)
		{
			var result = true;

			if (element is Concern)
			{
				var concern = (Concern)element;

				for(var i=0;i<concern.Elements.Count;++i)
					if(!Visit(concern.Elements[i]))
					{
						concern.Elements.RemoveAt(i);
						--i;
					}
			}
			else
			{
				var concernPoint = (ConcernPoint)element;

				if (Mapping.ContainsKey(concernPoint.TreeNode))
					concernPoint.TreeNode = Mapping[concernPoint.TreeNode];
				else
					result = false;
			}

			return result;
		}
	}
}
