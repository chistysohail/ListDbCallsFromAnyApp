using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Replace with the path of the project you want to analyze
        string projectFolderPath = @"Path\To\Your\Project";

        var csFiles = Directory.EnumerateFiles(projectFolderPath, "*.cs", SearchOption.AllDirectories);

        int dbRequestCount = 0;

        string[] linqMethods = { "Where", "Select", "GroupBy" }; // Add other LINQ methods as needed.

        foreach (var file in csFiles)
        {
            var code = await File.ReadAllTextAsync(file);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = await tree.GetRootAsync();

            var methodCalls = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var methodCall in methodCalls)
            {
                var memberAccessExpr = methodCall.Expression as MemberAccessExpressionSyntax;
                var methodName = memberAccessExpr?.Name.ToString();

                if (methodName == "FromSqlRaw" || methodName == "ExecuteSqlRaw" || methodName == "ExecuteSqlCommand" || linqMethods.Contains(methodName))
                {
                    dbRequestCount++;

                    // Get the line number
                    var lineSpan = methodCall.GetLocation().GetLineSpan();
                    var lineNumber = lineSpan.StartLinePosition.Line + 1; // Lines are 0-indexed

                    //Console.WriteLine($"Found method call '{methodName}' in file '{file}' at line {lineNumber}");

                    // Getting project and file name only
                    var projectAndFileName = Path.Combine(Path.GetFileName(Path.GetDirectoryName(file)), Path.GetFileName(file));
                  
                    Console.WriteLine($"Found method call '{methodName}' in file '{projectAndFileName}' at line {lineNumber}");
                }
            }
        }

        Console.WriteLine($"Total Database Requests Found: {dbRequestCount}");
    }
}
