using System;
using System.Collections.Generic;

using Land.Core.Parsing.Tree;

namespace Land.Core.Parsing.Preprocessing
{
	public abstract class BasePreprocessor
	{
		public virtual List<Message> Log { get; set; } = new List<Message>();

		public abstract string Preprocess(string text, out bool success);

		public abstract void Postprocess(Node root, List<Message> log);

		public PreprocessorSettings Properties { get; set; }
	}

    public abstract class PreprocessorSettings : ICloneable
    {
        public abstract object Clone();
    }
}
