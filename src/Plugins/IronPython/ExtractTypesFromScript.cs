using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.API;
using Core.Abstractions;
using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Types;
using Microsoft.CSharp.RuntimeBinder;
using Microsoft.Scripting.Hosting;
using Plugins.Commands;

namespace Plugins.IronPython
{
    public class ExtractTypesFromScript
    {
        private readonly ScriptEngine _engine;

        public ExtractTypesFromScript(ScriptEngine engine)
        {
            _engine = engine;
        }

        public IEnumerable<IronPythonComposablePart> GetPartsFromScript(ScriptSource script)
        {
            return GetParts(GetTypesFromScript(script));
        }

        public IEnumerable<IronPythonTypeWrapper> GetTypesFromScript(ScriptSource script)
        {
            CompiledCode code = script.Compile();
            var scope = _engine.CreateScope();

            var types = new[]
                            {
                                typeof (IItem), typeof (IConverter<>), typeof (BaseActOnTypedItem<>),
                                typeof (BaseActOnTypedItemAndReturnTypedItem<,>),
                                typeof (IItemSource),
                                typeof (BaseItemSource),
                                typeof (IConverter),
                                typeof (ICommand),
                                typeof (IRequest),
                                typeof (IActOnItem),
                                typeof (IActOnItemWithArguments),
                                typeof (BasePythonItemSource),
                                typeof (IronPythonImportDefinition)
                            };
            foreach (Type type in types)
            {
                scope.InjectType(type);
            }
            using (var libStream = GetType().Assembly.GetManifestResourceStream(GetType(), "lib.py"))
            using (var libText = new StreamReader(libStream))
            {
                var libSource = _engine.CreateScriptSourceFromString(libText.ReadToEnd());
                libSource.Execute(scope);
            }


            // "force" all classes to be new style classes
            dynamic metaclass;
            if(!scope.TryGetVariable("__metaclass__", out metaclass))
            {
                scope.SetVariable("__metaclass__", _engine.GetBuiltinModule().GetVariable("type"));
            }
            
            scope.SetVariable("clr", _engine.GetClrModule());
            code.Execute(scope);

            var pluginClasses = scope.GetItems()
                .Where(kvp => kvp.Value is PythonType && !kvp.Key.StartsWith("__"))
                .Select(kvp => new IronPythonTypeWrapper(_engine, kvp.Key, kvp.Value, scope.GetVariableHandle(kvp.Key)))
                .Where(kvp => !types.Contains(kvp.Type));

            return pluginClasses;
        }
        public IEnumerable<IronPythonComposablePart> GetParts(IEnumerable<IronPythonTypeWrapper> types)
        {
            foreach (var definedType in types)
            {
                dynamic type = definedType.PythonType;
                IEnumerable<object> exportObjects = new List();
                IDictionary<string, object> importObjects = new Dictionary<string, object>();
                PythonDictionary pImportObjects = null;

                try
                {
                    exportObjects = ((IEnumerable<object>)type.__exports__);
                }
                catch (RuntimeBinderException)
                {
                }
                try
                {
                    foreach (var callable in ((IEnumerable)_engine.Operations.GetMemberNames(type)).Cast<string>()
                        .Select(m => new {name = m, member = _engine.Operations.GetMember(type, m)})
                        .Where(d => d.member != null)
                        .Where(d => _engine.Operations.IsCallable(d.member)))
                    {
                        try
                        {
                            if(callable.member.im_func.func_dict.has_key("imports"))
                            {
                                var import = callable.member.im_func.func_dict["imports"];
                                importObjects.Add(callable.name, import);
                            }
                        }catch(RuntimeBinderException){}
                    }
                }
                catch (RuntimeBinderException)
                {
                }

                if (importObjects.Count + exportObjects.Count() == 0)
                {
                    continue;
                }

                var exports = exportObjects.Cast<PythonType>().Select(o => (Type)o);
                var imports =
                    importObjects.Keys
                        .Select(key => new KeyValuePair<string, IronPythonImportDefinition>(key, (IronPythonImportDefinition)importObjects[key]));
                yield return new IronPythonComposablePart(definedType, exports, imports);
            }
        }
    }
}