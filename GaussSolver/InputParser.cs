#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GaussSolver
{
    public static class InputParser
    {
        // =====================================================================
        //  1) Matrix input  (space / comma separated numbers)
        // =====================================================================
        public static bool TryParseMatrix(
            string input,
            out Matrix matrix,
            out string error)
        {
            matrix = null;
            error = "";

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Please enter a matrix.";
                return false;
            }

            string[] lines = input.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);

            var rows = new List<Fraction[]>();
            int columnCount = -1;

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = Regex.Split(line.Trim(), @"[\s,;|]+")
                    .Where(p => p.Length > 0)
                    .ToArray();

                if (columnCount == -1)
                    columnCount = parts.Length;

                if (parts.Length != columnCount)
                {
                    error = "All matrix rows must have the same number of values.";
                    return false;
                }

                var values = new Fraction[parts.Length];

                for (int i = 0; i < parts.Length; i++)
                {
                    if (!Fraction.TryParse(parts[i], out Fraction value))
                    {
                        error = $"Invalid value '{parts[i]}' in row {rows.Count + 1}.";
                        return false;
                    }

                    values[i] = value;
                }

                rows.Add(values);
            }

            if (rows.Count == 0 || columnCount <= 0)
            {
                error = "The matrix is empty.";
                return false;
            }

            matrix = new Matrix(rows.Count, columnCount);

            for (int r = 0; r < rows.Count; r++)
                matrix.SetRow(r, rows[r]);

            return true;
        }

        // =====================================================================
        //  2) Equations input
        // =====================================================================
        public static bool TryParseEquations(
            string input,
            out Matrix matrix,
            out string error)
        {
            return TryParseEquations(input, out matrix, out error, out _, out _);
        }

        public static bool TryParseEquations(
            string input,
            out Matrix matrix,
            out string error,
            out string[] variableNames,
            out int failedEquationIndex)
        {
            matrix = null;
            error = "";
            variableNames = Array.Empty<string>();
            failedEquationIndex = -1;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Please enter equations.";
                return false;
            }

            // تنظيف الأسطر: شيل الترقيم والأقواس وتجاهل الأسطر العنوانية (مفيهاش أرقام ولا =)
            var lines = new List<string>();
            foreach (string raw in input.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string s = CleanEquationLine(raw);
                if (s.Length == 0) continue;

                bool hasEquals = s.IndexOf('=') >= 0;
                bool hasDigit = s.Any(char.IsDigit);
                if (!hasEquals && !hasDigit) continue;

                lines.Add(s);
            }

            if (lines.Count == 0)
            {
                error = "No equations were found in the text.";
                return false;
            }

            var parsedRows = new List<Dictionary<string, Fraction>>();
            var constants = new List<Fraction>();
            var variables = new SortedSet<string>(new VariableComparer());

            for (int i = 0; i < lines.Count; i++)
            {
                if (!TryParseEquation(lines[i], out Fraction[] values, out string lineError, out string[] names))
                {
                    failedEquationIndex = i;
                    error = $"Equation {i + 1} ('{lines[i]}'): {lineError}";
                    return false;
                }

                var row = new Dictionary<string, Fraction>(StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < names.Length; j++)
                {
                    row[names[j]] = values[j];
                    variables.Add(names[j]);
                }

                parsedRows.Add(row);
                constants.Add(values[values.Length - 1]);
            }

            variableNames = variables.ToArray();

            if (variableNames.Length == 0)
            {
                error = "No variables were found in the equations.";
                return false;
            }

            matrix = new Matrix(lines.Count, variableNames.Length + 1);

            for (int r = 0; r < lines.Count; r++)
            {
                for (int c = 0; c < variableNames.Length; c++)
                {
                    matrix[r, c] = parsedRows[r].TryGetValue(variableNames[c], out Fraction coefficient)
                        ? coefficient
                        : Fraction.Zero;
                }

                matrix[r, variableNames.Length] = constants[r];
            }

            return true;
        }

        public static bool TryParseSingleEquationForMatrix(
            string equationStr,
            string[] expectedVariables,
            out Fraction[] alignedRowValues,
            out string errorMessage)
        {
            alignedRowValues = Array.Empty<Fraction>();

            if (!TryParseEquation(equationStr, out Fraction[] rawValues, out errorMessage, out string[] foundNames))
                return false;

            var coeffMap = new Dictionary<string, Fraction>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < foundNames.Length; i++)
            {
                if (!expectedVariables.Contains(foundNames[i], StringComparer.OrdinalIgnoreCase))
                {
                    errorMessage = $"Unexpected variable '{foundNames[i]}'. Expected variables: {string.Join(", ", expectedVariables)}.";
                    return false;
                }
                coeffMap[foundNames[i]] = rawValues[i];
            }

            alignedRowValues = new Fraction[expectedVariables.Length + 1];
            for (int c = 0; c < expectedVariables.Length; c++)
            {
                alignedRowValues[c] = coeffMap.TryGetValue(expectedVariables[c], out Fraction val)
                    ? val
                    : Fraction.Zero;
            }

            alignedRowValues[expectedVariables.Length] = rawValues[rawValues.Length - 1];
            return true;
        }

        public static bool TryParseEquation(
            string equationStr,
            out Fraction[] rowValues,
            out string errorMessage)
        {
            return TryParseEquation(equationStr, out rowValues, out errorMessage, out _);
        }

        // ---------------------------------------------------------------------
        //  Single equation parser.
        //  Supports:  2x   x/2   3/4x   (1/2)x   2*x   0.5y   -y
        //             variables and constants on BOTH sides:  2x + 1 = y - 3
        //  Output: coefficients (sorted by variable name) + constant on the right.
        // ---------------------------------------------------------------------
        private static bool TryParseEquation(
            string equationStr,
            out Fraction[] rowValues,
            out string errorMessage,
            out string[] variableNames)
        {
            rowValues = Array.Empty<Fraction>();
            variableNames = Array.Empty<string>();
            errorMessage = "";

            if (string.IsNullOrWhiteSpace(equationStr))
            {
                errorMessage = "The equation is empty.";
                return false;
            }

            string eq = NormalizeEquationText(equationStr);
            string[] sides = eq.Split('=');

            if (sides.Length != 2)
            {
                errorMessage = "Each equation must contain exactly one '=' sign.";
                return false;
            }

            if (sides[0].Length == 0 || sides[1].Length == 0)
            {
                errorMessage = "Both sides of '=' must contain something.";
                return false;
            }

            var coefficients = new Dictionary<string, Fraction>(StringComparer.OrdinalIgnoreCase);
            Fraction leftConst = Fraction.Zero;
            Fraction rightConst = Fraction.Zero;

            if (!TryParseSide(sides[0], coefficients, 1, ref leftConst, out errorMessage)) return false;
            if (!TryParseSide(sides[1], coefficients, -1, ref rightConst, out errorMessage)) return false;

            if (coefficients.Count == 0)
            {
                errorMessage = "No variables were found in this equation.";
                return false;
            }

            variableNames = coefficients.Keys
                .OrderBy(v => v, new VariableComparer())
                .ToArray();

            rowValues = new Fraction[variableNames.Length + 1];
            for (int i = 0; i < variableNames.Length; i++)
                rowValues[i] = coefficients[variableNames[i]];

            rowValues[rowValues.Length - 1] = rightConst - leftConst;
            return true;
        }

        private static readonly Regex NumberRegex =
            new Regex(@"\G(?:\d+(?:\.\d+)?|\.\d+)", RegexOptions.Compiled);

        private static readonly Regex VariableRegex =
            new Regex(@"\G[a-zA-Z](?:_?\d+)?", RegexOptions.Compiled);

        private static bool TryParseSide(
            string side,
            Dictionary<string, Fraction> coefficients,
            int sideSign,
            ref Fraction constant,
            out string error)
        {
            error = "";

            foreach (string term in SplitTerms(side))
            {
                if (!TryParseTerm(term, out Fraction coef, out string variable, out error))
                    return false;

                if (variable == null)
                {
                    constant = constant + coef;
                }
                else
                {
                    Fraction signed = sideSign < 0 ? -coef : coef;
                    coefficients[variable] = coefficients.TryGetValue(variable, out Fraction old)
                        ? old + signed
                        : signed;
                }
            }

            return true;
        }

        // يقسّم التعبير عند + أو - الموجودة خارج الأقواس
        private static List<string> SplitTerms(string s)
        {
            var terms = new List<string>();
            int depth = 0;
            int start = 0;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (depth == 0 && (c == '+' || c == '-') && i > start)
                {
                    char prev = s[i - 1];
                    if (prev != '+' && prev != '-' && prev != '*' && prev != '/')
                    {
                        terms.Add(s.Substring(start, i - start));
                        start = i;
                    }
                }
            }

            if (start < s.Length)
                terms.Add(s.Substring(start));

            return terms;
        }

        // حد واحد مثل:  -3/4x   y/2   (1/2)z   2*w   7
        private static bool TryParseTerm(
            string term,
            out Fraction coef,
            out string variable,
            out string error)
        {
            coef = Fraction.One;
            variable = null;
            error = "";

            int i = 0;
            while (i < term.Length && (term[i] == '+' || term[i] == '-'))
            {
                if (term[i] == '-') coef = -coef;
                i++;
            }

            if (i >= term.Length)
            {
                error = $"Incomplete term '{term}'.";
                return false;
            }

            char op = '*';

            while (i < term.Length)
            {
                Fraction value = Fraction.One;
                string name = null;
                char c = term[i];

                if (c >= '0' && c <= '9' || c == '.')
                {
                    Match m = NumberRegex.Match(term, i);
                    if (!m.Success || !Fraction.TryParse(m.Value, out value))
                    {
                        error = $"Invalid number near '{term.Substring(i)}'.";
                        return false;
                    }
                    i += m.Length;
                }
                else if (c == '(')
                {
                    int depth = 0;
                    int j = i;
                    for (; j < term.Length; j++)
                    {
                        if (term[j] == '(') depth++;
                        else if (term[j] == ')')
                        {
                            depth--;
                            if (depth == 0) break;
                        }
                    }

                    if (j >= term.Length)
                    {
                        error = "Missing closing parenthesis ')'.";
                        return false;
                    }

                    string inner = term.Substring(i + 1, j - i - 1);
                    if (inner.Length == 0)
                    {
                        error = "Empty parentheses.";
                        return false;
                    }

                    var innerCoefs = new Dictionary<string, Fraction>(StringComparer.OrdinalIgnoreCase);
                    Fraction innerConst = Fraction.Zero;
                    if (!TryParseSide(inner, innerCoefs, 1, ref innerConst, out error))
                        return false;

                    if (innerCoefs.Count > 0)
                    {
                        error = "Variables inside parentheses are not supported. Expand the expression first.";
                        return false;
                    }

                    value = innerConst;
                    i = j + 1;
                }
                else if (char.IsLetter(c))
                {
                    Match m = VariableRegex.Match(term, i);
                    if (!m.Success)
                    {
                        error = $"Invalid variable near '{term.Substring(i)}'.";
                        return false;
                    }
                    name = m.Value;
                    i += m.Length;
                }
                else
                {
                    error = $"Unexpected character '{c}' in '{term}'.";
                    return false;
                }

                if (name != null)
                {
                    if (op == '/')
                    {
                        error = "Division by a variable is not supported.";
                        return false;
                    }
                    if (variable != null)
                    {
                        error = $"Non-linear term '{term}' (product of variables).";
                        return false;
                    }
                    variable = name;
                }
                else if (op == '*')
                {
                    coef = coef * value;
                }
                else
                {
                    if (value.IsZero)
                    {
                        error = "Division by zero.";
                        return false;
                    }
                    coef = coef / value;
                }

                if (i >= term.Length) break;

                char next = term[i];
                if (next == '*') { op = '*'; i++; }
                else if (next == '/') { op = '/'; i++; }
                else op = '*'; // ضرب ضمني: 2x  أو  3/4x

                if (i >= term.Length)
                {
                    error = $"Incomplete term '{term}'.";
                    return false;
                }
            }

            return true;
        }

        // توحيد الرموز: سالب/ضرب/قسمة/أرقام عربية/فاصلة عشرية، وإزالة المسافات
        private static string NormalizeEquationText(string input)
        {
            var sb = new StringBuilder(input.Length);

            foreach (char c in input)
            {
                if (c >= '\u0660' && c <= '\u0669') sb.Append((char)('0' + (c - '\u0660')));
                else if (c >= '\u06F0' && c <= '\u06F9') sb.Append((char)('0' + (c - '\u06F0')));
                else
                {
                    switch (c)
                    {
                        case '\u2212': case '\u2013': case '\u2014': case '\u2012':
                        case '\uFE63': case '\u0096': case '\u0097': case '~':
                            sb.Append('-'); break;
                        case '\u00D7': case '\u00B7': case '\u22C5':
                            sb.Append('*'); break;
                        case '\u00F7': case '\u2215': case '\u2044': case '\\':
                            sb.Append('/'); break;
                        case '\uFF1D':
                            sb.Append('='); break;
                        case '\u066B':
                            sb.Append('.'); break;
                        default:
                            if (!char.IsWhiteSpace(c)) sb.Append(c);
                            break;
                    }
                }
            }

            return Regex.Replace(sb.ToString(), @"(?<=\d),(?=\d)", ".");
        }

        // تنظيف سطر واحد جاي من OCR/PDF: أقواس النظام { ، الترقيم 1) ، "Eq1:" ، علامات في الآخر
        private static string CleanEquationLine(string line)
        {
            string s = line.Trim();
            s = Regex.Replace(s, @"^[\{\[\|]+\s*", "");
            s = Regex.Replace(s, @"^\(?\d+[\)\.:]\s+", "");
            s = Regex.Replace(s, @"^(?:Eq|Equation)\.?\s*\d*\s*[:.)]\s*", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"[\s.;,]+$", "");
            return s.Trim();
        }

        // ترتيب طبيعي للمتغيرات: x, x2, x10, y, z ...
        private sealed class VariableComparer : IComparer<string>
        {
            public int Compare(string a, string b)
            {
                Match ma = Regex.Match(a, @"^(\D*?)_?(\d*)$");
                Match mb = Regex.Match(b, @"^(\D*?)_?(\d*)$");

                int c = string.Compare(ma.Groups[1].Value, mb.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;

                long na = -1, nb = -1;
                if (ma.Groups[2].Value.Length > 0) long.TryParse(ma.Groups[2].Value, out na);
                if (mb.Groups[2].Value.Length > 0) long.TryParse(mb.Groups[2].Value, out nb);
                return na.CompareTo(nb);
            }
        }

        // =====================================================================
        //  3) File import  (txt / csv / pdf / images)
        // =====================================================================
        public static bool TryExtractTextFromFile(string filePath, out string extractedText, out string error)
        {
            extractedText = "";
            error = "";

            if (!File.Exists(filePath))
            {
                error = "File does not exist.";
                return false;
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            try
            {
                if (ext == ".txt" || ext == ".csv")
                {
                    extractedText = File.ReadAllText(filePath, Encoding.UTF8).Trim();
                    if (string.IsNullOrWhiteSpace(extractedText))
                    {
                        error = "The file is empty.";
                        return false;
                    }
                    return true;
                }
                else if (ext == ".pdf")
                {
                    extractedText = ExtractTextFromPdf(filePath);
                    if (string.IsNullOrWhiteSpace(extractedText))
                    {
                        error = "Could not extract readable text from this PDF.\n" +
                                "It may be a scanned PDF or use embedded fonts.\n" +
                                "Tip: take a screenshot of the equations and import the image instead.";
                        return false;
                    }
                    return true;
                }
                else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                {
                    extractedText = ExtractTextFromImageWindowsOcr(filePath);
                    if (string.IsNullOrWhiteSpace(extractedText))
                    {
                        error = "No readable text or equations were detected in the image.";
                        return false;
                    }
                    return true;
                }
                else
                {
                    error = "Unsupported file format.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = $"Failed to read file: {ex.Message}";
                return false;
            }
        }

        // ---------------------------------------------------------------------
        //  Image preprocessing: تكبير آمن (لا يتعدى حد Windows OCR) + قلب الألوان
        //  للخلفيات الداكنة. بدون binarization لأنه كان بيبوّظ الخطوط الرفيعة.
        // ---------------------------------------------------------------------
        private static readonly ColorMatrix InvertMatrix = new ColorMatrix(new float[][]
        {
            new float[] { -1,  0,  0, 0, 0 },
            new float[] {  0, -1,  0, 0, 0 },
            new float[] {  0,  0, -1, 0, 0 },
            new float[] {  0,  0,  0, 1, 0 },
            new float[] {  1,  1,  1, 0, 1 }
        });

        private static bool IsDarkBackground(Bitmap src)
        {
            long total = 0;
            int count = 0;
            int stepY = Math.Max(1, src.Height / 20);
            int stepX = Math.Max(1, src.Width / 20);

            for (int y = 0; y < src.Height; y += stepY)
            {
                for (int x = 0; x < src.Width; x += stepX)
                {
                    Color c = src.GetPixel(x, y);
                    total += (c.R + c.G + c.B) / 3;
                    count++;
                }
            }

            return count > 0 && (total / count) < 128;
        }

        private static string PreprocessImageForOcr(string originalPath)
        {
            const int pad = 40;
            const int maxDimension = 2400; // حد Windows OCR حوالي 2600

            using (Bitmap src = new Bitmap(originalPath))
            {
                double scale = Math.Min(3.0, (double)(maxDimension - 2 * pad) / Math.Max(src.Width, src.Height));
                if (scale <= 0) scale = 1;

                int w = Math.Max(1, (int)(src.Width * scale));
                int h = Math.Max(1, (int)(src.Height * scale));
                bool dark = IsDarkBackground(src);

                using (Bitmap result = new Bitmap(w + pad * 2, h + pad * 2, PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(result))
                    using (ImageAttributes attrs = new ImageAttributes())
                    {
                        g.Clear(Color.White);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;

                        attrs.SetWrapMode(WrapMode.TileFlipXY);
                        if (dark) attrs.SetColorMatrix(InvertMatrix);

                        g.DrawImage(
                            src,
                            new Rectangle(pad, pad, w, h),
                            0, 0, src.Width, src.Height,
                            GraphicsUnit.Pixel,
                            attrs);
                    }

                    string tempPath = Path.Combine(Path.GetTempPath(), "gauss_ocr_" + Guid.NewGuid().ToString("N") + ".png");
                    result.Save(tempPath, ImageFormat.Png);
                    return tempPath;
                }
            }
        }

        // السكريبت بيطبع كل كلمة: X <tab> Y <tab> Height <tab> Text
        private const string OcrScriptTemplate = @"
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType=WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Foundation, ContentType=WindowsRuntime]
$null = [Windows.Globalization.Language, Windows.Foundation, ContentType=WindowsRuntime]

$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
Function Await($WinRtTask, $ResultType) {
    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    $netTask = $asTask.Invoke($null, @($WinRtTask))
    $netTask.Wait(-1) | Out-Null
    $netTask.Result
}

$file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync('__PATH__')) ([Windows.Storage.StorageFile])
$stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
$decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
$bitmap = Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])

