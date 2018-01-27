using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

using LandParserGenerator.Parsing.Tree;

namespace TestGUI
{
    public class TreeViewAdapter : TreeViewItem
    {
        public Node Source { get; private set; }

		public static explicit operator TreeViewAdapter(Node node)
		{
			var treeItem = new TreeViewAdapter();
			treeItem.Source = node;
			treeItem.Header = node.Symbol + (node.Value.Count > 0 ? ": " + String.Join(" ", node.Value) : "");
			var items = new List<TreeViewItem>();
			treeItem.ItemsSource = items;

			foreach (var nd in node.Children)
				items.Add((TreeViewAdapter)nd);

			return treeItem;

		}
    }
}
