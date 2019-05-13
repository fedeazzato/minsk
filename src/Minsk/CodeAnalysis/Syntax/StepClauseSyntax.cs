namespace Minsk.CodeAnalysis.Syntax
{
    public sealed class StepClauseSyntax : SyntaxNode
    {
        public StepClauseSyntax(SyntaxToken stepToken, ExpressionSyntax stepStatement)
        {
            StepToken = stepToken;
            StepStatement = stepStatement;
        }

        public override SyntaxKind Kind => SyntaxKind.StepClause;

        public SyntaxToken StepToken { get; }
        public ExpressionSyntax StepStatement { get; }
    }
}