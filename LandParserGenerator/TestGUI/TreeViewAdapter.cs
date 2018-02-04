using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

using LandParserGenerator.Parsing.Tree;
using LandParserGenerator.Mapping;


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

			foreach (var nd in node.Children)
				treeItem.Items.Add((TreeViewAdapter)nd);

			return treeItem;

		}

		public static explicit operator TreeViewAdapter(ConcernPoint point)
		{
			var treeItem = new TreeViewAdapter();
			treeItem.Source = point.TreeNode;
			treeItem.Header = !String.IsNullOrEmpty(point.Name) ? point.Name : 
				point.TreeNode.Symbol + (point.TreeNode.Value.Count > 0 ? ": " + String.Join(" ", point.TreeNode.Value) : "");

			return treeItem;
		}

		public static explicit operator TreeViewAdapter(KeyValuePair<Node, Dictionary<Node, double>> elem)
		{
			var treeItem = new TreeViewAdapter();
			treeItem.Source = elem.Key;
			treeItem.Header = elem.Key.Symbol + (elem.Key.Value.Count > 0 ? ": " + String.Join(" ", elem.Key.Value) : "");

			foreach (var kvp in elem.Value)
				treeItem.Items.Add((TreeViewAdapter)kvp);

			return treeItem;
		}

		public static explicit operator TreeViewAdapter(KeyValuePair<Node, double> elem)
		{
			var treeItem = new TreeViewAdapter();
			treeItem.Source = elem.Key;
			treeItem.Header = elem.Value + " " + elem.Key.Symbol + (elem.Key.Value.Count > 0 ? ": " + String.Join(" ", elem.Key.Value) : "");

			return treeItem;
		}
	}
}
