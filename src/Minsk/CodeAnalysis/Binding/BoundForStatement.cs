using Minsk.CodeAnalysis.Symbols;

namespace Minsk.CodeAnalysis.Binding
{
    internal sealed class BoundForStatement : BoundStatement
    {
        public BoundForStatement(VariableSymbol variable, BoundExpression lowerBound, BoundExpression upperBound, BoundExpression stepper, BoundStatement body)
        {
            Variable = variable;
            LowerBound = lowerBound;
            UpperBound = upperBound;
            Stepper = stepper;
            Body = body;
        }

        public override BoundNodeKind Kind => BoundNodeKind.ForStatement;
        public VariableSymbol Variable { get; }
        public BoundExpression LowerBound { get; }
        public BoundExpression UpperBound { get; }
        public BoundExpression Stepper { get; }
        public BoundStatement Body { get; }
    }
}
