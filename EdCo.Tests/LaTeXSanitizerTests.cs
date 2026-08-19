using EdCo.Core.Utilities;
using Xunit;

namespace EdCo.Tests
{
    public class LaTeXSanitizerTests
    {
        [Fact]
        public void Sanitize_RemovesLiteralBackslashT_AndEscapedSpaces()
        {
            var raw = @"(a)\t\( 11\ 001_2 \)" + "\n" + @"(b)\t(i)\t\( 442_5 \)" + "\n" + @"\t(ii)\t\( 577_8 \)";
            var result = LaTeXSanitizer.Sanitize(raw);

            Assert.DoesNotContain(@"\t", result);
            Assert.DoesNotContain(@"\ ", result);
            Assert.Contains(@"\(11 001_{2}\)", result);
            Assert.Contains(@"\(442_{5}\)", result);
            Assert.Contains(@"\(577_{8}\)", result);
        }

        [Fact]
        public void Sanitize_WrapsOrphanSubscriptsOutsideMathDelimiters()
        {
            var raw = "Simplify 442_5 + 577_8";
            var result = LaTeXSanitizer.Sanitize(raw);

            Assert.Contains(@"\(442_{5}\)", result);
            Assert.Contains(@"\(577_{8}\)", result);
        }

        [Fact]
        public void Sanitize_FixesUnclosedLaTeXDelimiters()
        {
            var raw = @"Solve \( 2x + 5 = 15";
            var result = LaTeXSanitizer.Sanitize(raw);

            Assert.EndsWith(@"\)", result);
        }

        [Fact]
        public void Sanitize_CollapsesMultipleBackslashes()
        {
            var raw = @"\\( \\frac{1}{2} \\)";
            var result = LaTeXSanitizer.Sanitize(raw);

            Assert.Equal(@"\(\frac{1}{2}\)", result);
        }

        [Fact]
        public void Sanitize_BalancesUnclosedCurlyBraces()
        {
            var raw = @"Solve \(\frac{3}{4";
            var result = LaTeXSanitizer.Sanitize(raw);

            Assert.EndsWith(@"}\)", result);
        }

        [Fact]
        public void Sanitize_CleansConflictingDollarAndLaTeXDelimiters()
        {
            var raw = @"3 (a) $11 \(001_{2}\)$ (b) (i) $\(442_{5}\)$ (ii) $\(577_{8}\)$";
            var result = LaTeXSanitizer.Sanitize(raw);

            Assert.DoesNotContain(@"$", result);
            Assert.Contains(@"\(11 001_{2}\)", result);
            Assert.Contains(@"\(442_{5}\)", result);
            Assert.Contains(@"\(577_{8}\)", result);
        }

        [Fact]
        public void Sanitize_PreservesBracketedPoints_AndWrapsOrphanFractions()
        {
            var raw = @"[2] (a) Answer is \frac{3}{4} and \[2\] (b) \sqrt{x}";
            var result = LaTeXSanitizer.Sanitize(raw);

            Assert.Contains(@"[2] (a)", result);
            Assert.Contains(@"[2] (b)", result);
            Assert.Contains(@"\(\frac{3}{4}\)", result);
            Assert.Contains(@"\(\sqrt{x}\)", result);
        }

        [Fact]
        public void Sanitize_PreservesLaTeXTableArrays()
        {
            var raw = @"\[ \\begin{array}{|c|c|} \\hline Header 1 & Header 2 \\\\ \\hline Cell 1 & Cell 2 \\\\ \\hline \\end{array} \]";
            var result = LaTeXSanitizer.Sanitize(raw);

            Assert.Contains(@"\begin{array}{|c|c|}", result);
            Assert.Contains(@"\end{array}", result);
            Assert.Contains(@"\hline", result);
        }
    }
}
