# GaussSolver — Step-by-Step Linear Algebra Solver

**GaussSolver** is an interactive desktop application built with **C#** and **Windows Forms (.NET)** designed to solve systems of linear equations using **Gaussian Elimination** (Row Echelon Form - $\text{REF}$) and **Gauss-Jordan Elimination** (Reduced Row Echelon Form - $\text{RREF}$).

Unlike standard calculators that rely on floating-point approximations, **GaussSolver** is powered by a custom-built rational arithmetic engine that performs all matrix operations using exact fractions, providing clear step-by-step visual explanations of every elementary row operation.

---

## Key Features

- **Exact Rational Arithmetic:** Powered by a dedicated `Fraction` engine that eliminates floating-point rounding errors. All calculations are performed using exact numerator/denominator representations and automatically simplified to their lowest terms via Greatest Common Divisor ($\text{GCD}$) reduction.
- **Dynamic $n \times m$ Matrix Support:** Supports arbitrary system dimensions for constructing and solving augmented matrices of size $n \times m$.
- **Intelligent Equation Parsing & Validation:**
  - Parses algebraic linear equations directly into augmented matrix coefficients.
  - Provides isolated, per-equation error validation—allowing users to fix a single mistyped equation without re-entering the entire system.
- **PDF & Optical Character Recognition (OCR) Import:** Extracts linear systems directly from PDF documents and images, automatically sanitizing formatting artifacts and converting them into solvable mathematical models.
- **Direct Single-Cell Editing:** Allows users to modify individual matrix cells directly from the interface and immediately recompute the solution pipeline without resetting the workspace.
- **Interactive Step-by-Step Visualization:**
  - Custom graphical rendering of matrix brackets, active pivot elements, row highlights, and column guidance arrows.
  - Full state history navigation (**Next Step** / **Previous Step**) enabling users to inspect intermediate matrix states, review mathematical explanations, and trace the exact path to $\text{REF}$ and $\text{RREF}$.

---

## Mathematical Foundation

Given a system of $n$ linear equations, **GaussSolver** constructs the augmented matrix $[A \mid B]$ and applies the three elementary row operations:

1. **Row Swapping:** $R_i \leftrightarrow R_j$
2. **Row Scaling:** $R_i \rightarrow k \cdot R_i \quad \text{where } k \neq 0$
3. **Row Addition:** $R_i \rightarrow R_i + k \cdot R_j$

The solver systematically processes the matrix through two distinct phases:

- **Phase 1 — Forward Elimination ($\text{REF}$):** Establishes leading pivots and eliminates all entries below each pivot to achieve Row Echelon Form.
- **Phase 2 — Backward Elimination ($\text{RREF}$):** Normalizes leading pivots to $1$ and eliminates all non-zero entries above each pivot to achieve Reduced Row Echelon Form, revealing the exact solution state (unique solution, infinitely many solutions, or inconsistent system).

---

## System Architecture

The project follows a modular, separation-of-concerns architecture under the `GaussSolver` directory:

- **`Fraction.cs`:** Core rational number structure implementing arithmetic operators (`+`, `-`, `*`, `/`), comparison operators, reciprocal calculation, algebraic string parsing, and row-operation string formatting.
- **`Matrix.cs`:** Encapsulates the 2D $n \times m$ array of `Fraction` elements, deep-cloning capabilities for state preservation, indexed cell access, and elementary row operations.
- **`InputParser.cs`:** Handles algebraic string parsing, per-equation syntax validation, and document/image text extraction (PDF & OCR).
- **`GaussJordanEngine.cs`:** Implements the step-by-step Gaussian and Gauss-Jordan elimination algorithms, recording every state transition.
- **`StepRecord.cs`:** Data model representing a single snapshot in the solution history, storing the cloned `Matrix` state, algebraic operation notation, textual explanation, algorithm stage, and visual highlight metadata.
- **`Form1.cs`:** Windows Forms graphical user interface managing user inputs, real-time cell editing, step navigation controls, and custom matrix rendering.

---

## License

This project was developed for academic and educational purposes in Linear Algebra and Numerical Computation.
