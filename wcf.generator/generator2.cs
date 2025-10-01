using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace RemoveWcfAttributesGenerator
{
    [Generator]
    public class RemoveWcfAttributesGenerator : ISourceGenerator
    {
        private static readonly string[] WcfAttributes = new[]
        {
            "ServiceContract",
            "OperationContract",
            "DataContract",
            "DataMember",
            "WebGet",
            "WebInvoke"
        };

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;

            foreach (var declaration in receiver.CandidateTypes)
            {
                var model = context.Compilation.GetSemanticModel(declaration.SyntaxTree);
                var symbol = model.GetDeclaredSymbol(declaration);
                if (symbol == null) continue;

                string namespaceName = symbol.ContainingNamespace?.ToDisplayString() ?? "";
                string originalTypeName = declaration.Identifier.Text;
                string generatedTypeName = originalTypeName + "Clean";
                string generatedNamespace = string.IsNullOrEmpty(namespaceName) ? "CleanNamespace" : namespaceName + ".Clean";

                var sourceBuilder = new StringBuilder();

                sourceBuilder.AppendLine($"namespace {generatedNamespace}");
                sourceBuilder.AppendLine("{");

                string modifiers = declaration.Modifiers.ToFullString().Trim();
                string kind = declaration.Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.ClassDeclaration ? "class" : "interface";

                sourceBuilder.AppendLine($"{modifiers} {kind} {generatedTypeName}");
                sourceBuilder.AppendLine("{");

                foreach (var member in declaration.Members)
                {
                    string memberText = member.NormalizeWhitespace().ToFullString();
                    string cleanedText = RemoveWcfAttributesFromText(memberText);
                    sourceBuilder.AppendLine(cleanedText);
                    sourceBuilder.AppendLine();
                }

                sourceBuilder.AppendLine("}");
                sourceBuilder.AppendLine("}");

                context.AddSource($"{generatedTypeName}.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
            }
        }

        private static string RemoveWcfAttributesFromText(string text)
        {
            foreach (string attr in WcfAttributes)
            {
                string pattern = $@"\[\s*{attr}(\([^)]*\))?\s*\]\s*";
                text = Regex.Replace(text, pattern, string.Empty, RegexOptions.Singleline);
            }
            return text;
        }

        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<BaseTypeDeclarationSyntax> CandidateTypes { get; } = new();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is ClassDeclarationSyntax cds && HasWcfAttributes(cds.AttributeLists))
                    CandidateTypes.Add(cds);
                else if (syntaxNode is InterfaceDeclarationSyntax ids && HasWcfAttributes(ids.AttributeLists))
                    CandidateTypes.Add(ids);
            }

            private static bool HasWcfAttributes(SyntaxList<AttributeListSyntax> attributeLists)
            {
                foreach (var attrList in attributeLists)
                {
                    foreach (var attr in attrList.Attributes)
                    {
                        string name = attr.Name.ToString();
                        name = name.EndsWith("Attribute") ? name.Substring(0, name.Length - 9) : name;
                        if (WcfAttributes.Contains(name))
                            return true;
                    }
                }
                return false;
            }
        }
    }
}
