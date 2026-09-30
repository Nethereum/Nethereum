using CodeQuality.Core.Model;
using CodeQuality.Core.Parsing;
using Xunit;

namespace CodeQuality.Tests.Parsing;

public class MethodMetricsTests
{
    const string Source = """
        namespace Sample;

        public class Worker
        {
            public int Add(int a, int b)
            {
                return a + b;
            }

            private void Deep(int n)
            {
                if (n > 0)
                {
                    foreach (var i in Enumerable.Range(0, n))
                    {
                        while (i > 2)
                        {
                            Console.WriteLine(i);
                        }
                    }
                }
            }
        }
        """;

    [Fact]
    public void FindsEveryMethodRegardlessOfIndentation()
    {
        var methods = SourceFileParser.ParseMethods("Worker.cs", Source);

        Assert.Equal(2, methods.Count);
        Assert.Contains(methods, m => m.Name == "Add");
        Assert.Contains(methods, m => m.Name == "Deep");
    }

    [Fact]
    public void RecordsContainingTypeAndVisibility()
    {
        var add = SourceFileParser.ParseMethods("Worker.cs", Source).Single(m => m.Name == "Add");

        Assert.Equal("Worker", add.ContainingType);
        Assert.True(add.IsPublic);
        Assert.Equal(2, add.ParameterCount);
    }

    [Fact]
    public void CountsStatementsAndNestingDepth()
    {
        var deep = SourceFileParser.ParseMethods("Worker.cs", Source).Single(m => m.Name == "Deep");

        Assert.False(deep.IsPublic);
        Assert.Equal(3, deep.MaxNesting);
        Assert.Equal(4, deep.StatementCount);
    }

    [Fact]
    public void ReportsOneBasedDeclarationLine()
    {
        var add = SourceFileParser.ParseMethods("Worker.cs", Source).Single(m => m.Name == "Add");

        Assert.Equal(5, add.Line);
    }

    const string LocalFunctionSource = """
        namespace Sample;

        public class Worker
        {
            public void Outer()
            {
                void Inner(int x, int y)
                {
                    if (x > 0)
                    {
                        if (y > 0)
                        {
                            Console.WriteLine(x + y);
                        }
                    }
                }

                Inner(1, 2);
            }
        }
        """;

    [Fact]
    public void ExcludesLocalFunctionBodyFromOuterNestingButIncludesItsStatements()
    {
        var outer = SourceFileParser.ParseMethods("Worker.cs", LocalFunctionSource).Single(m => m.Name == "Outer");

        Assert.Equal(0, outer.MaxNesting);
        Assert.Equal(5, outer.StatementCount);
    }

    const string LambdaSource = """
        namespace Sample;

        public class Worker
        {
            public void RunLambda()
            {
                Action<int> handler = value =>
                {
                    if (value > 0)
                    {
                        if (value > 10)
                        {
                            Console.WriteLine(value);
                        }
                    }
                };

                handler(5);
            }
        }
        """;

    [Fact]
    public void ExcludesLambdaBodyFromOuterNestingButIncludesItsStatements()
    {
        var method = SourceFileParser.ParseMethods("Worker.cs", LambdaSource).Single(m => m.Name == "RunLambda");

        Assert.Equal(0, method.MaxNesting);
        Assert.Equal(5, method.StatementCount);
    }

    const string AccessorSource = """
        namespace Sample;

        public class Worker
        {
            public int Count
            {
                get
                {
                    return _count;
                }
                private set
                {
                    _count = value;
                }
            }

            int _count;
        }
        """;

    [Fact]
    public void RecordsPropertyAccessorsAsSeparateMethods()
    {
        var methods = SourceFileParser.ParseMethods("Worker.cs", AccessorSource);

        var getter = methods.Single(m => m.Name == "Count.get");
        var setter = methods.Single(m => m.Name == "Count.set");

        Assert.Equal("Worker", getter.ContainingType);
        Assert.Equal(0, getter.ParameterCount);
        Assert.True(getter.IsPublic);
        Assert.False(setter.IsPublic);
    }

    const string AutoPropertySource = """
        namespace Sample;

        public class Widget
        {
            public int Value { get; set; }
        }
        """;

    [Fact]
    public void SkipsAutoImplementedPropertyAccessors()
    {
        var methods = SourceFileParser.ParseMethods("Widget.cs", AutoPropertySource);

        Assert.Empty(methods);
    }
}
