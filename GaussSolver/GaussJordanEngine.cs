using System;
using System.Collections.Generic;

namespace GaussSolver
{
    public class GaussJordanEngine
    {
        public class REF
        {
            private int rows;
            private int cols;
            private Matrix matrix;

            public List<StepRecord> Steps { get; private set; }

            public REF(int rows, int cols, Matrix inputMatrix)
            {
                this.rows = rows;
                this.cols = cols;
                this.matrix = inputMatrix.Clone();
                this.Steps = new List<StepRecord>();
            }

            public Matrix Calculate()
            {
                Steps.Clear();
                int stepNumber = 1;

                Steps.Add(new StepRecord(
                    stepNumber++,
                    matrix.Clone(),
                    "Initial Matrix",
                    "Start with the initial augmented matrix.",
                    -1, -1, -1, -1
                ));

                int pivotRow = 0;

                for (int col = 0; col < cols - 1 && pivotRow < rows; col++)
                {
                    int nonZeroRow = -1;
                    for (int r = pivotRow; r < rows; r++)
                    {
                        if (!matrix[r, col].IsZero)
                        {
                            nonZeroRow = r;
                            break;
                        }
                    }

                    if (nonZeroRow == -1)
                        continue;

                    if (nonZeroRow != pivotRow)
                    {
                        matrix.SwapRows(pivotRow, nonZeroRow);
                        Steps.Add(new StepRecord(
                            stepNumber++,
                            matrix.Clone(),
                            $"R{pivotRow + 1} <-> R{nonZeroRow + 1}",
                            $"Swap Row {pivotRow + 1} with Row {nonZeroRow + 1} to bring a non-zero pivot to position ({pivotRow + 1}, {col + 1}).",
                            pivotRow, col, pivotRow, nonZeroRow
                        ));
                    }

                    Fraction pivotValue = matrix[pivotRow, col];
                    if (pivotValue != Fraction.One)
                    {
                        Fraction scaleFactor = pivotValue.Reciprocal();
                        matrix.ScaleRow(pivotRow, scaleFactor);
                        Steps.Add(new StepRecord(
                            stepNumber++,
                            matrix.Clone(),
                            $"R{pivotRow + 1} = ({scaleFactor}) * R{pivotRow + 1}",
                            $"Scale Row {pivotRow + 1} by {scaleFactor} to make the pivot equal to 1.",
                            pivotRow, col, pivotRow, pivotRow
                        ));
                    }

                    for (int r = pivotRow + 1; r < rows; r++)
                    {
                        Fraction factor = matrix[r, col];
                        if (!factor.IsZero)
                        {
                            Fraction multiplier = -factor;
                            matrix.AddMultipleOfRow(r, pivotRow, multiplier);
                            Steps.Add(new StepRecord(
                                stepNumber++,
                                matrix.Clone(),
                                $"R{r + 1} = R{r + 1} + ({multiplier}) * R{pivotRow + 1}",
                                $"Eliminate the entry in Row {r + 1}, Column {col + 1} using Row {pivotRow + 1}.",
                                pivotRow, col, r, pivotRow
                            ));
                        }
                    }

                    pivotRow++;
                }

                return matrix;
            }
        }

        public class RREF
        {
            private int rows;
            private int cols;
            private Matrix matrix;

            public List<StepRecord> Steps { get; private set; }

            public RREF(int rows, int cols, Matrix inputMatrix)
            {
                this.rows = rows;
                this.cols = cols;
                this.matrix = inputMatrix.Clone();
                this.Steps = new List<StepRecord>();
            }

            public Matrix Calculate()
            {
                REF refSolver = new REF(rows, cols, matrix);
                matrix = refSolver.Calculate();
                Steps = new List<StepRecord>(refSolver.Steps);

                int stepNumber = Steps.Count + 1;

                for (int r = rows - 1; r >= 0; r--)
                {
                    int pivotCol = -1;
                    for (int c = 0; c < cols - 1; c++)
                    {
                        if (!matrix[r, c].IsZero)
                        {
                            pivotCol = c;
                            break;
                        }
                    }

                    if (pivotCol == -1)
                        continue;

                    for (int aboveRow = r - 1; aboveRow >= 0; aboveRow--)
                    {
                        Fraction factor = matrix[aboveRow, pivotCol];
                        if (!factor.IsZero)
                        {
                            Fraction multiplier = -factor;
                            matrix.AddMultipleOfRow(aboveRow, r, multiplier);
                            Steps.Add(new StepRecord(
                                stepNumber++,
                                matrix.Clone(),
                                $"R{aboveRow + 1} = R{aboveRow + 1} + ({multiplier}) * R{r + 1}",
                                $"Eliminate the entry above the pivot in Row {aboveRow + 1}, Column {pivotCol + 1}.",
                                r, pivotCol, aboveRow, r
                            ));
                        }
                    }
                }

                return matrix;
            }
        }

        public static string GetSolutionSummary(Matrix finalMatrix)
        {
            int rows = finalMatrix.Rows;
            int cols = finalMatrix.Columns;
            int numVars = cols - 1;
            int rank = 0;

            for (int r = 0; r < rows; r++)
            {
                bool allCoeffsZero = true;
                for (int c = 0; c < numVars; c++)
                {
                    if (!finalMatrix[r, c].IsZero)
                    {
                        allCoeffsZero = false;
                        break;
                    }
                }

                if (allCoeffsZero && !finalMatrix[r, numVars].IsZero)
                {
                    return "No Solution (Inconsistent System)";
                }

                if (!allCoeffsZero)
                {
                    rank++;
                }
            }

            if (rank < numVars)
            {
                return $"Infinite Solutions ({numVars - rank} Free Variable(s))";
            }

            Fraction[] solutions = new Fraction[numVars];
            for (int i = numVars - 1; i >= 0; i--)
            {
                Fraction sum = finalMatrix[i, numVars];
                for (int j = i + 1; j < numVars; j++)
                {
                    sum = sum - (finalMatrix[i, j] * solutions[j]);
                }
                solutions[i] = sum / finalMatrix[i, i];
            }

            List<string> results = new List<string>();
            for (int i = 0; i < numVars; i++)
            {
                results.Add($"x{i + 1} = {solutions[i]}");
            }

            return "Unique Solution:  " + string.Join(" ,  ", results);
        }
    }
}