using Microsoft.CodeAnalysis;
using System.Linq;

namespace Nethereum.ABI.Generator
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Text;
    using System.Text;

    [Generator]
    public class StructAbiGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (node, _) => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                    static (ctx, _) => ctx.Node as ClassDeclarationSyntax)
                .Where(static m => m is not null);

            var classDeclarationWithModel = classDeclarations
                .Combine(context.CompilationProvider)
                .Select((pair, cancellationToken) => (classDeclaration: pair.Left, model: pair.Right.GetSemanticModel(pair.Left.SyntaxTree)));

            context.RegisterSourceOutput(classDeclarationWithModel, static (spc, pair) => Execute(spc, pair));
        }

        private static void Execute(SourceProductionContext context, (ClassDeclarationSyntax classDeclaration, SemanticModel model) pair)
        {
            var (classDeclaration, model) = pair;
            var classSymbol = model.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
            if (classSymbol == null || !classSymbol.GetAttributes().Any(attr => attr.AttributeClass?.Name == "StructAttribute")) return;

            var sb = new StringBuilder();
            string className = classSymbol.Name;

            string namespaceName = classSymbol.ContainingNamespace.IsGlobalNamespace ? "" : $"namespace {classSymbol.ContainingNamespace.ToDisplayString()}\n{{";
            var functionAttr = classSymbol.GetAttributes().FirstOrDefault(attr => attr.AttributeClass?.Name == "StructAttribute");
            var functionName = functionAttr.ConstructorArguments[0].Value?.ToString();



            sb.AppendLine($"{namespaceName}");
            sb.AppendLine("using Nethereum.ABI.FunctionEncoding.Attributes;");
            sb.AppendLine("using Nethereum.ABI.Model;");
            sb.AppendLine("using System.Collections.Generic;");
            //
            sb.AppendLine($"public partial class {className} : IGetStructAbi");
            sb.AppendLine("{");
            sb.AppendLine("    private static List<Parameter> _parameters;");
            sb.AppendLine("    public List<Parameter> GetParameters()");
            sb.AppendLine("    {");
            sb.AppendLine("        if (_parameters == null)");
            sb.AppendLine("        {");
            sb.AppendLine("           ");
            sb.AppendLine($"         _parameters = new List<Parameter>();");
            foreach (var member in classSymbol.GetMembers())
            {
                if (member is IPropertySymbol propertySymbol)
                {
                    GenerateParameterCreation(sb, propertySymbol);
                }
            }
            sb.AppendLine("        }");
            sb.AppendLine("        return _parameters;");
            sb.AppendLine("    }");

            sb.AppendLine("    public void SetValue(string parameterName, object value)");
            sb.AppendLine("    {");
            foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                GenerateSetValue(sb, member); // SetValue logic
            }
            sb.AppendLine("    }");

            // Generate GetValue method
            sb.AppendLine("    public object GetValue(string parameterName)");
            sb.AppendLine("    {");
            foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                GenerateGetValue(sb, member); // GetValue logic
            }
            sb.AppendLine("        return null;"); // Default return if no matching property
            sb.AppendLine("    }");

            // Class footer
            sb.AppendLine("}");
            sb.AppendLine("}");
            var value = sb.ToString();

            context.AddSource($"{className}_StructAbi.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static void GenerateSetValue(StringBuilder sb, IPropertySymbol propertySymbol)
        {
            var parameterAttrs = propertySymbol.GetAttributes().Where(attr => attr.AttributeClass?.Name == "ParameterAttribute");

            foreach (var parameterAttr in parameterAttrs)
            {
                var parameterName = parameterAttr.ConstructorArguments.Length > 1 ? parameterAttr.ConstructorArguments[1].Value?.ToString() : propertySymbol.Name;

                // Generate code for SetValue
                sb.AppendLine($"    if (parameterName == \"{parameterName}\")");
                sb.AppendLine($"    {{");
                sb.AppendLine($"        this.{propertySymbol.Name} = ({propertySymbol.Type})value;");
                sb.AppendLine($"        return;");
                sb.AppendLine($"    }}");


            }
        }

        private static void GenerateGetValue(StringBuilder sb, IPropertySymbol propertySymbol)
        {
            var parameterAttrs = propertySymbol.GetAttributes().Where(attr => attr.AttributeClass?.Name == "ParameterAttribute");

            foreach (var parameterAttr in parameterAttrs)
            {
                var parameterName = parameterAttr.ConstructorArguments.Length > 1 ? parameterAttr.ConstructorArguments[1].Value?.ToString() : propertySymbol.Name;

                // Generate code for GetValue
                sb.AppendLine($"    if (parameterName == \"{parameterName}\")");
                sb.AppendLine($"    {{");
                sb.AppendLine($"        return this.{propertySymbol.Name};");
                sb.AppendLine($"    }}");
            }
        }

        private static void GenerateParameterCreation(StringBuilder sb, IPropertySymbol propertySymbol)
        {

            var parameterAttrs = propertySymbol.GetAttributes().Where(attr => attr.AttributeClass?.Name == "ParameterAttribute");


            // Extract ParameterAttribute info
            foreach (var parameterAttr in parameterAttrs)
            {
                var parameterType = parameterAttr.ConstructorArguments[0].Value?.ToString();
                var parameterName = parameterAttr.ConstructorArguments.Length > 1 ? parameterAttr.ConstructorArguments[1].Value?.ToString() : null;
                var parameterOrder = parameterAttr.ConstructorArguments.Length > 2 ? parameterAttr.ConstructorArguments[2].Value.ToString() : "1";
                sb.AppendLine($"        var param_{propertySymbol.Name.ToLower()} = new Parameter(\"{parameterType}\", \"{parameterName}\", {parameterOrder})" +
                                             $"{{DecodedType = typeof({propertySymbol.Type})}};");

                if (parameterType == "tuple")
                {
                    sb.AppendLine($"       if(param_{propertySymbol.Name.ToLower()}.ABIType is TupleType abiTupleType)");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            abiTupleType.SetComponents(new {propertySymbol.Type}().GetParameters().ToArray());");
                    sb.AppendLine("        }");
                }

                if (parameterType.IndexOf("[") > -1)
                {
                    var elementType = GetInnermostElementType(propertySymbol.Type);
                    sb.AppendLine($"       if(param_{propertySymbol.Name.ToLower()}.ABIType is ArrayType abiArrayType)");
                    sb.AppendLine("        {");
                    //convert this to sb.string each statement
                    sb.AppendLine("         while (abiArrayType != null)");
                    sb.AppendLine("         {");

                    sb.AppendLine("             if (abiArrayType.ElementType is TupleType arrayTupleType)");
                    sb.AppendLine("             {");
                    sb.AppendLine($"              arrayTupleType.SetComponents(new {elementType}().GetParameters().ToArray());");
                    sb.AppendLine("              abiArrayType = null;");
                    sb.AppendLine("              }");
                    sb.AppendLine("              else");
                    sb.AppendLine("              {");
                    sb.AppendLine("               abiArrayType = abiArrayType.ElementType as ArrayType;");
                    sb.AppendLine("              }");
                    sb.AppendLine("         }");
                    sb.AppendLine("        }");

                }
                sb.AppendLine($"        _parameters.Add(param_{propertySymbol.Name.ToLower()});");

            }

        }

        private static string GetInnermostElementType(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is INamedTypeSymbol namedTypeSymbol && namedTypeSymbol.IsGenericType)
            {
                // Get the first type argument
                var typeArgument = namedTypeSymbol.TypeArguments[0];

                // Recursively call this method to handle nested generic types
                return GetInnermostElementType(typeArgument);
            }

            // When a non-generic type is reached, return its name
            return typeSymbol.ToString(); // or typeSymbol.ToString() for the fully qualified name
        }
    }
}
