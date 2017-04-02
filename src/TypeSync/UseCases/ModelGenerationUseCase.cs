﻿using System;
using System.IO;
using System.Linq;
using log4net;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.MSBuild;
using TypeSync.Core;
using TypeSync.Core.Analyzers;
using TypeSync.Core.SyntaxRewriters;
using TypeSync.Models;
using TypeSync.Output;
using TypeSync.Output.Converters;
using TypeSync.Output.Generators;
using TypeSync.Providers;

namespace TypeSync.UseCases
{
    public class ModelGenerationUseCase : IUseCase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ModelGenerationUseCase));

        private readonly IConfigurationProvider _configurationProvider;

        private Configuration _configuration;

        public string Id { get; } = "GenerateModels";

        public string Name { get; } = "Generate TypeScript model classes from C# DTO objects.";

        public ModelGenerationUseCase(IConfigurationProvider configurationProvider)
        {
            _configurationProvider = configurationProvider;
        }

        public void Execute()
        {
            _configuration = _configurationProvider.GetConfiguration();

            switch (_configuration.PathKind)
            {
                case PathKind.File:
                    ExecuteOnFile();
                    break;
                case PathKind.Project:
                    ExecuteOnProject();
                    break;
                case PathKind.Solution:
                    ExecuteOnSolution();
                    break;
                default:
                    break;
            }
        }

        private void ExecuteOnFile()
        {
            // parse the syntax tree from a .cs file
            var viewModelText = File.ReadAllText(_configuration.Path);

            var tree = CSharpSyntaxTree.ParseText(viewModelText).WithFilePath(_configuration.Path);

            // check for any syntax errors
            var errors = tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

            if (errors.Any())
            {
                log.Warn("Syntax contains errors: ");

                foreach (var error in errors)
                {
                    log.Warn(error.ToString());
                }
            }

            var root = tree.GetRoot();

            var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
            var compilation = CSharpCompilation.Create("MyCompilation", syntaxTrees: new[] { tree }, references: new[] { mscorlib });

            // note that we must specify the tree for which we want the model.
            // each tree has its own semantic model
            var semanticModel = compilation.GetSemanticModel(tree);

            var classSyntax = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
            var classSymbol = semanticModel.GetDeclaredSymbol(classSyntax) as INamedTypeSymbol;

            // rewrite System types with aliases
            var aliasRewriter = new PropertyTypeAliasRewriter();
            var rewriteResult = aliasRewriter.Visit(root);

            if (root != rewriteResult)
            {
                root = rewriteResult;

                File.WriteAllText(_configuration.Path, root.ToFullString());
                log.Debug("Some property types were replaced with aliases");
            }

            ProccessSemanticModel(semanticModel);
        }

        private void ExecuteOnProject()
        {
            throw new NotImplementedException();
        }

        private void ExecuteOnSolution()
        {
            var workspace = MSBuildWorkspace.Create();
            var solution = workspace.OpenSolutionAsync(_configuration.Path).Result;

            foreach (var project in solution.Projects)
            {
                Console.WriteLine(project.Name);

                var compilation = project.GetCompilationAsync().Result;
                var syntaxTrees = compilation.SyntaxTrees;

                foreach (var syntaxTree in syntaxTrees)
                {
                    if (syntaxTree.ToString().Contains("auto-generated") || syntaxTree.ToString().Contains("autogenerated"))
                    {
                        continue;
                    }

                    var semanticModel = compilation.GetSemanticModel(syntaxTree);

                    ProccessSemanticModel(semanticModel);
                }

                //foreach (var document in project.Documents)
                //{
                //    var syntaxTree = document.GetSyntaxTreeAsync().Result;
                //    var semanticModel = compilation.GetSemanticModel(syntaxTree);

                //    var classSyntax = syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
                //    var classSymbol = semanticModel.GetDeclaredSymbol(classSyntax) as INamedTypeSymbol;
                //    var referencesToClass = SymbolFinder.FindReferencesAsync(classSymbol, solution).Result;

                //    var descendantNodes = classSyntax.DescendantNodes().ToList();
                //    var typesForCurrentClass = descendantNodes.Select(n => semanticModel.GetTypeInfo(n).Type).ToList();

                //    Console.WriteLine(document.Name);
                //}
            }
        }

        private void ProccessSemanticModel(SemanticModel semanticModel)
        {        
            var analyzer = new DTOAnalyzer(semanticModel);
            var converter = new ModelConverter();
            var emitter = new TypeScriptEmitter();
            var generator = new ModelGenerator();

            var classModels = analyzer.AnalyzeDTOs();

            foreach (var classModel in classModels)
            {
                log.DebugFormat("Class {0}", classModel.Name);

                var tsModel = converter.ConvertModel(classModel);

                log.Debug("Models converted");

                var contents = generator.Generate(tsModel);

                log.Debug("Contents generated");

                emitter.Emit(_configuration.OutputPath, classModel.Name, contents);

                log.Debug("Contents emitted");
            }
        }
    }
}