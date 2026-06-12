using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ASIAndDLLMaker.Core
{
    public class CompilerCpp
    {
        public event Action<string> OnLogReceived;
        public event Action<bool, string> OnCompilationFinished;

        private void Log(string message)
        {
            OnLogReceived?.Invoke(message);
        }

        public async Task CompileAsync(string editorCode, string[] inputFiles, string outputPath, string projectName = "CustomScript")
        {
            string msbuildPath = VSLocator.FindMSBuild();
            if (msbuildPath == null)
            {
                Log("Error: MSBuild.exe was not found. Please ensure Visual Studio 2022 C++ Build Tools are installed.");
                OnCompilationFinished?.Invoke(false, null);
                return;
            }

            string toolset = VSLocator.FindPlatformToolset();
            Log($"Found MSBuild at: {msbuildPath}");
            Log($"Using Platform Toolset: {toolset}");

            string tempDir = Path.Combine(Path.GetTempPath(), "ASIAndDLLMaker_Cpp_" + Guid.NewGuid().ToString().Substring(0, 8));
            Directory.CreateDirectory(tempDir);
            Log($"Created temporary compilation folder: {tempDir}");

            try
            {
                // 1. Extract SDK Files
                string sdkIncDir = Path.Combine(tempDir, "sdk", "inc");
                string sdkLibDir = Path.Combine(tempDir, "sdk", "lib");
                Directory.CreateDirectory(sdkIncDir);
                Directory.CreateDirectory(sdkLibDir);

                ExtractEmbeddedSDK(sdkIncDir, sdkLibDir);
                Log("Extracted Script Hook V SDK headers and import library.");

                // 2. Write C++ Source Files and compile lists
                var clCompileFiles = new System.Collections.Generic.List<string>();
                var clIncludeFiles = new System.Collections.Generic.List<string>();

                // Always include main.cpp which registers the script hook main
                File.WriteAllText(Path.Combine(tempDir, "main.cpp"), GetBoilerplateMain());
                clCompileFiles.Add("main.cpp");

                if (inputFiles == null || inputFiles.Length == 0)
                {
                    File.WriteAllText(Path.Combine(tempDir, "script.h"), GetBoilerplateHeader());
                    File.WriteAllText(Path.Combine(tempDir, "script.cpp"), editorCode);
                    clCompileFiles.Add("script.cpp");
                    clIncludeFiles.Add("script.h");
                    Log("Using script code from editor.");
                }
                else if (inputFiles.Length == 1)
                {
                    string filename = Path.GetFileName(inputFiles[0]);
                    string ext = Path.GetExtension(filename).ToLower();
                    if (ext == ".cpp" || ext == ".c" || ext == ".cc" || ext == ".cxx")
                    {
                        File.WriteAllText(Path.Combine(tempDir, filename), editorCode);
                        clCompileFiles.Add(filename);
                        File.WriteAllText(Path.Combine(tempDir, "script.h"), GetBoilerplateHeader());
                        clIncludeFiles.Add("script.h");
                    }
                    else
                    {
                        File.Copy(inputFiles[0], Path.Combine(tempDir, filename), true);
                        clIncludeFiles.Add(filename);
                        File.WriteAllText(Path.Combine(tempDir, "script.cpp"), editorCode);
                        clCompileFiles.Add("script.cpp");
                    }
                    Log($"Loaded script file: {filename}");
                }
                else
                {
                    Log($"Copying {inputFiles.Length} source files...");
                    foreach (var file in inputFiles)
                    {
                        string filename = Path.GetFileName(file);
                        string ext = Path.GetExtension(filename).ToLower();
                        File.Copy(file, Path.Combine(tempDir, filename), true);
                        if (ext == ".cpp" || ext == ".c" || ext == ".cc" || ext == ".cxx")
                        {
                            clCompileFiles.Add(filename);
                        }
                        else if (ext == ".h" || ext == ".hpp" || ext == ".hxx")
                        {
                            clIncludeFiles.Add(filename);
                        }
                    }
                }

                // 3. Write VCXProj File
                string extension = Path.GetExtension(outputPath);
                if (string.IsNullOrEmpty(extension))
                {
                    extension = ".asi";
                }
                string vcxprojContent = GetVcxprojTemplate(toolset, projectName, extension, clCompileFiles, clIncludeFiles);
                string vcxprojPath = Path.Combine(tempDir, $"{projectName}.vcxproj");
                File.WriteAllText(vcxprojPath, vcxprojContent);
                Log("Generated project files.");

                // 4. Run MSBuild
                Log("Launching MSBuild compilation...");
                bool buildSuccess = await RunMSBuildAsync(msbuildPath, vcxprojPath, tempDir);

                if (buildSuccess)
                {
                    string compiledBinary = Path.Combine(tempDir, "bin", $"{projectName}{extension}");
                    if (File.Exists(compiledBinary))
                    {
                        string destDir = Path.GetDirectoryName(outputPath);
                        if (!Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }
                        File.Copy(compiledBinary, outputPath, true);
                        Log($"Success! Compilation complete. Output copied to: {outputPath}");
                        OnCompilationFinished?.Invoke(true, outputPath);
                    }
                    else
                    {
                        Log($"Error: Compilation finished but target {extension} file was not found.");
                        OnCompilationFinished?.Invoke(false, null);
                    }
                }
                else
                {
                    Log("Compilation failed. See error logs above.");
                    OnCompilationFinished?.Invoke(false, null);
                }
            }
            catch (Exception ex)
            {
                Log($"Critical Exception during compilation: {ex.Message}");
                OnCompilationFinished?.Invoke(false, null);
            }
            finally
            {
                // Clean up temp files in background
                Task.Run(() =>
                {
                    try
                    {
                        if (Directory.Exists(tempDir))
                        {
                            Directory.Delete(tempDir, true);
                        }
                    }
                    catch { /* Ignore cleanup issues */ }
                });
            }
        }

        private void ExtractEmbeddedSDK(string incDir, string libDir)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string prefix = "ASIAndDLLMaker.Resources.SDK";

            // Headers
            string[] headers = { "main.h", "types.h", "enums.h", "nativeCaller.h", "natives.h" };
            foreach (var header in headers)
            {
                string resName = $"{prefix}.inc.{header}";
                ExtractResource(assembly, resName, Path.Combine(incDir, header));
            }

            // Lib
            ExtractResource(assembly, $"{prefix}.lib.ScriptHookV.lib", Path.Combine(libDir, "ScriptHookV.lib"));
        }

        private void ExtractResource(Assembly assembly, string resourceName, string destPath)
        {
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"Embedded resource '{resourceName}' not found.");

                using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fs);
                }
            }
        }

        private async Task<bool> RunMSBuildAsync(string msbuildPath, string projectPath, string workingDir)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = msbuildPath,
                Arguments = $"\"{projectPath}\" /p:Configuration=Release /p:Platform=x64 /v:m",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDir
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();

                var outputTask = ReadStreamAsync(process.StandardOutput, line => Log(line));
                var errorTask = ReadStreamAsync(process.StandardError, line => Log("[Error] " + line));

                await Task.WhenAll(outputTask, errorTask);
                process.WaitForExit();

                return process.ExitCode == 0;
            }
        }

        private async Task ReadStreamAsync(StreamReader reader, Action<string> onLineRead)
        {
            string line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                onLineRead(line);
            }
        }

        private string GetBoilerplateMain()
        {
            return @"#include ""sdk/inc/main.h""
#include ""script.h""

BOOL APIENTRY DllMain(HMODULE hInstance, DWORD reason, LPVOID lpReserved)
{
    switch (reason)
    {
    case DLL_PROCESS_ATTACH:
        scriptRegister(hInstance, ScriptMain);
        break;
    }
    return TRUE;
}
";
        }

        private string GetBoilerplateHeader()
        {
            return @"#pragma once

#include <cmath>
#include <string>
#include <vector>
#include <sstream>

#include ""sdk/inc/natives.h""
#include ""sdk/inc/types.h""
#include ""sdk/inc/enums.h""
#include ""sdk/inc/main.h""

void ScriptMain();
";
        }

        private string GetVcxprojTemplate(string toolset, string projectName, string targetExt, System.Collections.Generic.IEnumerable<string> compileFiles, System.Collections.Generic.IEnumerable<string> includeFiles)
        {
            var compileNodes = System.Linq.Enumerable.Select(compileFiles, f => $"    <ClCompile Include=\"{f}\" />");
            var includeNodes = System.Linq.Enumerable.Select(includeFiles, f => $"    <ClInclude Include=\"{f}\" />");
            string compileString = string.Join("\r\n", compileNodes);
            string includeString = string.Join("\r\n", includeNodes);

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project DefaultTargets=""Build"" ToolsVersion=""15.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemGroup Label=""ProjectConfigurations"">
    <ProjectConfiguration Include=""Release|x64"">
      <Configuration>Release</Configuration>
      <Platform>x64</Platform>
    </ProjectConfiguration>
  </ItemGroup>
  <ItemGroup>
{compileString}
  </ItemGroup>
  <ItemGroup>
{includeString}
  </ItemGroup>
  <PropertyGroup Label=""Globals"">
    <ProjectGuid>{{{Guid.NewGuid().ToString().ToUpper()}}}</ProjectGuid>
    <Keyword>Win32Proj</Keyword>
    <RootNamespace>{projectName}</RootNamespace>
    <ProjectName>{projectName}</ProjectName>
  </PropertyGroup>
  <Import Project=""$(VCTargetsPath)\Microsoft.Cpp.Default.props"" />
  <PropertyGroup Condition=""'$(Configuration)|$(Platform)'=='Release|x64'"" Label=""Configuration"">
    <ConfigurationType>DynamicLibrary</ConfigurationType>
    <UseDebugLibraries>false</UseDebugLibraries>
    <PlatformToolset>{toolset}</PlatformToolset>
    <WholeProgramOptimization>true</WholeProgramOptimization>
    <CharacterSet>MultiByte</CharacterSet>
  </PropertyGroup>
  <Import Project=""$(VCTargetsPath)\Microsoft.Cpp.props"" />
  <PropertyGroup Condition=""'$(Configuration)|$(Platform)'=='Release|x64'"">
    <LinkIncremental>false</LinkIncremental>
    <TargetExt>{targetExt}</TargetExt>
    <OutDir>$(SolutionDir)bin\</OutDir>
    <IntDir>$(SolutionDir)tmp\</IntDir>
  </PropertyGroup>
  <ItemDefinitionGroup Condition=""'$(Configuration)|$(Platform)'=='Release|x64'"">
    <ClCompile>
      <WarningLevel>Level3</WarningLevel>
      <Optimization>MaxSpeed</Optimization>
      <FunctionLevelLinking>true</FunctionLevelLinking>
      <IntrinsicFunctions>true</IntrinsicFunctions>
      <PreprocessorDefinitions>WIN32;NDEBUG;_WINDOWS;_USRDLL;SCRIPTMOD_EXPORTS;%(PreprocessorDefinitions)</PreprocessorDefinitions>
      <RuntimeLibrary>MultiThreaded</RuntimeLibrary>
      <FloatingPointModel>Fast</FloatingPointModel>
      <AdditionalIncludeDirectories>sdk/inc;%(AdditionalIncludeDirectories)</AdditionalIncludeDirectories>
    </ClCompile>
    <Link>
      <SubSystem>Windows</SubSystem>
      <GenerateDebugInformation>false</GenerateDebugInformation>
      <EnableCOMDATFolding>true</EnableCOMDATFolding>
      <OptimizeReferences>true</OptimizeReferences>
      <AdditionalOptions>sdk/lib/ScriptHookV.lib %(AdditionalOptions)</AdditionalOptions>
    </Link>
  </ItemDefinitionGroup>
  <Import Project=""$(VCTargetsPath)\Microsoft.Cpp.targets"" />
</Project>";
        }
    }
}
