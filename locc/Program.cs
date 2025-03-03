using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

class Program
{
    private static readonly string[] ExcludedDirectories = { ".git", "bin", "obj", "node_modules", "packages", ".vs" };
    private static readonly string[] ExcludedExtensions = { 
        ".exe", ".dll", ".pdb", ".gitignore", ".gitattributes",
        ".suo", ".vsidx", ".v2", ".cache", ".bin",
        ".user", ".lock", ".ide", ".resources"
    };
    private const string ColumnFormat = " {0,10} ";
    private const string FileColumnFormat = " {0,8} ";
    private const string NameColumnFormat = "{0,-20} |";
    
    private class LanguageInfo
    {
        public required string Name { get; init; }
        public required string[] LineComments { get; init; }
        public required (string Start, string End)[] BlockComments { get; init; }
    }

    private static readonly Dictionary<string, LanguageInfo> LanguageConfigs = new()
    {
        // C-style languages
        [".cs"] = new() { Name = "C#", LineComments = new[] { "//" }, BlockComments = new[] { ("/*", "*/") } },
        [".java"] = new() { Name = "Java", LineComments = new[] { "//" }, BlockComments = new[] { ("/*", "*/") } },
        [".js"] = new() { Name = "JavaScript", LineComments = new[] { "//" }, BlockComments = new[] { ("/*", "*/") } },
        [".ts"] = new() { Name = "TypeScript", LineComments = new[] { "//" }, BlockComments = new[] { ("/*", "*/") } },
        [".cpp"] = new() { Name = "C++", LineComments = new[] { "//" }, BlockComments = new[] { ("/*", "*/") } },
        [".c"] = new() { Name = "C", LineComments = new[] { "//" }, BlockComments = new[] { ("/*", "*/") } },
        
        // Script languages
        [".py"] = new() { Name = "Python", LineComments = new[] { "#" }, BlockComments = new[] { ("\"\"\"", "\"\"\""), ("'''", "'''") } },
        [".rb"] = new() { Name = "Ruby", LineComments = new[] { "#" }, BlockComments = new[] { ("=begin", "=end") } },
        [".php"] = new() { Name = "PHP", LineComments = new[] { "//", "#" }, BlockComments = new[] { ("/*", "*/") } },
        
        // Shell scripts
        [".sh"] = new() { Name = "Shell", LineComments = new[] { "#" }, BlockComments = new (string, string)[] { } },
        [".bash"] = new() { Name = "Shell", LineComments = new[] { "#" }, BlockComments = new (string, string)[] { } },
        [".ps1"] = new() { Name = "PowerShell", LineComments = new[] { "#" }, BlockComments = new[] { ("<#", "#>") } },
        
        // Web technologies
        [".html"] = new() { Name = "HTML", LineComments = new string[] { }, BlockComments = new[] { ("<!--", "-->") } },
        [".xml"] = new() { Name = "XML", LineComments = new string[] { }, BlockComments = new[] { ("<!--", "-->") } },
        [".xsd"] = new() { Name = "XML Schema", LineComments = new string[] { }, BlockComments = new[] { ("<!--", "-->") } },
        [".targets"] = new() { Name = "MSBuild Targets", LineComments = new string[] { }, BlockComments = new[] { ("<!--", "-->") } },
        [".csproj"] = new() { Name = "VS Project", LineComments = new string[] { }, BlockComments = new[] { ("<!--", "-->") } },
        [".css"] = new() { Name = "CSS", LineComments = new string[] { }, BlockComments = new[] { ("/*", "*/") } },
        
        // Other common languages
        [".sql"] = new() { Name = "SQL", LineComments = new[] { "--" }, BlockComments = new[] { ("/*", "*/") } },
        [".lua"] = new() { Name = "Lua", LineComments = new[] { "--" }, BlockComments = new[] { ("--[[", "]]") } },
        [".go"] = new() { Name = "Go", LineComments = new[] { "//" }, BlockComments = new[] { ("/*", "*/") } }
    };

    private record struct LineStats(int TotalLines, int NonBlankLines, int CommentLines, int CommentedCodeLines, int FileCount);

