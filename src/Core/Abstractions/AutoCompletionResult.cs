﻿using System.Collections.Generic;
using System.Linq;

namespace Core.Abstractions
{
    public class AutoCompletionResult
    {
        public bool HasAutoCompletion
        {
            get { return AutoCompletedCommand != null; }
        }

        public string OriginalText { get; private set; }
        public ICommand AutoCompletedCommand { get; private set; }
        public IEnumerable<ICommand> OtherOptions { get; private set; }

        protected AutoCompletionResult()
        {
            OtherOptions = new ICommand[]{};
        }

        public static AutoCompletionResult NoResult(string originalText)
        {
            return new AutoCompletionResult(){OriginalText = originalText, AutoCompletedCommand = null};
        }
        
        public static AutoCompletionResult OrderedResult(string originalText, IEnumerable<ICommand> result)
        {
            return new AutoCompletionResult(){OriginalText = originalText, AutoCompletedCommand = result.First(), OtherOptions = result.Skip(1)};
        }
    }
}