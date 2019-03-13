using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;

using Land.Core.Parsing;
using Land.Core;

namespace Land.Control
{
	public enum CacheActionType { Delete, Copy }

	public enum CacheActionLibraryType { Parser, Preprocessor }

	public class CacheAction
	{
		public CacheActionType ActionType { get; set; }

		public string SourcePath { get; set; }

		public string TargetPath { get; set; }

		public bool? Success { get; set; }
	}

	public class RefreshParserCacheResponse
	{
		public string ParserCachedPath { get; set; }

		public string PreprocessorCachedPath { get; set; }

		public bool NoErrors { get; set; }

		public bool CacheRefreshed { get; set; }
	}
}