    static async Task Main(string[] args)
    {
        if (args.Contains("--version") || args.Contains("-v"))
        {
            PrintVersion();
            return;
        }

        Console.WriteLine("locc - Lines of Code Counter");
        Console.WriteLine("---------------------------");

        bool showBlankLines = args.Contains("--show-blank-lines");
        bool showByFileType = args.Contains("--by-extension");
        string repoPath;

        var pathArgs = args.Where(arg => !arg.StartsWith("--") && arg != "-v").ToArray();
        if (pathArgs.Length > 0)
        {
            repoPath = pathArgs[0];
        }
        else
        {
            Console.Write("Enter the path to your git repository: ");
            repoPath = Console.ReadLine()?.Trim() ?? "";
        }

        if (!Directory.Exists(repoPath))
        {
            Console.WriteLine("Error: Directory does not exist!");
            return;
        }

        if (!Directory.Exists(Path.Combine(repoPath, ".git")))
        {
            Console.WriteLine("Warning: This doesn't appear to be a git repository!");
            Console.Write("Continue anyway? (y/n): ");
            if (Console.ReadLine()?.ToLower() != "y")
                return;
        }

        var stats = await CountLinesInRepository(repoPath);
        PrintResults(stats, showBlankLines, showByFileType);
    }

    private static void PrintVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        var title = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "locc";
        var description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description 
            ?? "A simple and fast lines of code counter for git repositories";
        
