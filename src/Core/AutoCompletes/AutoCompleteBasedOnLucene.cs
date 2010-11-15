﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using Core.Abstractions;
using Core.Converters;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Directory = Lucene.Net.Store.Directory;
using Version = Lucene.Net.Util.Version;

namespace Core.AutoCompletes
{
    [Export(typeof(IAutoCompleteText))]
    public class AutoCompleteBasedOnLucene : IAutoCompleteText
    {
        private ShortcutFinder _finder;
        private Directory _directory;

        private Hashtable _stopwords;

        [ImportMany(typeof(IConverter))]
        public IEnumerable<IConverter> Converters { get; set; }

        public AutoCompleteBasedOnLucene()
        {
            _stopwords = new Hashtable(ShortcutFinder._extensions.ToDictionary(s => s, s => s));
            
            var indexDirectory = new DirectoryInfo(Path.Combine(new FileInfo(Assembly.GetExecutingAssembly().FullName).DirectoryName, "index"));
            var createIndex = !indexDirectory.Exists;
            _directory = new SimpleFSDirectory(indexDirectory);
            new IndexWriter(_directory, new StandardAnalyzer(Version.LUCENE_29), createIndex, IndexWriter.MaxFieldLength.UNLIMITED).Close();

            _finder = new ShortcutFinder(files =>
                                             {
                                                 if(files.Count() == 0) return;

                                                 var indexWriter = new IndexWriter(_directory, new StandardAnalyzer(Version.LUCENE_29, _stopwords), mfl: IndexWriter.MaxFieldLength.UNLIMITED);
                                                 var host = new ConverterHost(Converters);
                                                 try
                                                 {
                                                     foreach (var fileInfo in files)
                                                     {
                                                         host.UpdateDocumentForItem(indexWriter, fileInfo);
                                                     }
                                                     indexWriter.Commit();
                                                 }
                                                 finally
                                                 {
                                                     indexWriter.Close();
                                                 }
                                             });
        }

        public AutoCompletionResult Autocomplete(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return AutoCompletionResult.NoResult(text);

            var searcher = new IndexSearcher(_directory, true);
            try
            {
                QueryParser queryParser = new QueryParser(Version.LUCENE_29, "filename",
                                                          new StandardAnalyzer(Version.LUCENE_29, _stopwords));
                var converterHost = new ConverterHost(Converters);

                queryParser.SetDefaultOperator(QueryParser.Operator.AND);
                var results = searcher.Search(queryParser.Parse(text), 10);
                var commands = results.scoreDocs
                    .Select(d => converterHost.GetCommandForDocument(searcher.Doc(d.doc)));
                return AutoCompletionResult.OrderedResult(text, commands);
            }
            catch (ParseException e)
            {
                return AutoCompletionResult.SingleResult(text, new TextCommand(text, "Error parsing input: " + e.Message));
            }
            
        }
    }
}