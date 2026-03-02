using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

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
    private const string NameColumnFormat = "{0,-25} |";
    
    private class LanguageInfo
    {
        public required string Name { get; init; }
        public required string[] LineComments { get; init; }
        public required (string Start, string End)[] BlockComments { get; init; }
        public bool IsProgrammingLanguage { get; init; } = false;
    }

    private static readonly Dictionary<string, LanguageInfo> LanguageConfigs = new()
    {
        // C-style languages
        [".cs"] = new() { Name = "C#", LineComments = new[] { "//" }, BlockComments = new[] { ("/*", "*/") }, IsProgrammingLanguage = true },
        [".java"] = new() { Name = "Java", LineComments = new[] { "//" }, BlockComments = new[] { ("/*", "*/") }, IsProgrammingLanguage = true },
        [".js"] = new() { Name = "JavaScript", LineComments = new[] { "//" }, BlockComments = new[] { ("/*", "*/") }, IsProgrammingLanguage = true },
        [".ts"] = new() { Name = "TypeScript", LineComments = new[] { "//" }, BlockComments = new[] { ("/*", "*/") }, IsProgrammingLanguage = true },
        [".cpp"] = new() { Name = "C++", LineComments = new[] { "//" }, BlockComments = new[] { ("/*", "*/") }, IsProgrammingLanguage = true },
        [".c"] = new() { Name = "C", LineComments = new[] { "//" }, BlockComments = new[] { ("/*", "*/") }, IsProgrammingLanguage = true },
        
        // Script languages
        [".py"] = new() { Name = "Python", LineComments = new[] { "#" }, BlockComments = new[] { ("\"\"\"", "\"\"\""), ("'''", "'''") }, IsProgrammingLanguage = true },
        [".rb"] = new() { Name = "Ruby", LineComments = new[] { "#" }, BlockComments = new[] { ("=begin", "=end") }, IsProgrammingLanguage = true },
        [".php"] = new() { Name = "PHP", LineComments = new[] { "//", "#" }, BlockComments = new[] { ("/*", "*/") }, IsProgrammingLanguage = true },
        
        // Shell scripts and configuration
        [".sh"] = new() { Name = "Shell", LineComments = new[] { "#" }, BlockComments = new (string, string)[] { } },
        [".bash"] = new() { Name = "Shell", LineComments = new[] { "#" }, BlockComments = new (string, string)[] { } },
        [".ps1"] = new() { Name = "PowerShell", LineComments = new[] { "#" }, BlockComments = new[] { ("<#", "#>") } },
        [".yaml"] = new() { Name = "YAML", LineComments = new[] { "#" }, BlockComments = new (string, string)[] { } },
        [".yml"] = new() { Name = "YAML", LineComments = new[] { "#" }, BlockComments = new (string, string)[] { } },
        
        // Web and markup technologies
        [".html"] = new() { Name = "HTML", LineComments = new string[] { }, BlockComments = new[] { ("<!--", "-->") } },
        [".xml"] = new() { Name = "XML", LineComments = new string[] { }, BlockComments = new[] { ("<!--", "-->") } },
        [".xsd"] = new() { Name = "XML Schema", LineComments = new string[] { }, BlockComments = new[] { ("<!--", "-->") } },
        [".targets"] = new() { Name = "MSBuild Targets", LineComments = new string[] { }, BlockComments = new[] { ("<!--", "-->") } },
        [".csproj"] = new() { Name = "Visual Studio Project", LineComments = new string[] { }, BlockComments = new[] { ("<!--", "-->") } },
        [".sln"] = new() { Name = "Visual Studio Solution", LineComments = new string[] { }, BlockComments = new (string, string)[] { } },
        [".css"] = new() { Name = "CSS", LineComments = new string[] { }, BlockComments = new[] { ("/*", "*/") } },
        [".md"] = new() { Name = "Markdown", LineComments = new string[] { }, BlockComments = new (string, string)[] { } },
        
        // Other programming languages
        [".sql"] = new() { Name = "SQL", LineComments = new[] { "--" }, BlockComments = new[] { ("/*", "*/") }, IsProgrammingLanguage = true },
        [".lua"] = new() { Name = "Lua", LineComments = new[] { "--" }, BlockComments = new[] { ("--[[", "]]") }, IsProgrammingLanguage = true },
        [".go"] = new() { Name = "Go", LineComments = new[] { "//" }, BlockComments = new[] { ("/*", "*/") }, IsProgrammingLanguage = true }
    };

    private record struct LineStats(int TotalLines, int NonBlankLines, int CommentLines, int CommentedCodeLines, int FileCount);
    
    private class ProgressInfo
    {
        public int CurrentFile { get; set; }
        public int TotalFiles { get; set; }
        public string CurrentFileName { get; set; } = "";
    }

    private static string CreateProgressBar(int current, int total, int width = 40)
    {
        float percentage = (float)current / total;
        int filled = (int)(width * percentage);
        string bar = new string('█', filled) + new string('░', width - filled);
        return $"[{bar}] {percentage:P0}";
    }

    private static void UpdateProgress(ProgressInfo info)
    {
        var progressBar = CreateProgressBar(info.CurrentFile, info.TotalFiles);
        var status = $"{info.CurrentFile}/{info.TotalFiles} files";
        var fileName = info.CurrentFileName;
        
        // Ensure the display fits within the console width
        var maxFileNameLength = Math.Max(10, Console.WindowWidth - progressBar.Length - status.Length - 5);
        if (fileName.Length > maxFileNameLength)
        {
            fileName = "..." + fileName.Substring(fileName.Length - maxFileNameLength + 3);
        }

        var display = $"\r{progressBar} {status} | {fileName}";
        Console.Write(display.PadRight(Console.WindowWidth - 1));
    }

    static async Task Main(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return;
        }

        if (args.Contains("--version") || args.Contains("-v"))
        {
            PrintVersion();
            return;
        }

        Console.WriteLine("locc - Lines of Code Counter");
        Console.WriteLine("---------------------------");

        bool showBlankLines = args.Contains("--show-blank-lines");
        bool showByFileType = args.Contains("--by-extension");
        bool showProgrammingTotal = args.Contains("--show-languages-total");
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

        Console.WriteLine("Scanning repository...");
        var progress = new Progress<ProgressInfo>(UpdateProgress);

        var stats = await CountLinesInRepository(repoPath, progress);
        Console.WriteLine(); // Clear the progress line
        PrintResults(stats, showBlankLines, showByFileType, showProgrammingTotal);
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

    private static void PrintHelp()
    {
        Console.WriteLine("locc - Lines of Code Counter");
        Console.WriteLine("Usage:");
        Console.WriteLine("  locc [path] [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help                 Show help and exit");
        Console.WriteLine("  -v, --version              Show version and exit");
        Console.WriteLine("  --show-blank-lines          Include blank/total columns in output");
        Console.WriteLine("  --by-extension              Group results by file extension");
        Console.WriteLine("  --show-languages-total      Show total for programming languages");
        Console.WriteLine();
        Console.WriteLine("Notes:");
        Console.WriteLine("  If [path] is omitted, the tool will prompt for a repository path.");
        Console.WriteLine();
        Console.WriteLine("Programming languages (for --show-languages-total):");
        var programmingLanguages = LanguageConfigs.Values
            .Where(lang => lang.IsProgrammingLanguage)
            .Select(lang => lang.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToArray();
        Console.WriteLine($"  {string.Join(", ", programmingLanguages)}");
    }

    private static async Task<Dictionary<string, LineStats>> CountLinesInRepository(string repoPath, IProgress<ProgressInfo>? progress = null)
    {
        var stats = new Dictionary<string, LineStats>();
        var files = GetCodeFiles(repoPath).ToList();
        var totalFiles = files.Count;
        var currentFile = 0;

        foreach (var file in files)
        {
            currentFile++;
            progress?.Report(new ProgressInfo 
            { 
                CurrentFile = currentFile, 
                TotalFiles = totalFiles,
                CurrentFileName = Path.GetFileName(file)
            });

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

    private static void PrintResults(Dictionary<string, LineStats> stats, bool showBlankLines, bool showByFileType, bool showProgrammingTotal)
    {
        if (!stats.Any())
        {
            Console.WriteLine("No code files found!");
            return;
        }

        var totalStats = new LineStats(0, 0, 0, 0, 0);
        var programmingTotalStats = new LineStats(0, 0, 0, 0, 0);

        // Print header
        Console.WriteLine();
        Console.Write(string.Format(NameColumnFormat, showByFileType ? "Extension" : "Language"));
        Console.Write(string.Format(FileColumnFormat, "Files"));
        Console.Write(string.Format(ColumnFormat, "Code"));
        Console.Write(string.Format(ColumnFormat, "Comments"));
        Console.Write(string.Format(ColumnFormat, "Com. Code"));
        Console.Write(string.Format(ColumnFormat, "Source"));
        if (showBlankLines)
        {
            Console.Write(string.Format(ColumnFormat, "Blank"));
            Console.Write(string.Format(ColumnFormat, "Total"));
        }
        Console.WriteLine();

        Console.WriteLine(new string('-', showBlankLines ? 125 : 95));

        // Print stats for each language/extension
        foreach (var (ext, fileStats) in stats.OrderByDescending(s => s.Value.NonBlankLines))
        {
            var langInfo = LanguageConfigs.GetValueOrDefault(ext);
            var displayName = showByFileType ? ext : (langInfo?.Name ?? GetOtherDisplayName(ext));
            
            PrintStatsLine(displayName, fileStats, showBlankLines);

            // Update totals
            totalStats = AddStats(totalStats, fileStats);
            
            // Update programming language totals
            if (langInfo?.IsProgrammingLanguage == true)
            {
                programmingTotalStats = AddStats(programmingTotalStats, fileStats);
            }
        }

        Console.WriteLine(new string('-', showBlankLines ? 125 : 95));
        
        // Print programming languages total
        if (showProgrammingTotal && programmingTotalStats.FileCount > 0)
        {
            PrintStatsLine("Programming Total", programmingTotalStats, showBlankLines);
        }

        // Print overall total
        PrintStatsLine("Total", totalStats, showBlankLines);
    }

    private static LineStats AddStats(LineStats a, LineStats b)
    {
        return new LineStats(
            a.TotalLines + b.TotalLines,
            a.NonBlankLines + b.NonBlankLines,
            a.CommentLines + b.CommentLines,
            a.CommentedCodeLines + b.CommentedCodeLines,
            a.FileCount + b.FileCount
        );
    }

    private static string GetOtherDisplayName(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return "Other";

        return $"Other ({extension})";
    }

    private static void PrintStatsLine(string name, LineStats stats, bool showBlankLines)
    {
        var sourceLines = stats.NonBlankLines - stats.CommentLines - stats.CommentedCodeLines;
        Console.Write(string.Format(NameColumnFormat, name));
        Console.Write(string.Format(FileColumnFormat, stats.FileCount));
        Console.Write(string.Format(ColumnFormat, sourceLines));
        Console.Write(string.Format(ColumnFormat, stats.CommentLines));
        Console.Write(string.Format(ColumnFormat, stats.CommentedCodeLines));
        Console.Write(string.Format(ColumnFormat, stats.NonBlankLines));
        if (showBlankLines)
        {
            var blankLines = stats.TotalLines - stats.NonBlankLines;
            Console.Write(string.Format(ColumnFormat, blankLines));
            Console.Write(string.Format(ColumnFormat, stats.TotalLines));
        }
        Console.WriteLine();
    }
} 