namespace Nethereum.EVM.SourceInfo
{
    public class SourceLocation
    {
        public string FilePath { get; set; }
        public int Position { get; set; }
        public int Length { get; set; }
        public string SourceCode { get; set; }
        public string FullFileContent { get; set; }
        public int LineNumber { get; set; }
        public int ColumnNumber { get; set; }
        public int SourceFileIndex { get; set; }
        public string JumpType { get; set; }
        public int ModifierDepth { get; set; }

        public static SourceLocation FromSourceMap(SourceMap sourceMap, string filePath, string fileContent)
        {
            if (sourceMap == null || string.IsNullOrEmpty(fileContent))
                return null;

            int lineNumber, columnNumber;
            GetLineAndColumn(fileContent, sourceMap.Position, out lineNumber, out columnNumber);
            var sourceCode = GetSourceSnippet(fileContent, sourceMap.Position, sourceMap.Length);

            return new SourceLocation
            {
                FilePath = filePath,
                Position = sourceMap.Position,
                Length = sourceMap.Length,
                SourceCode = sourceCode,
                FullFileContent = fileContent,
                LineNumber = lineNumber,
                ColumnNumber = columnNumber,
                SourceFileIndex = sourceMap.SourceFile,
                JumpType = sourceMap.JumpType,
                ModifierDepth = sourceMap.ModifierDepth
            };
        }

        private static void GetLineAndColumn(string content, int position, out int line, out int column)
        {
            line = 1;
            column = 1;

            if (string.IsNullOrEmpty(content) || position < 0 || position >= content.Length)
                return;

            for (int i = 0; i < position && i < content.Length; i++)
            {
                if (content[i] == '\n')
                {
                    line++;
                    column = 1;
                }
                else
                {
                    column++;
                }
            }
        }

        private static string GetSourceSnippet(string content, int position, int length)
        {
            if (string.IsNullOrEmpty(content) || position < 0 || position >= content.Length)
                return null;

            var actualLength = System.Math.Min(length, content.Length - position);
            if (actualLength <= 0)
                return null;

            return content.Substring(position, actualLength);
        }

        public string GetContextLines(int linesBefore = 2, int linesAfter = 2)
        {
            if (string.IsNullOrEmpty(FullFileContent))
                return null;

            var lines = FullFileContent.Split('\n');
            var startLine = System.Math.Max(0, LineNumber - 1 - linesBefore);
            var endLine = System.Math.Min(lines.Length - 1, LineNumber - 1 + linesAfter);

            var sb = new System.Text.StringBuilder();
            for (int i = startLine; i <= endLine; i++)
            {
                var lineNum = i + 1;
                var marker = lineNum == LineNumber ? ">>> " : "    ";
                sb.AppendLine($"{marker}{lineNum,4}: {lines[i].TrimEnd('\r')}");
            }

            return sb.ToString();
        }

        public override string ToString()
        {
            return $"{FilePath}:{LineNumber}:{ColumnNumber}";
        }
    }
}
