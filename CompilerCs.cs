using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace ASIAndDLLMaker.Core
{
    public class CompilerCs
    {
        public event Action<string> OnLogReceived;
        public event Action<bool, string> OnCompilationFinished;

        private void Log(string message)
        {
            OnLogReceived?.Invoke(message);
        }

        public async Task CompileAsync(string editorCode, string[] inputFiles, string outputPath, int shvdnVersion, string projectName = "CustomScript")
        {
            Log("Starting C# Script compilation...");
            Log($"Targeting ScriptHookVDotNet version {shvdnVersion}");

            await Task.Run(() =>
            {
                try
                {
                    // 1. Resolve .NET Framework reference paths
                    string netFrameworkFolder = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319";
                    if (!Directory.Exists(netFrameworkFolder))
                    {
                        netFrameworkFolder = @"C:\Windows\Microsoft.NET\Framework\v4.0.30319";
                    }

                    if (!Directory.Exists(netFrameworkFolder))
                    {
                        Log("Error: Could not locate .NET Framework 4.8 installation directory.");
                        OnCompilationFinished?.Invoke(false, null);
                        return;
                    }

                    // 2. Prepare SHVDN reference DLL
                    string tempDir = Path.Combine(Path.GetTempPath(), "ASIAndDLLMaker_Cs_" + Guid.NewGuid().ToString().Substring(0, 8));
                    Directory.CreateDirectory(tempDir);

                    string shvdnDllName = shvdnVersion == 2 ? "ScriptHookVDotNet2.dll" : "ScriptHookVDotNet3.dll";
                    string shvdnTempPath = Path.Combine(tempDir, shvdnDllName);

                    ExtractSHVDNResource(shvdnDllName, shvdnTempPath);

                    // 3. Create Syntax Trees
                    var syntaxTrees = new System.Collections.Generic.List<SyntaxTree>();
                    if (inputFiles == null || inputFiles.Length == 0)
                    {
                        syntaxTrees.Add(CSharpSyntaxTree.ParseText(editorCode));
                        Log("Using script code from editor.");
                    }
                    else if (inputFiles.Length == 1)
                    {
                        syntaxTrees.Add(CSharpSyntaxTree.ParseText(editorCode));
                        Log($"Loaded script file: {Path.GetFileName(inputFiles[0])}");
                    }
                    else
                    {
                        Log($"Parsing {inputFiles.Length} C# source files...");
                        foreach (var file in inputFiles)
                        {
                            if (File.Exists(file))
                            {
                                syntaxTrees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(file)));
                                Log($"  Parsed file: {Path.GetFileName(file)}");
                            }
                        }
                    }

                    // 4. Set up compiler references
                    string[] refAssemblies = {
                        "mscorlib.dll",
                        "System.dll",
                        "System.Core.dll",
                        "System.Windows.Forms.dll",
                        "System.Drawing.dll",
                        "System.Data.dll",
                        "System.Xml.dll",
                        "System.Xml.Linq.dll"
                    };

                    var references = refAssemblies
                        .Select(name => MetadataReference.CreateFromFile(Path.Combine(netFrameworkFolder, name)))
                        .ToList();

                    // Add SHVDN reference
                    references.Add(MetadataReference.CreateFromFile(shvdnTempPath));

                    // 5. Compile options
                    var compilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                        .WithOptimizationLevel(OptimizationLevel.Release)
                        .WithAssemblyIdentityComparer(DesktopAssemblyIdentityComparer.Default);

                    var compilation = CSharpCompilation.Create(
                        projectName,
                        syntaxTrees,
                        references,
                        compilationOptions);

                    // 6. Emit to destination DLL
                    string destDir = Path.GetDirectoryName(outputPath);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    Log("Emitting DLL...");
                    EmitResult emitResult = compilation.Emit(outputPath);

                    // 7. Parse compile results
                    if (emitResult.Success)
                    {
                        Log($"Success! Compilation complete. DLL saved to: {outputPath}");
                        OnCompilationFinished?.Invoke(true, outputPath);
                    }
                    else
                    {
                        Log("Compilation failed with errors:");
                        foreach (Diagnostic diagnostic in emitResult.Diagnostics)
                        {
                            if (diagnostic.Severity == DiagnosticSeverity.Error)
                            {
                                var lineSpan = diagnostic.Location.GetLineSpan();
                                int line = lineSpan.StartLinePosition.Line + 1;
                                int col = lineSpan.StartLinePosition.Character + 1;
                                Log($"[Error] (Line {line}, Col {col}) {diagnostic.Id}: {diagnostic.GetMessage()}");
                            }
                            else if (diagnostic.Severity == DiagnosticSeverity.Warning)
                            {
                                var lineSpan = diagnostic.Location.GetLineSpan();
                                int line = lineSpan.StartLinePosition.Line + 1;
                                int col = lineSpan.StartLinePosition.Character + 1;
                                Log($"[Warning] (Line {line}, Col {col}) {diagnostic.Id}: {diagnostic.GetMessage()}");
                            }
                        }
                        OnCompilationFinished?.Invoke(false, null);
                    }

                    // Clean up temp SHVDN references
                    try
                    {
                        if (Directory.Exists(tempDir))
                            Directory.Delete(tempDir, true);
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    Log($"Critical Exception during compilation: {ex.Message}");
                    OnCompilationFinished?.Invoke(false, null);
                }
            });
        }

        private void ExtractSHVDNResource(string fileName, string destPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resName = $"ASIAndDLLMaker.Resources.SHVDN.{fileName}";

            using (Stream stream = assembly.GetManifestResourceStream(resName))
            {
                if (stream == null)
                    throw new Exception($"Embedded ScriptHookVDotNet resource '{resName}' not found.");

                using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fs);
                }
            }
        }
    }
}