$engine = $null
try { $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage((New-Object Windows.Globalization.Language('en-US'))) } catch { }
if ($null -eq $engine) { $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages() }
if ($null -eq $engine) { throw 'No OCR language pack is installed. Add English (US) from Windows Settings > Time and language > Language.' }

$result = Await ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
$tab = [char]9
foreach ($line in $result.Lines) {
    foreach ($w in $line.Words) {
        $r = $w.BoundingRect
        Write-Output (([int]$r.X).ToString() + $tab + ([int]$r.Y).ToString() + $tab + ([int]$r.Height).ToString() + $tab + $w.Text)
    }
}
";

        private static string ExtractTextFromImageWindowsOcr(string imagePath)
        {
            string processedPath = PreprocessImageForOcr(imagePath);
            try
            {
                string script = OcrScriptTemplate.Replace("__PATH__", processedPath.Replace("'", "''"));
                string base64Script = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {base64Script}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (Process proc = Process.Start(psi))
                {
                    var outTask = proc.StandardOutput.ReadToEndAsync();
                    var errTask = proc.StandardError.ReadToEndAsync();

                    if (!proc.WaitForExit(40000))
                    {
                        try { proc.Kill(); } catch { }
                        throw new TimeoutException("The OCR process took too long.");
                    }

                    string output = outTask.Result;
                    string err = errTask.Result;

                    // لو السكريبت فشل، اظهر السبب الحقيقي بدل "no text detected"
                    if (string.IsNullOrWhiteSpace(output) && !string.IsNullOrWhiteSpace(err))
                        throw new InvalidOperationException("OCR failed: " + err.Trim());

                    return SanitizeOcrText(WordsToLines(output));
                }
            }
            finally
            {
                if (File.Exists(processedPath))
                {
                    try { File.Delete(processedPath); } catch { }
                }
            }
        }

        private sealed class OcrWord
        {
            public double X;
            public double Yc;
            public double H;
            public string Text;
        }

        // يعيد بناء الأسطر من إحداثيات الكلمات (بدل أسطر الـ OCR اللي بتتقسم لأعمدة)
        private static string WordsToLines(string tsv)
        {
            var words = new List<OcrWord>();

            foreach (string rawLine in tsv.Split('\n'))
            {
                string ln = rawLine.TrimEnd('\r');
                if (ln.Length == 0) continue;

                string[] p = ln.Split(new[] { '\t' }, 4);
                if (p.Length < 4) continue;

                if (!double.TryParse(p[0], out double x) ||
                    !double.TryParse(p[1], out double y) ||
                    !double.TryParse(p[2], out double h))
                    continue;

                words.Add(new OcrWord { X = x, Yc = y + h / 2.0, H = h, Text = p[3] });
            }

            if (words.Count == 0) return "";

            var heights = words.Select(w => w.H).OrderBy(v => v).ToList();
            double medianH = heights[heights.Count / 2];
            double tolerance = Math.Max(4, medianH * 0.6);

            var rows = new List<List<OcrWord>>();
            var rowCenters = new List<double>();

            foreach (OcrWord w in words.OrderBy(w => w.Yc))
            {
                int last = rows.Count - 1;
                if (last >= 0 && Math.Abs(w.Yc - rowCenters[last]) <= tolerance)
                {
                    rows[last].Add(w);
                    rowCenters[last] = rows[last].Average(v => v.Yc);
                }
                else
                {
                    rows.Add(new List<OcrWord> { w });
                    rowCenters.Add(w.Yc);
                }
            }

            return string.Join(
                Environment.NewLine,
                rows.Select(r => string.Join(" ", r.OrderBy(w => w.X).Select(w => w.Text))));
        }

        // ---------------------------------------------------------------------
        //  PDF text extraction (best effort — بدون مكتبات خارجية)
        //  بيدعم: Flate streams, Tj / TJ, hex strings, escapes, وفواصل الأسطر.
        //  مش بيدعم: PDF ممسوح ضوئيًا أو خطوط Identity-H (استخدم صورة بدلها).
        // ---------------------------------------------------------------------
        private static readonly Regex PdfTextOps = new Regex(
            @"(?<arr>\[(?:[^\]\\]|\\.)*\])\s*TJ" +
            @"|(?<str>\((?:\\.|[^\\()])*\))\s*(?:Tj|')" +
            @"|(?<hex><[0-9A-Fa-f\s]+>)\s*Tj" +
            @"|(?<tm>(?:[-+]?\d*\.?\d+\s+){5}(?<tmy>[-+]?\d*\.?\d+)\s+Tm)" +
            @"|(?<td>[-+]?\d*\.?\d+\s+(?<tdy>[-+]?\d*\.?\d+)\s+T[dD])" +
            @"|(?<nl>T\*|\bET\b)",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex PdfArrayItems = new Regex(
            @"(?<s>\((?:\\.|[^\\()])*\))|(?<h><[0-9A-Fa-f\s]+>)|(?<n>[-+]?\d*\.?\d+)",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static string ExtractTextFromPdf(string pdfPath)
        {
            byte[] bytes = File.ReadAllBytes(pdfPath);
            Encoding latin1 = Encoding.GetEncoding("ISO-8859-1");
            string raw = latin1.GetString(bytes);
            var output = new StringBuilder();

            foreach (Match sm in Regex.Matches(raw, @"stream\r?\n(.*?)\r?\nendstream", RegexOptions.Singleline))
            {
                string streamData = sm.Groups[1].Value;
                string content = TryInflate(latin1.GetBytes(streamData), latin1);
                if (string.IsNullOrEmpty(content)) content = streamData;

                if (!content.Contains("BT")) continue;

                ParsePdfContent(content, output);
                NewLine(output);
            }

            return SanitizeOcrText(output.ToString());
        }

        private static void ParsePdfContent(string content, StringBuilder sb)
        {
            double lastTmY = double.NaN;

            foreach (Match m in PdfTextOps.Matches(content))
            {
                if (m.Groups["arr"].Success)
                {
                    foreach (Match item in PdfArrayItems.Matches(m.Groups["arr"].Value))
                    {
                        if (item.Groups["s"].Success) sb.Append(DecodePdfLiteral(item.Groups["s"].Value));
                        else if (item.Groups["h"].Success) sb.Append(DecodePdfHex(item.Groups["h"].Value));
                        else if (double.TryParse(item.Groups["n"].Value,
                                 System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out double k) && k < -200)
                            sb.Append(' ');
                    }
                }
                else if (m.Groups["str"].Success)
                {
                    sb.Append(DecodePdfLiteral(m.Groups["str"].Value));
                }
                else if (m.Groups["hex"].Success)
                {
                    sb.Append(DecodePdfHex(m.Groups["hex"].Value));
                }
                else if (m.Groups["tm"].Success)
                {
                    if (double.TryParse(m.Groups["tmy"].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double y))
                    {
                        if (double.IsNaN(lastTmY) || Math.Abs(y - lastTmY) > 0.5) NewLine(sb);
                        lastTmY = y;
                    }
                }
                else if (m.Groups["td"].Success)
                {
                    if (double.TryParse(m.Groups["tdy"].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double dy) && Math.Abs(dy) > 0.01)
                        NewLine(sb);
                }
                else if (m.Groups["nl"].Success)
                {
                    NewLine(sb);
                }
            }
        }

        private static void NewLine(StringBuilder sb)
        {
            if (sb.Length > 0 && sb[sb.Length - 1] != '\n')
                sb.Append('\n');
        }

        private static string TryInflate(byte[] data, Encoding enc)
        {
            if (data.Length <= 2) return "";

            try
            {
                using (var ms = new MemoryStream(data))
                using (var zs = new ZLibStream(ms, CompressionMode.Decompress))
                using (var outMs = new MemoryStream())
                {
                    zs.CopyTo(outMs);
                    return enc.GetString(outMs.ToArray());
                }
            }
            catch { }

            try
            {
                using (var ms = new MemoryStream(data, 2, data.Length - 2))
                using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
                using (var outMs = new MemoryStream())
                {
                    ds.CopyTo(outMs);
                    return enc.GetString(outMs.ToArray());
                }
            }
            catch { }

            return "";
        }

        private static string DecodePdfLiteral(string lit)
        {
            var sb = new StringBuilder();
            int end = lit.Length - 1; // آخر قوس

            for (int i = 1; i < end; i++)
            {
                char c = lit[i];
                if (c != '\\') { sb.Append(c); continue; }

                if (++i >= end) break;
                char n = lit[i];

                if (n >= '0' && n <= '7')
                {
                    int v = n - '0';
                    int k = 0;
                    while (k < 2 && i + 1 < end && lit[i + 1] >= '0' && lit[i + 1] <= '7')
                    {
                        v = v * 8 + (lit[++i] - '0');
                        k++;
                    }
                    sb.Append((char)v);
                }
                else if (n == 'n' || n == 'r' || n == 't') sb.Append(' ');
                else if (n == 'b' || n == 'f') { }
                else sb.Append(n); // \(  \)  \\
            }

            return sb.ToString();
        }

        private static string DecodePdfHex(string hex)
        {
            string h = Regex.Replace(hex, @"[<>\s]", "");
            if (h.Length % 2 == 1) h += "0";

            var bytes = new byte[h.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(h.Substring(i * 2, 2), 16);

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

            return Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
        }

        // ---------------------------------------------------------------------
        //  تنظيف عام للنص المستخرج (OCR / PDF)
        // ---------------------------------------------------------------------
        public static string SanitizeOcrText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "";

            string cleaned = raw
                .Replace('\u2212', '-')
                .Replace('\u2013', '-')
                .Replace('\u2014', '-')
                .Replace('\u0096', '-')
                .Replace('\u0097', '-')
                .Replace('~', '-')
                .Replace('\u00D7', '*')
                .Replace('\u00A0', ' ');

            string[] lines = cleaned.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var validLines = new List<string>();

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0)
                    continue;

                if (!trimmed.Contains("="))
                    trimmed = Regex.Replace(trimmed, @"\s*[:?]+\s*(?=-?[\d./]+\s*$)", " = ");

                validLines.Add(trimmed);
            }

            return string.Join(Environment.NewLine, validLines);
        }
    }
}
