# locc - Lines of Code Counter

A simple and fast lines of code counter for git repositories.

## Building from Source

```powershell
# Clone the repository
git clone https://github.com/yourusername/LinesofCodeCounter.git
cd LinesofCodeCounter

# Build and publish (self-contained executable)
dotnet publish locc.csproj -c Release -r win-x64 --self-contained true

# The executable will be in bin\Release\net9.0\win-x64\publish\locc.exe
```

## Features

- Counts lines of code in all files within a git repository
- Groups code by programming language (with option to show by file extension)
- Separates actual code, comments, and commented-out code
- Detects both single-line and multi-line comments
- Excludes common non-code directories (.git, bin, obj, etc.)
- Shows total lines of code across all files
- Optional display of blank line statistics
- Table-formatted output for better readability
- Real-time progress display while processing files

## Requirements

- .NET 9.0 SDK or later

## Building and Publishing

### Building Single Executable

1. For Windows x64:
   ```
   dotnet publish -c Release -r win-x64 --self-contained true
   ```
   The executable will be in `bin/Release/net9.0/win-x64/publish/locc.exe`

2. For Linux x64:
   ```
   dotnet publish -c Release -r linux-x64 --self-contained true
   ```
   The executable will be in `bin/Release/net9.0/linux-x64/publish/locc`

3. For macOS x64:
   ```
   dotnet publish -c Release -r osx-x64 --self-contained true
   ```
   The executable will be in `bin/Release/net9.0/osx-x64/publish/locc`

The generated executable is self-contained and doesn't require .NET runtime installation.

### Publishing as .NET Tool

If you prefer to distribute as a .NET tool:

1. Pack as a NuGet package:
   ```
   dotnet pack -c Release
   ```

2. Install locally from the built package:
   ```
   dotnet tool install --global --add-source ./nupkg locc
   ```

### Publishing to NuGet

1. Create a NuGet API key at https://www.nuget.org

2. Build and publish:
   ```
   dotnet pack -c Release
   dotnet nuget push ./nupkg/locc.1.0.0.nupkg --api-key YOUR_API_KEY --source https://api.nuget.org/v3/index.json
   ```

### Installing from NuGet

Once published, users can install directly from NuGet:
```
dotnet tool install -g locc
```

To update to a newer version:
```
dotnet tool update -g locc
```

To uninstall:
```
dotnet tool uninstall -g locc
```

## Usage

```powershell
# Count lines in current directory
.\locc.exe

# Count lines in specific directory
.\locc.exe C:\path\to\repository

# Show blank lines in output
.\locc.exe --show-blank-lines

# Show statistics by file extension instead of by language
.\locc.exe --by-extension

# Show total for programming languages only
.\locc.exe --show-languages-total

# Show help
.\locc.exe --help

# Show version
.\locc.exe --version
.\locc.exe -v
```

## Output Format

| Language | Files | Code | Comments | Com. Code | Source | Blank | Total |
|----------|-------|------|----------|-----------|--------|-------|-------|
| C#       | 10    | 1000 | 200      | 50        | 1250   | 300   | 1550  |
| ...      | ...   | ...  | ...      | ...       | ...    | ...   | ...   |

- `Language`: Programming language or file type
- `Files`: Number of files
- `Code`: Lines of actual code
- `Comments`: Lines of comments
- `Com. Code`: Lines of commented-out code
- `Source`: Total meaningful lines (Code + Comments + Com. Code)
- `Blank`: Number of blank lines (shown with --show-blank-lines)
- `Total`: Total number of lines (shown with --show-blank-lines)

## Supported Languages

- C-style: C#, Java, JavaScript, TypeScript, C++, C
- Scripts: Python, Ruby, PHP
- Shell: Bash, PowerShell
- Web: HTML, XML, CSS, XML Schema, MSBuild Targets, VS Project
- Other: SQL, Lua, Go

## Configuration

The tool automatically excludes:
- Common directories: `.git`, `bin`, `obj`, `node_modules`, `packages`, `.vs`
- Binary and system files: `.exe`, `.dll`, `.pdb`, `.suo`, etc.
- Source control files: `.gitignore`, `.gitattributes`

## Command Line Arguments

- `[repository_path]`: Path to the git repository to analyze
- `--by-extension`: Show results grouped by file extension instead of programming language
- `--show-blank-lines`: Show blank line counts and file totals
- `--show-languages-total`: Show separate total for programming languages (excluding markup, config files, etc.)
- `--version`, `-v`: Display version information

Programming languages used for `--show-languages-total`:
C#, Java, JavaScript, TypeScript, C++, C, Python, Ruby, PHP, SQL, Lua, Go

## Comment Detection

The tool recognizes:
- Single-line comments (e.g., //, #, --)
- Multi-line block comments (e.g., /* */, """ """, <!-- -->)
- Commented-out code (detected using language-specific heuristics)

## Supported Languages and Comment Styles

The tool recognizes various comment styles for different languages:

- C-style languages (C#, Java, JavaScript, TypeScript, C++, C)
  - Single-line: //
  - Multi-line: /* */

- Python
  - Single-line: #
  - Multi-line: """ """ or ''' '''

- Ruby
  - Single-line: #
  - Multi-line: =begin/=end

- PHP
  - Single-line: // or #
  - Multi-line: /* */

- Shell Scripts
  - Single-line: #

- PowerShell
  - Single-line: #
  - Multi-line: <# #>

- HTML
  - Multi-line: <!-- -->

- XML
  - Multi-line: <!-- -->

- CSS
  - Multi-line: /* */

And many more...

Files with unrecognized extensions will be grouped under "Other" with basic comment detection.

## Excluded Directories

The following directories are excluded from the count:
- `.git` directory
- `bin` and `obj` directories
- `node_modules` directory
- `packages` directory 