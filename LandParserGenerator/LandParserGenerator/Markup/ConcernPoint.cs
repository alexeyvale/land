using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Land.Core.Parsing.Tree;

namespace Land.Core.Markup
{
	[Serializable]
	public class ConcernPoint: MarkupElement
	{
		[NonSerialized]
		private Node _treeNode = null;

		public Node TreeNode
		{
			get { return _treeNode; }
			set { _treeNode = value; TreeNodeId = value?.Id; }
		}

		public int? TreeNodeId { get; set; }

		public string FileName { get; set; }

		public ConcernPoint(string fileName, Node node, Concern parent = null)
		{
			FileName = fileName;
			TreeNode = node;
			Parent = parent;
			Name = String.IsNullOrEmpty(node.Alias) ? node.Symbol : node.Alias;

			if (node.Value.Count > 0)
				Name += ": " + String.Join(" ", node.Value);
			else
			{
				if (node.Children.Count > 0)
				{
					Name += ": " + String.Join(" ", node.Children.SelectMany(c => c.Value.Count > 0 ? c.Value
						: new List<string>() { '"' + (String.IsNullOrEmpty(c.Alias) ? c.Symbol : c.Alias) + '"' }));
				}
			}
		}

		public ConcernPoint(string name, string fileName, Node node, Concern parent = null)
		{
			Name = name;
			FileName = fileName;
			TreeNode = node;
			Parent = parent;
		}
	}
}
