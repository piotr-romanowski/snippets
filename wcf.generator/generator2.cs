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

            // Filtrowanie usingów - wyłączamy namespace powiązane z WCF
            var usings = root.DescendantNodes()
                             .OfType<UsingDirectiveSyntax>()
                             .Where(u =>
                                !u.Name.ToString().StartsWith("System.ServiceModel") &&
                                !u.Name.ToString().StartsWith("System.Runtime.Serialization")
                             );

            string namespaceName = symbol.ContainingNamespace?.ToDisplayString() ?? "";
            string originalTypeName = declaration.Identifier.Text;
            string generatedTypeName = originalTypeName + "Clean";
            string generatedNamespace = string.IsNullOrEmpty(namespaceName) ? "CleanNamespace" : namespaceName + ".Clean";

            var sourceBuilder = new StringBuilder();

            // Dodajemy filt
