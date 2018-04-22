using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;

namespace csharp2json
{
    public class RoslynHelper
    {
        /// <summary>
        /// Compiles C# code and creates instances of object types
        /// </summary>
        /// <param name="csharp">C# code text</param>
        /// <returns>Collection of object instances</returns>
        public static IEnumerable<object> CompileClasses(string csharp)
        {
            SyntaxTree tree = SyntaxTree.ParseText(csharp);
            CompilationUnitSyntax root = tree.GetRoot();
        
            // add Using statements to syntax tree
            var system = Syntax.IdentifierName("System");
            var systemCollections = Syntax.QualifiedName(system, Syntax.IdentifierName("Collections"));
            var systemCollectionsGeneric = Syntax.QualifiedName(systemCollections, Syntax.IdentifierName("Generic"));
            var systemText = Syntax.QualifiedName(system, Syntax.IdentifierName("Text"));
            var systemLinq = Syntax.QualifiedName(system, Syntax.IdentifierName("Linq"));

            var declaredUsings = root.Usings.Select(x => x.Name.ToString()).ToList();
            if (!declaredUsings.Contains("System"))
            {
                root = root.AddUsings(Syntax.UsingDirective(system).NormalizeWhitespace());
            }
            if (!declaredUsings.Contains("System.Collections"))
            {
                root = root.AddUsings(Syntax.UsingDirective(systemCollections).NormalizeWhitespace());
            }
            if (!declaredUsings.Contains("System.Collections.Generic"))
            {
                root = root.AddUsings(Syntax.UsingDirective(systemCollectionsGeneric).NormalizeWhitespace());
            }
            if (!declaredUsings.Contains("System.Text"))
            {
                root = root.AddUsings(Syntax.UsingDirective(systemLinq).NormalizeWhitespace());
            }
            if (!declaredUsings.Contains("System.Linq"))
            {
                root = root.AddUsings(Syntax.UsingDirective(systemText).NormalizeWhitespace());
            }

            tree = SyntaxTree.Create(root);
            root = tree.GetRoot();

            // generate compiled object with references to commonly used .NET Framework assemblies
            var compilation = Compilation.Create("CSharp2Json",
                syntaxTrees: new[] {tree},
                references: new[]
                {
                    new MetadataFileReference(typeof(object).Assembly.Location),      // mscorelib.dll
                    new MetadataFileReference(typeof(Enumerable).Assembly.Location),  // System.Core.dll
                    new MetadataFileReference(typeof(Uri).Assembly.Location),         // System.dll
                    new MetadataFileReference(typeof(DataSet).Assembly.Location),     // System.Data.dll
                    new MetadataFileReference(typeof(EntityKey).Assembly.Location),   // System.Data.Entity.dll
                    new MetadataFileReference(typeof(XmlDocument).Assembly.Location), // System.Xml.dll
                },
                options: new CompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            // load compiled bits into assembly
            Assembly assembly;
            using (var memoryStream = new MemoryStream())
            {
                var result = compilation.Emit(memoryStream);
                if (!result.Success)
                {
                    throw new RoslynException(result.Diagnostics);
                }

                assembly = AppDomain.CurrentDomain.Load(memoryStream.ToArray());
            }

            // instantiate object instances from assembly types
            foreach (var definedType in assembly.DefinedTypes)
            {
                Type objType = assembly.GetType(definedType.FullName);
                if (objType.BaseType?.FullName != "System.Enum")
                {
                    object instance = null;
                    try
                    {
                        instance = assembly.CreateInstance(definedType.FullName);
                    }
                    catch (MissingMethodException)
                    {
                        // no default constructor - eat the exception
                    }

                    if (instance != null)
                    {
                        yield return instance;
                    }
                }
            }
        }
    }
}