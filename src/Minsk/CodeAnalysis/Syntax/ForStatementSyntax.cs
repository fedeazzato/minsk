namespace Minsk.CodeAnalysis.Syntax
{
    public sealed class ForStatementSyntax : StatementSyntax
    {
        public ForStatementSyntax(SyntaxToken keyword, SyntaxToken identifier, SyntaxToken equalsToken, ExpressionSyntax lowerBound, SyntaxToken toKeyword, ExpressionSyntax upperBound, StepClauseSyntax stepClause, StatementSyntax body)
        {
            Keyword = keyword;
            Identifier = identifier;
            EqualsToken = equalsToken;
            LowerBound = lowerBound;
            ToKeyword = toKeyword;
            UpperBound = upperBound;
            StepClause = stepClause;
            Body = body;
        }

        public override SyntaxKind Kind => SyntaxKind.ForStatement;
        public SyntaxToken Keyword { get; }
        public SyntaxToken Identifier { get; }
        public SyntaxToken EqualsToken { get; }
        public ExpressionSyntax LowerBound { get; }
        public SyntaxToken ToKeyword { get; }
        public ExpressionSyntax UpperBound { get; }
        public StepClauseSyntax StepClause { get; }
        public StatementSyntax Body { get; }
    }
}