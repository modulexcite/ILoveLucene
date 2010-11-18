using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using Core.Abstractions;
using Microsoft.Win32;

namespace Core.Commands
{
    [Export(typeof(ICommand))]
    public class Putty : ICommandWithAutoCompletedArguments
    {
        private string[] _sessionNames;

        public Putty()
        {
            var sessionsKey = Registry.CurrentUser.OpenSubKey(@"Software\SimonTatham\PuTTY\Sessions");
            if(sessionsKey == null) _sessionNames = new string[]{};
            else _sessionNames = sessionsKey.GetSubKeyNames();
        }

        public string Text
        {
            get { return "Putty"; }
        }

        public string Description
        {
            get { return "Putty launcher"; }
        }

        public void Execute()
        {
            Process.Start("putty");
        }

        public void Execute(string arguments)
        {
            if (_sessionNames.Contains(arguments))
            {
                arguments = "-load " + arguments;
            }
            Process.Start("putty", arguments);
        }

        public ArgumentAutoCompletionResult AutoCompleteArguments(string arguments)
        {
            return ArgumentAutoCompletionResult.OrderedResult(arguments,
                                                              _sessionNames.Where(s => s.Contains(arguments)));
        }
    }
}