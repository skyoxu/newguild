using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Game.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NoOneLetterLocalVariableAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "NG0001";

    private static readonly LocalizableString Title =
        "Local variable name is a single character";

    private static readonly LocalizableString MessageFormat =
        "Local variable '{0}' is a single character; use a meaningful name (allowed: i, j)";

    private static readonly LocalizableString Description =
        "One-letter local variables reduce readability and make LLM- and human-assisted reviews error-prone.";

    private const string Category = "Naming";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    private static readonly ImmutableHashSet<string> AllowedOneLetterNames =
        ImmutableHashSet.Create(StringComparer.Ordinal, "i", "j");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

        context.RegisterSyntaxNodeAction(AnalyzeVariableDeclarator, SyntaxKind.VariableDeclarator);
        context.RegisterSyntaxNodeAction(AnalyzeForEachStatement, SyntaxKind.ForEachStatement);
        context.RegisterSyntaxNodeAction(AnalyzeSingleVariableDesignation, SyntaxKind.SingleVariableDesignation);
    }

    private static void AnalyzeVariableDeclarator(SyntaxNodeAnalysisContext context)
    {
        var declarator = (VariableDeclaratorSyntax)context.Node;

        var name = declarator.Identifier.ValueText;
        if (!IsForbiddenOneLetterName(name))
            return;

        // Exclude fields, events, parameters, etc. Only local/loop variables are in scope.
        if (declarator.Ancestors().Any(a =>
                a is FieldDeclarationSyntax ||
                a is EventFieldDeclarationSyntax))
        {
            return;
        }

        // Include locals and loop initializers (for/using/fixed/etc.).
        if (declarator.Parent is not VariableDeclarationSyntax declaration)
            return;

        if (declaration.Parent is not (LocalDeclarationStatementSyntax or ForStatementSyntax or UsingStatementSyntax or FixedStatementSyntax))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, declarator.Identifier.GetLocation(), name));
    }

    private static void AnalyzeForEachStatement(SyntaxNodeAnalysisContext context)
    {
        var stmt = (ForEachStatementSyntax)context.Node;
        var name = stmt.Identifier.ValueText;
        if (!IsForbiddenOneLetterName(name))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, stmt.Identifier.GetLocation(), name));
    }

    private static void AnalyzeSingleVariableDesignation(SyntaxNodeAnalysisContext context)
    {
        var designation = (SingleVariableDesignationSyntax)context.Node;
        var name = designation.Identifier.ValueText;
        if (string.Equals(name, "_", StringComparison.Ordinal) &&
            context.SemanticModel.GetDeclaredSymbol(designation, context.CancellationToken) is IDiscardSymbol)
        {
            return;
        }

        if (!IsForbiddenOneLetterName(name))
            return;

        // Only variables introduced by patterns / deconstruction, not method parameters.
        // Declaration patterns and deconstruction designations are treated as locals by C#.
        context.ReportDiagnostic(Diagnostic.Create(Rule, designation.Identifier.GetLocation(), name));
    }

    private static bool IsForbiddenOneLetterName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        if (name.Length != 1)
            return false;

        if (AllowedOneLetterNames.Contains(name))
            return false;

        // Only ASCII letters are targeted to avoid false positives with non-latin identifiers.
        return name[0] is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
    }
}
