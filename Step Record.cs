using System;

namespace GaussSolver
{
  
    /// It only stores and exposes data. It does not compute anything.
    public class StepRecord
    {
       
        public int StepNumber { get; private set; }
             
        public Matrix MatrixSnapshot { get; private set; }

        public string OperationText { get; private set; }
               
        public string Description { get; private set; }

        public int PivotRow { get; private set; }

        public int PivotColumn { get; private set; }

        public int TargetRow { get; private set; }

       public int SourceRow { get; private set; }

        public StepRecord(
            int stepNumber,
            Matrix matrixSnapshot,
            string operationText,
            string description,
            int pivotRow = -1,
            int pivotColumn = -1,
            int sourceRow = -1,
            int targetRow = -1)
        {
            
            if (matrixSnapshot == null)
                throw new ArgumentNullException(nameof(matrixSnapshot));


            StepNumber = stepNumber;
            MatrixSnapshot = matrixSnapshot.Clone();
            OperationText = operationText ?? string.Empty;
            Description = description ?? string.Empty;
            PivotRow = pivotRow;
            PivotColumn = pivotColumn;
            SourceRow = sourceRow;
            TargetRow = targetRow;
        }
    }
}
