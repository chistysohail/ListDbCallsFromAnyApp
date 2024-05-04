using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Enter the path to the project folder:");
        string projectFolderPath = Console.ReadLine();

        Console.WriteLine("Choose an option:");
        Console.WriteLine("A. Analyze LINQ and Database Requests");
        Console.WriteLine("B. Analyze SqlCommands");

        char option = Console.ReadKey().KeyChar;
        Console.WriteLine(); // Move to the next line after user input

        switch (option)
        {
            case 'A':
                await AnalyzeLinqAndDatabaseRequests(projectFolderPath);
                break;
            case 'B':
                await AnalyzeSqlCommand(projectFolderPath);
                break;
            default:
                Console.WriteLine("Invalid option selected.");
                break;
        }
    }

    static async Task AnalyzeLinqAndDatabaseRequests(string projectFolderPath)
    {
        var outputFile = GetOutputFilePath(projectFolderPath, "LinqAndDatabaseRequests");
        var csvData = new List<string>();

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

                    // Getting project and file name only
                    var projectAndFileName = Path.Combine(Path.GetFileName(Path.GetDirectoryName(file)), Path.GetFileName(file));

                    csvData.Add($"Found method call '{methodName}' in file '{projectAndFileName}' at line {lineNumber}");
                }
            }

            var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
            foreach (var objectCreation in objectCreations)
            {
                var typeSymbol = (SemanticModel)await tree.GetSemanticModelAsync();
                if (typeSymbol.GetSymbolInfo(objectCreation).Symbol?.ToString() == "System.Data.SqlClient.SqlCommand")
                {
                    dbRequestCount++;

                    // Get the line number
                    var lineSpan = objectCreation.GetLocation().GetLineSpan();
                    var lineNumber = lineSpan.StartLinePosition.Line + 1; // Lines are 0-indexed

                    // Getting project and file name only
                    var projectAndFileName = Path.Combine(Path.GetFileName(Path.GetDirectoryName(file)), Path.GetFileName(file));

                    var spArgument = objectCreation.ArgumentList.Arguments.FirstOrDefault(a => a.Expression is LiteralExpressionSyntax);
                    if (spArgument != null)
                    {
                        var storedProcedureName = spArgument.Expression.ToString().Trim('"');
                        csvData.Add($"Found SqlCommand object creation for stored procedure '{storedProcedureName}' in file '{projectAndFileName}' at line {lineNumber}");
                    }
                }
            }
        }

        csvData.Add($"Total Database Requests Found: {dbRequestCount}");

        WriteToCsv(outputFile, csvData);
        Console.WriteLine($"Analysis result written to: {outputFile}");
    }

    static async Task AnalyzeSqlCommand(string projectFolderPath)
    {
        var outputFile = GetOutputFilePath(projectFolderPath, "SqlCommands");
        var csvData = new List<string>();

        var csFiles = Directory.EnumerateFiles(projectFolderPath, "*.cs", SearchOption.AllDirectories);

        int sqlCommandCount = 0;

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

                if (methodName == "SqlCommand")
                {
                    sqlCommandCount++;

                    // Get the line number
                    var lineSpan = methodCall.GetLocation().GetLineSpan();
                    var lineNumber = lineSpan.StartLinePosition.Line + 1; // Lines are 0-indexed

                    // Getting project and file name only
                    var projectAndFileName = Path.Combine(Path.GetFileName(Path.GetDirectoryName(file)), Path.GetFileName(file));

                    csvData.Add($"Found method call '{methodName}' in file '{projectAndFileName}' at line {lineNumber}");
                }
            }

            var objectCreations = root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>();
            foreach (var objectCreation in objectCreations)
            {
                var typeSymbol = (SemanticModel)await tree.GetSemanticModelAsync();
                if (typeSymbol.GetSymbolInfo(objectCreation).Symbol?.ToString() == "System.Data.SqlClient.SqlCommand")
                {
                    sqlCommandCount++;

                    // Get the line number
                    var lineSpan = objectCreation.GetLocation().GetLineSpan();
                    var lineNumber = lineSpan.StartLinePosition.Line + 1; // Lines are 0-indexed

                    // Getting project and file name only
                    var projectAndFileName = Path.Combine(Path.GetFileName(Path.GetDirectoryName(file)), Path.GetFileName(file));

                    var spArgument = objectCreation.ArgumentList.Arguments.FirstOrDefault(a => a.Expression is LiteralExpressionSyntax);
                    if (spArgument != null)
                    {
                        var storedProcedureName = spArgument.Expression.ToString().Trim('"');
                        csvData.Add($"Found SqlCommand object creation for stored procedure '{storedProcedureName}' in file '{projectAndFileName}' at line {lineNumber}");
                    }
                }
            }
        }

        csvData.Add($"Total SqlCommands Found: {sqlCommandCount}");

        WriteToCsv(outputFile, csvData);
        Console.WriteLine($"Analysis result written to: {outputFile}");
    }

    static string GetOutputFilePath(string projectFolderPath, string analysisType)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        string fileName = $"{analysisType}_{timestamp}.csv";
        return Path.Combine(projectFolderPath, fileName);
    }

    static void WriteToCsv(string filePath, List<string> data)
    {
        using (StreamWriter writer = new StreamWriter(filePath))
        {
            foreach (var line in data)
            {
                writer.WriteLine(line);
            }
        }
    }
}