        Console.WriteLine($"{title} version {version}");
        Console.WriteLine(description);
    }

    private static async Task<Dictionary<string, LineStats>> CountLinesInRepository(string repoPath)
    {
        var stats = new Dictionary<string, LineStats>();
        var files = GetCodeFiles(repoPath);

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file).ToLower();
            var fileStats = await CountLinesInFile(file);

            if (!stats.ContainsKey(extension))
                stats[extension] = new LineStats(0, 0, 0, 0, 0);
            
            var current = stats[extension];
            stats[extension] = new LineStats(
                current.TotalLines + fileStats.TotalLines,
                current.NonBlankLines + fileStats.NonBlankLines,
                current.CommentLines + fileStats.CommentLines,
                current.CommentedCodeLines + fileStats.CommentedCodeLines,
                current.FileCount + 1
            );
        }

        return stats;
    }

    private static IEnumerable<string> GetCodeFiles(string path)
    {
        return Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(file => 
                !ExcludedDirectories.Any(dir => file.Contains($"{Path.DirectorySeparatorChar}{dir}{Path.DirectorySeparatorChar}")) &&
                !ExcludedExtensions.Contains(Path.GetExtension(file).ToLower()));
    }

    private static async Task<LineStats> CountLinesInFile(string filePath)
    {
        try
        {
            var extension = Path.GetExtension(filePath).ToLower();
            var lines = await File.ReadAllLinesAsync(filePath);
            var nonBlankCount = 0;
            var commentCount = 0;
            var commentedCodeCount = 0;
            var inBlockComment = false;
            var currentBlockComment = ("", "");

            // Get language configuration
            var langConfig = LanguageConfigs.GetValueOrDefault(extension);
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                nonBlankCount++;

                if (langConfig != null)
                {
                    // Check if we're in a block comment
                    if (inBlockComment)
                    {
                        commentCount++;
                        if (line.Contains(currentBlockComment.Item2))
                        {
                            inBlockComment = false;
                        }
                        continue;
                    }

                    // Check for start of block comments
                    if (langConfig.BlockComments != null)
                    {
                        foreach (var blockComment in langConfig.BlockComments)
                        {
                            if (line.Contains(blockComment.Start))
                            {
                                inBlockComment = true;
                                currentBlockComment = blockComment;
                                commentCount++;
                                
                                if (line.Contains(blockComment.End))
                                {
                                    inBlockComment = false;
                                }
                                break;
                            }
                        }
                        if (inBlockComment) continue;
                    }

                    // Check for line comments
                    if (langConfig.LineComments != null)
                    {
                        foreach (var commentStart in langConfig.LineComments)
                        {
                            if (line.StartsWith(commentStart))
                            {
                                // Check if this might be commented-out code
                                var uncommentedLine = line.Substring(commentStart.Length).Trim();
                                if (LooksLikeCode(uncommentedLine, langConfig))
                                {
                                    commentedCodeCount++;
                                }
                                else
                                {
                                    commentCount++;
                                }
                                break;
                            }
                        }
                    }
                }
            }

            return new LineStats(lines.Length, nonBlankCount, commentCount, commentedCodeCount, 1);
        }
        catch (Exception)
        {
            Console.WriteLine($"Warning: Could not read file {filePath}");
            return new LineStats(0, 0, 0, 0, 0);
        }
    }

    private static bool LooksLikeCode(string line, LanguageInfo langConfig)
    {
        // Simple heuristics to detect if a commented line looks like code
        if (string.IsNullOrWhiteSpace(line)) return false;

        // Common code indicators
        var codeIndicators = new[]
        {
            @"^[a-zA-Z_][a-zA-Z0-9_]*\s*\(", // Function calls
            @"^(if|for|while|switch|return|break|continue)\b", // Keywords
            @"^[a-zA-Z_][a-zA-Z0-9_]*\s*=", // Assignment
            @"^(public|private|protected|class|void|int|string)\b", // Common type declarations
            @"[{};]$", // Code block markers
            @"^\s*\{|\}\s*$" // Brackets alone
        };

        return codeIndicators.Any(pattern => Regex.IsMatch(line, pattern));
    }

    private static void PrintResults(Dictionary<string, LineStats> stats, bool showBlankLines, bool showByFileType)
    {
        var groupedStats = showByFileType
            ? stats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            : GroupStatsByLanguage(stats);

        Console.WriteLine($"\nLines of Code by {(showByFileType ? "File Type" : "Language")}:");
        Console.WriteLine();

        // Print header
        PrintTableHeader(showBlankLines);

        var totalStats = new LineStats(0, 0, 0, 0, 0);

        // Print each language/file type
        foreach (var stat in groupedStats.OrderByDescending(x => x.Value.NonBlankLines - x.Value.CommentLines - x.Value.CommentedCodeLines))
        {
            PrintTableRow(stat.Key, stat.Value, showBlankLines);
            
            totalStats = new LineStats(
                totalStats.TotalLines + stat.Value.TotalLines,
                totalStats.NonBlankLines + stat.Value.NonBlankLines,
                totalStats.CommentLines + stat.Value.CommentLines,
                totalStats.CommentedCodeLines + stat.Value.CommentedCodeLines,
                totalStats.FileCount + stat.Value.FileCount
            );
        }

        // Print separator
        PrintTableSeparator(showBlankLines);

        // Print totals
        PrintTableRow("Total", totalStats, showBlankLines, true);
    }

    private static Dictionary<string, LineStats> GroupStatsByLanguage(Dictionary<string, LineStats> stats)
    {
        var result = new Dictionary<string, LineStats>();
        
        foreach (var stat in stats)
        {
            var extension = stat.Key;
            var langConfig = LanguageConfigs.GetValueOrDefault(extension);
            var langName = langConfig?.Name ?? extension.TrimStart('.').ToUpper();
            
            if (!result.ContainsKey(langName))
                result[langName] = new LineStats(0, 0, 0, 0, 0);
            
            var current = result[langName];
            result[langName] = new LineStats(
                current.TotalLines + stat.Value.TotalLines,
                current.NonBlankLines + stat.Value.NonBlankLines,
                current.CommentLines + stat.Value.CommentLines,
                current.CommentedCodeLines + stat.Value.CommentedCodeLines,
                current.FileCount + stat.Value.FileCount
            );
        }
        
        return result;
    }

    private static void PrintTableHeader(bool showBlankLines)
    {
        PrintTableSeparator(showBlankLines);
        Console.Write(NameColumnFormat, "Language");
        Console.Write(FileColumnFormat, "Files");
        Console.Write(ColumnFormat, "Code");
        Console.Write(ColumnFormat, "Comments");
        Console.Write(ColumnFormat, "Com. Code");
        Console.Write(ColumnFormat, "Source");
        if (showBlankLines)
        {
            Console.Write(ColumnFormat, "Blank");
            Console.Write(ColumnFormat, "Total");
        }
        Console.WriteLine();
        PrintTableSeparator(showBlankLines);
    }

    private static void PrintTableSeparator(bool showBlankLines)
    {
        Console.Write("----------------------+");
        var remainingWidth = 11 + 11 + 11 + 11 + 11; // Files, Code, Comments, Com. Code, Source
        if (showBlankLines)
        {
            remainingWidth += 22; // Blank and Total columns
        }
        Console.Write(new string('-', remainingWidth));
        Console.WriteLine();
    }

    private static void PrintTableRow(string name, LineStats stats, bool showBlankLines, bool isTotal = false)
    {
        var actualCode = stats.NonBlankLines - stats.CommentLines - stats.CommentedCodeLines;
        var blankLines = stats.TotalLines - stats.NonBlankLines;
        var sourceLines = actualCode + stats.CommentLines + stats.CommentedCodeLines;

        if (isTotal)
        {
            PrintTableSeparator(showBlankLines);
        }

        Console.Write(NameColumnFormat, name);
        Console.Write(FileColumnFormat, stats.FileCount);
        Console.Write(ColumnFormat, actualCode);
        Console.Write(ColumnFormat, stats.CommentLines);
        Console.Write(ColumnFormat, stats.CommentedCodeLines);
        Console.Write(ColumnFormat, sourceLines);
        
        if (showBlankLines)
        {
            Console.Write(ColumnFormat, blankLines);
            Console.Write(ColumnFormat, stats.TotalLines);
        }
        
        Console.WriteLine();
    }
} 