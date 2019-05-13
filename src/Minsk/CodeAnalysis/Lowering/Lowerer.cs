using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Minsk.CodeAnalysis.Binding;
using Minsk.CodeAnalysis.Symbols;
using Minsk.CodeAnalysis.Syntax;

namespace Minsk.CodeAnalysis.Lowering
{
    internal sealed class Lowerer : BoundTreeRewriter
    {
        private int _labelCount;

        private Lowerer()
        {
        }

        private BoundLabel GenerateLabel()
        {
            var name = $"Label{++_labelCount}";
            return new BoundLabel(name);
        }

        public static BoundBlockStatement Lower(BoundStatement statement)
        {
            var lowerer = new Lowerer();
            var result = lowerer.RewriteStatement(statement);
            return Flatten(result);
        }

        private static BoundBlockStatement Flatten(BoundStatement statement)
        {
            var builder = ImmutableArray.CreateBuilder<BoundStatement>();
            var stack = new Stack<BoundStatement>();
            stack.Push(statement);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                if (current is BoundBlockStatement block)
                {
                    foreach (var s in block.Statements.Reverse())
                        stack.Push(s);
                }
                else
                {
                    builder.Add(current);
                }
            }

            return new BoundBlockStatement(builder.ToImmutable());
        }

        protected override BoundStatement RewriteIfStatement(BoundIfStatement node)
        {
            if (node.ElseStatement == null)
            {
                // if <condition>
                //      <then>
                //
                // ---->
                //
                // gotoFalse <condition> end
                // <then>
                // end:
                var endLabel = GenerateLabel();
                var gotoFalse = new BoundConditionalGotoStatement(endLabel, node.Condition, false);
                var endLabelStatement = new BoundLabelStatement(endLabel);
                var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(gotoFalse, node.ThenStatement, endLabelStatement));
                return RewriteStatement(result);
            }
            else
            {
                // if <condition>
                //      <then>
                // else
                //      <else>
                //
                // ---->
                //
                // gotoFalse <condition> else
                // <then>
                // goto end
                // else:
                // <else>
                // end:

                var elseLabel = GenerateLabel();
                var endLabel = GenerateLabel();

                var gotoFalse = new BoundConditionalGotoStatement(elseLabel, node.Condition, false);
                var gotoEndStatement = new BoundGotoStatement(endLabel);
                var elseLabelStatement = new BoundLabelStatement(elseLabel);
                var endLabelStatement = new BoundLabelStatement(endLabel);
                var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                    gotoFalse,
                    node.ThenStatement,
                    gotoEndStatement,
                    elseLabelStatement,
                    node.ElseStatement,
                    endLabelStatement
                ));
                return RewriteStatement(result);
            }
        }

        protected override BoundStatement RewriteWhileStatement(BoundWhileStatement node)
        {
            // while <condition>
            //      <body>
            //
            // ----->
            //
            // goto check
            // continue:
            // <body>
            // check:
            // gotoTrue <condition> continue
            //

            var continueLabel = GenerateLabel();
            var checkLabel = GenerateLabel();

            var gotoCheck = new BoundGotoStatement(checkLabel);
            var continueLabelStatement = new BoundLabelStatement(continueLabel);
            var checkLabelStatement = new BoundLabelStatement(checkLabel);
            var gotoTrue = new BoundConditionalGotoStatement(continueLabel, node.Condition);

            var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                gotoCheck,
                continueLabelStatement,
                node.Body,
                checkLabelStatement,
                gotoTrue
            ));

            return RewriteStatement(result);
        }

        protected override BoundStatement RewriteDoWhileStatement(BoundDoWhileStatement node)
        {
            // do
            //      <body>
            // while <condition>
            //
            // ----->
            //
            // continue:
            // <body>
            // gotoTrue <condition> continue
            //

            var continueLabel = GenerateLabel();

            var continueLabelStatement = new BoundLabelStatement(continueLabel);
            var gotoTrue = new BoundConditionalGotoStatement(continueLabel, node.Condition);

            var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                continueLabelStatement,
                node.Body,
                gotoTrue
            ));

            return RewriteStatement(result);
        }

        protected override BoundStatement RewriteForStatement(BoundForStatement node)
        {
            if (node.Stepper == null)
            {
                // for <var> = <lower> to <upper>
                //      <body>
                //
                // ---->
                //
                // {
                //      var <var> = <lower>
                //      let upperBound = <upper>
                //      while (<var> <= upperBound)
                //      {
                //          <body>
                //          <var> = <var> + 1
                //      }
                // }

                var variableDeclaration = new BoundVariableDeclaration(node.Variable, node.LowerBound);
                var variableExpression = new BoundVariableExpression(node.Variable);
                var upperBoundSymbol = new VariableSymbol("upperBound", true, TypeSymbol.Int);
                var upperBoundDeclaration = new BoundVariableDeclaration(upperBoundSymbol, node.UpperBound);
                var upperBoundExpression = new BoundVariableExpression(upperBoundSymbol);
                var condition = new BoundBinaryExpression(
                    variableExpression,
                    BoundBinaryOperator.Bind(SyntaxKind.LessOrEqualsToken, TypeSymbol.Int, TypeSymbol.Int),
                    upperBoundExpression
                );
                var increment = new BoundExpressionStatement(
                    new BoundAssignmentExpression(
                        node.Variable,
                        new BoundBinaryExpression(
                            variableExpression,
                            BoundBinaryOperator.Bind(SyntaxKind.PlusToken, TypeSymbol.Int, TypeSymbol.Int),
                            new BoundLiteralExpression(1)
                        )
                    )
                );
                var whileBody = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(node.Body, increment));
                var whileStatement = new BoundWhileStatement(condition, whileBody);
                var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                    variableDeclaration,
                    upperBoundDeclaration,
                    whileStatement
                ));

                return RewriteStatement(result);
            }
            else
            {
                // for <var> = <lower> to <upper> step <stepper>
                //      <body>
                //
                // ---->
                //
                // {
                //      var <var> = <lower>
                //      let upperBound = <upper>
                //      let stepper = <stepper>
                //      while (stepper > 0 && <var> <= upperBound || stepper < 0 && <var> >= upperBound)
                //      {
                //          <body>
                //          <var> = <var> + <stepper>
                //      }
                // }

                var variableDeclaration = new BoundVariableDeclaration(node.Variable, node.LowerBound);
                var variableExpression = new BoundVariableExpression(node.Variable);
                var upperBoundSymbol = new VariableSymbol("upperBound", true, TypeSymbol.Int);
                var upperBoundDeclaration = new BoundVariableDeclaration(upperBoundSymbol, node.UpperBound);
                var upperBoundExpression = new BoundVariableExpression(upperBoundSymbol);
                var stepperSymbol = new VariableSymbol("stepper", true, TypeSymbol.Int);
                var stepperDeclaration = new BoundVariableDeclaration(stepperSymbol, node.Stepper);
                var stepperExpression = new BoundVariableExpression(stepperSymbol);
                var condition = new BoundBinaryExpression(
                    new BoundBinaryExpression(
                        new BoundBinaryExpression(
                            stepperExpression,
                            BoundBinaryOperator.Bind(SyntaxKind.GreaterToken, TypeSymbol.Int, TypeSymbol.Int),
                            new BoundLiteralExpression(0)
                        ),
                        BoundBinaryOperator.Bind(SyntaxKind.AmpersandAmpersandToken, TypeSymbol.Bool, TypeSymbol.Bool),
                        new BoundBinaryExpression(
                            variableExpression,
                            BoundBinaryOperator.Bind(SyntaxKind.LessOrEqualsToken, TypeSymbol.Int, TypeSymbol.Int),
                            upperBoundExpression
                        )
                    ),
                    BoundBinaryOperator.Bind(SyntaxKind.PipePipeToken, TypeSymbol.Bool, TypeSymbol.Bool),
                    new BoundBinaryExpression(
                        new BoundBinaryExpression(
                            stepperExpression,
                            BoundBinaryOperator.Bind(SyntaxKind.LessToken, TypeSymbol.Int, TypeSymbol.Int),
                            new BoundLiteralExpression(0)
                        ),
                        BoundBinaryOperator.Bind(SyntaxKind.AmpersandAmpersandToken, TypeSymbol.Bool, TypeSymbol.Bool),
                        new BoundBinaryExpression(
                            variableExpression,
                            BoundBinaryOperator.Bind(SyntaxKind.GreaterOrEqualsToken, TypeSymbol.Int, TypeSymbol.Int),
                            upperBoundExpression
                        )
                    )
                );
                var increment = new BoundExpressionStatement(
                    new BoundAssignmentExpression(
                        node.Variable,
                        new BoundBinaryExpression(
                            variableExpression,
                            BoundBinaryOperator.Bind(SyntaxKind.PlusToken, TypeSymbol.Int, TypeSymbol.Int),
                            stepperExpression
                        )
                    )
                );
                var whileBody = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(node.Body, increment));
                var whileStatement = new BoundWhileStatement(condition, whileBody);
                var result = new BoundBlockStatement(ImmutableArray.Create<BoundStatement>(
                    variableDeclaration,
                    upperBoundDeclaration,
                    stepperDeclaration,
                    whileStatement
                ));

                return RewriteStatement(result);
            }
        }
    }
}