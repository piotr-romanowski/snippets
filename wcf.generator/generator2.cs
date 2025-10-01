using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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

            var root = declaration.SyntaxTree.GetRoot();

            string usingsText = GetUsingsWithDirectives(root);

            string namespaceName = symbol.ContainingNamespace?.ToDisplayString() ?? "";
            string originalTypeName = declaration.Identifier.Text;
            string generatedTypeName = originalTypeName + "Clean";
            string generatedNamespace = string.IsNullOrEmpty(namespaceName) ? "CleanNamespace" : namespaceName + ".Clean";

            var sourceBuilder = new StringBuilder();

            sourceBuilder.AppendLine(usingsText);
            sourceBuilder.AppendLine();

            sourceBuilder.AppendLine($"namespace {generatedNamespace}");
            sourceBuilder.AppendLine("{");

            string modifiers = declaration.Modifiers.ToFullString().Trim();
            string kind = declaration.Kind() == Microsoft.CodeAnalysis.CSharp.SyntaxKind.ClassDeclaration ? "class" : "interface";

            sourceBuilder.AppendLine($"{modifiers} {kind} {generatedTypeName}");
            sourceBuilder.AppendLine("{");

            IEnumerable<MemberDeclarationSyntax> members = null;

            if (declaration is ClassDeclarationSyntax classDecl)
                members = classDecl.Members;
            else if (declaration is InterfaceDeclarationSyntax interfaceDecl)
                members = interfaceDecl.Members;

            if (members != null)
            {
                foreach (var member in members)
                {
                    string memberText = member.NormalizeWhitespace().ToFullString();
                    string cleanedText = RemoveWcfAttributesFromText(memberText);
                    sourceBuilder.AppendLine(cleanedText);
                    sourceBuilder.AppendLine();
                }
            }

            sourceBuilder.AppendLine("}");
            sourceBuilder.AppendLine("}");

            context.AddSource($"{generatedTypeName}.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
        }
    }

    private static string GetUsingsWithDirectives(SyntaxNode root)
    {
        var sourceText = new StringBuilder();

        var directiveGroups = root.DescendantTrivia()
            .Where(t => t.IsDirective)
            .GroupBy(t => t.Token.Parent, (key, group) => new { Key = key, Trivia = group.ToList() });

        foreach (var group in directiveGroups)
        {
            if (group.Key == null)
                continue;

            var usingNodes = group.Key.DescendantNodesAndSelf().OfType<UsingDirectiveSyntax>();
            if (!usingNodes.Any())
                continue;

            if (usingNodes.Any(u =>
            {
                var ns = u.Name.ToString();
                return ns.StartsWith("System.ServiceModel") || ns.StartsWith("CoreWCF");
            }))
            {
                // Pomijamy cały blok z wykluczonymi usingami
                continue;
            }

            sourceText.AppendLine(group.Key.ToFullString());
        }

        var rootUsings = root.DescendantNodes()
            .OfType<UsingDirectiveSyntax>()
            .Where(u => u.Parent is CompilationUnitSyntax)
            .Where(u =>
            {
                var ns = u.Name.ToString();
                return !ns.StartsWith("System.ServiceModel") && !ns.StartsWith("CoreWCF");
            });

        foreach (var u in rootUsings)
        {
            sourceText.AppendLine(u.ToFullString());
        }

        return sourceText.ToString();
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
