using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

[Generator]
public class RemoveWcfAttributesGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        // Rejestrujemy analizator składni
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var syntaxReceiver = (SyntaxReceiver)context.SyntaxReceiver!;
        foreach (var classDecl in syntaxReceiver.CandidateClasses)
        {
            var model = context.Compilation.GetSemanticModel(classDecl.SyntaxTree);

            var classSymbol = model.GetDeclaredSymbol(classDecl);
            if (classSymbol == null)
                continue;

            var sourceBuilder = new StringBuilder();

            // Namespace
            var ns = classSymbol.ContainingNamespace.ToDisplayString();
            if (!string.IsNullOrEmpty(ns))
            {
                sourceBuilder.AppendLine($"namespace {ns}");
                sourceBuilder.AppendLine("{");
            }

            // Modyfikatory klasy
            var modifiers = classDecl.Modifiers.ToFullString();

            // Nazwa klasy
            var className = classDecl.Identifier.Text;

            // Zbierz deklaracje członków bez atrybutów WCF
            var membersWithoutWcfAttrs = classDecl.Members.Select(m =>
            {
                // Filtrujemy atrybuty
                var attrLists = m.GetAttributes().Where(al =>
                    !al.DescendantTokens().Any(t => 
                        t.Text.StartsWith("ServiceContract") ||
                        t.Text.StartsWith("OperationContract") ||
                        t.Text.StartsWith("DataContract") ||
                        t.Text.StartsWith("DataMember") ||
                        t.Text.StartsWith("WebGet") ||
                        t.Text.StartsWith("WebInvoke")
                    )
                );
                return m.NormalizeWhitespace().ToFullString(); // W uproszczeniu bez filtrowania atrybutów szczegółowo
            });

            // Prostsze podejście: kopiujemy całą klasę tylko bez atrybutów na poziomie klasy (pełna implementacja wymaga parsowania atrybutów na poziomie metody i property)

            // Tymczasowo usuwamy atrybuty klasy i generujemy klasę bez atrybutów

            sourceBuilder.AppendLine($"{modifiers} class {className}");
            sourceBuilder.AppendLine("{");
            foreach(var member in classDecl.Members)
            {
                // Usuwamy atrybuty na poziomie członka
                var memberText = member.NormalizeWhitespace().ToFullString();
                // Proste usunięcie atrybutów na poziomie tekstu:
                var noAttr = System.Text.RegularExpressions.Regex.Replace(memberText, @"\[\s*(ServiceContract|OperationContract|DataContract|DataMember|WebGet|WebInvoke)[^\]]*\]", "");
                sourceBuilder.AppendLine(noAttr);
            }
            sourceBuilder.AppendLine("}");

            if (!string.IsNullOrEmpty(ns))
            {
                sourceBuilder.AppendLine("}");
            }

            // Dodajemy wygenerowany kod do kompilacji
            context.AddSource($"{className}_NoWcfAttributes.g.cs", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
        }
    }

    class SyntaxReceiver : ISyntaxReceiver
    {
        public List<ClassDeclarationSyntax> CandidateClasses { get; } = new();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            // Szukamy klas z atrybutami WCF (lub po prostu wszystkich klas jeśli chcemy)
            if (syntaxNode is ClassDeclarationSyntax cds &&
                cds.AttributeLists.Count > 0)
            {
                CandidateClasses.Add(cds);
            }
        }
    }
}
