# GaussSolver — Step-by-Step Linear Algebra Solver

GaussSolver is an interactive desktop application built with C# and Windows Forms (.NET) designed to solve systems of linear equations using Gaussian Elimination (Row Echelon Form - REF) and Gauss-Jordan Elimination (Reduced Row Echelon Form - RREF).

Unlike standard calculators that rely on floating-point approximations, GaussSolver is powered by a custom-built rational arithmetic engine that performs all matrix operations using exact fractions, providing clear step-by-step visual explanations of every elementary row operation.

## Key Features
- **Exact Rational Arithmetic**: Powered by a dedicated Fraction engine that eliminates floating-point rounding errors. All calculations are performed using exact numerator/denominator representations and automatically simplified to their lowest terms via Greatest Common Divisor (GCD) reduction.
- **Dynamic n×m Matrix Support**: Supports arbitrary system dimensions for constructing and solving augmented matrices up to 10x10.
- **Intelligent Equation Parsing & Validation**: Parses algebraic linear equations directly into augmented matrix coefficients with error handling.
- **Interactive Step-by-Step Visualization**:
  - Custom graphical rendering of matrix elements, active pivot elements, and row highlights.
  - Full state history navigation (Next Step / Previous Step / First / Last) enabling users to inspect intermediate matrix states and trace the exact path to REF and RREF.
- **Solution Summary**: Automatically detects and displays whether the system has a Unique Solution, Infinite Solutions, or No Solution, along with exact fraction values for variables.

## Mathematical Foundation
Given a system of n linear equations, GaussSolver constructs the augmented matrix [A|B] and applies the three elementary row operations:
1. **Row Swapping**: $R_i \leftrightarrow R_j$
2. **Row Scaling**: $R_i \rightarrow k \cdot R_i$ where $k \neq 0$
3. **Row Addition**: $R_i \rightarrow R_i + k \cdot R_j$

The solver systematically processes the matrix through two distinct phases:
- **Phase 1 — Forward Elimination (REF)**: Establishes leading pivots and eliminates all entries below each pivot to achieve Row Echelon Form.
- **Phase 2 — Backward Elimination (RREF)**: Normalizes leading pivots to 1 and eliminates all non-zero entries above each pivot to achieve Reduced Row Echelon Form, revealing the exact solution state.

## System Architecture
The project follows a modular, separation-of-concerns architecture under the GaussSolver directory:
- **Fraction.cs**: Core rational number structure implementing arithmetic operators, GCD reduction, and reciprocal calculation.
- **Matrix.cs**: Encapsulates the 2D array of Fraction elements, deep-cloning capabilities, and elementary row operations (`SwapRows`, `ScaleRow`, `AddMultipleOfRow`).
- **InputParser.cs**: Handles algebraic string parsing and syntax validation.
- **GaussJordanEngine.cs**: Implements the step-by-step Gaussian and Gauss-Jordan elimination algorithms along with solution type evaluation (`GetSolutionSummary`).
- **StepRecord.cs**: Data model representing a single snapshot in the solution history, storing the cloned Matrix state, operation text, description, and pivot coordinates for UI highlighting.
- **Form1.cs**: Windows Forms graphical user interface managing user inputs, DataGridView rendering, and step navigation controls.

## License
This project was developed for academic and educational purposes in Linear Algebra and Numerical Computation.
