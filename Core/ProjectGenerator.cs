using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace ASIAndDLLMaker.Core
{
    public static class ProjectGenerator
    {
        public static void ExportCppProject(string destDir, string projectName, string userCode, string[] inputFiles, string targetExt)
        {
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            // 1. Create directory structure
            string sdkIncDir = Path.Combine(destDir, "sdk", "inc");
            string sdkLibDir = Path.Combine(destDir, "sdk", "lib");
            Directory.CreateDirectory(sdkIncDir);
            Directory.CreateDirectory(sdkLibDir);

            // 2. Extract C++ SDK resources
            ExtractCppSDK(sdkIncDir, sdkLibDir);

            // 3. Write C++ Source Files and compile lists
            var clCompileFiles = new System.Collections.Generic.List<string>();
            var clIncludeFiles = new System.Collections.Generic.List<string>();

            // Always write main.cpp
            File.WriteAllText(Path.Combine(destDir, "main.cpp"), GetBoilerplateMain());
            clCompileFiles.Add("main.cpp");

            if (inputFiles == null || inputFiles.Length == 0)
            {
                File.WriteAllText(Path.Combine(destDir, "script.h"), GetBoilerplateHeader());
                File.WriteAllText(Path.Combine(destDir, "script.cpp"), userCode);
                clCompileFiles.Add("script.cpp");
                clIncludeFiles.Add("script.h");
            }
            else if (inputFiles.Length == 1)
            {
                string filename = Path.GetFileName(inputFiles[0]);
                string ext = Path.GetExtension(filename).ToLower();
                if (ext == ".cpp" || ext == ".c" || ext == ".cc" || ext == ".cxx")
                {
                    File.WriteAllText(Path.Combine(destDir, filename), userCode);
                    clCompileFiles.Add(filename);
                    File.WriteAllText(Path.Combine(destDir, "script.h"), GetBoilerplateHeader());
                    clIncludeFiles.Add("script.h");
                }
                else
                {
                    File.Copy(inputFiles[0], Path.Combine(destDir, filename), true);
                    clIncludeFiles.Add(filename);
                    File.WriteAllText(Path.Combine(destDir, "script.cpp"), userCode);
                    clCompileFiles.Add("script.cpp");
                }
            }
            else
            {
                foreach (var file in inputFiles)
                {
                    string filename = Path.GetFileName(file);
                    string ext = Path.GetExtension(filename).ToLower();
                    File.Copy(file, Path.Combine(destDir, filename), true);
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

            // 4. Generate MSBuild Project Files
            string vcxprojContent = GetVcxprojTemplate(projectName, "v143", targetExt, clCompileFiles, clIncludeFiles);
            File.WriteAllText(Path.Combine(destDir, $"{projectName}.vcxproj"), vcxprojContent);

            string filtersContent = GetVcxprojFiltersTemplate(clCompileFiles, clIncludeFiles);
            File.WriteAllText(Path.Combine(destDir, $"{projectName}.vcxproj.filters"), filtersContent);

            // 5. Generate Solution File
            string slnContent = GetSlnTemplate(projectName, "{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}", ".vcxproj");
            File.WriteAllText(Path.Combine(destDir, $"{projectName}.sln"), slnContent);
        }

        public static void ExportCsProject(string destDir, string projectName, string userCode, int shvdnVersion, string[] inputFiles, string targetExt)
        {
            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            // 1. Create directory structure
            string libDir = Path.Combine(destDir, "lib");
            Directory.CreateDirectory(libDir);

            // 2. Extract C# SHVDN DLL resource
            string shvdnDllName = shvdnVersion == 2 ? "ScriptHookVDotNet2.dll" : "ScriptHookVDotNet3.dll";
            ExtractSHVDNResource(shvdnDllName, Path.Combine(libDir, shvdnDllName));

            // 3. Write C# Code Files
            var compileFiles = new System.Collections.Generic.List<string>();

            if (inputFiles == null || inputFiles.Length == 0)
            {
                string filename = $"{projectName}.cs";
                File.WriteAllText(Path.Combine(destDir, filename), userCode);
                compileFiles.Add(filename);
            }
            else if (inputFiles.Length == 1)
            {
                string filename = Path.GetFileName(inputFiles[0]);
                File.WriteAllText(Path.Combine(destDir, filename), userCode);
                compileFiles.Add(filename);
            }
            else
            {
                foreach (var file in inputFiles)
                {
                    string filename = Path.GetFileName(file);
                    File.Copy(file, Path.Combine(destDir, filename), true);
                    compileFiles.Add(filename);
                }
            }

            // 4. Generate Csproj File
            string csprojContent = GetCsprojTemplate(projectName, shvdnDllName, targetExt, compileFiles);
            File.WriteAllText(Path.Combine(destDir, $"{projectName}.csproj"), csprojContent);

            // 5. Generate Solution File
            string slnContent = GetSlnTemplate(projectName, "{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}");
            File.WriteAllText(Path.Combine(destDir, $"{projectName}.sln"), slnContent);
        }

        private static void ExtractCppSDK(string incDir, string libDir)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string prefix = "ASIAndDLLMaker.Resources.SDK";

            string[] headers = { "main.h", "types.h", "enums.h", "nativeCaller.h", "natives.h" };
            foreach (var header in headers)
            {
                ExtractResource(assembly, $"{prefix}.inc.{header}", Path.Combine(incDir, header));
            }

            ExtractResource(assembly, $"{prefix}.lib.ScriptHookV.lib", Path.Combine(libDir, "ScriptHookV.lib"));
        }

        private static void ExtractSHVDNResource(string fileName, string destPath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string resName = $"ASIAndDLLMaker.Resources.SHVDN.{fileName}";
            ExtractResource(assembly, resName, destPath);
        }

        private static void ExtractResource(Assembly assembly, string resourceName, string destPath)
        {
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new Exception($"Resource '{resourceName}' not found.");

                using (FileStream fs = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fs);
                }
            }
        }

        private static string GetBoilerplateMain()
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

        private static string GetBoilerplateHeader()
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

        private static string GetVcxprojTemplate(string projectName, string toolset, string targetExt, System.Collections.Generic.IEnumerable<string> compileFiles, System.Collections.Generic.IEnumerable<string> includeFiles)
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

        private static string GetVcxprojFiltersTemplate(System.Collections.Generic.IEnumerable<string> compileFiles, System.Collections.Generic.IEnumerable<string> includeFiles)
        {
            var compileNodes = System.Linq.Enumerable.Select(compileFiles, f => $"    <ClCompile Include=\"{f}\">\r\n      <Filter>Source Files</Filter>\r\n    </ClCompile>");
            var includeNodes = System.Linq.Enumerable.Select(includeFiles, f => $"    <ClInclude Include=\"{f}\">\r\n      <Filter>Header Files</Filter>\r\n    </ClInclude>");
            string compileString = string.Join("\r\n", compileNodes);
            string includeString = string.Join("\r\n", includeNodes);

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""4.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <ItemGroup>
    <Filter Include=""Source Files"">
      <UniqueIdentifier>{{4FC737F1-C7A5-4376-A066-2A32D752A2FF}}</UniqueIdentifier>
      <Extensions>cpp;c;cc;cxx;def;odl;idl;hpj;bat;asm;asmx</Extensions>
    </Filter>
    <Filter Include=""Header Files"">
      <UniqueIdentifier>{{93995380-89BD-4b04-88EB-625FBE52EBFB}}</UniqueIdentifier>
      <Extensions>h;hh;hpp;hxx;hm;inl;inc;xsd</Extensions>
    </Filter>
  </ItemGroup>
  <ItemGroup>
{compileString}
  </ItemGroup>
  <ItemGroup>
{includeString}
  </ItemGroup>
</Project>";
        }

        private static string GetCsprojTemplate(string projectName, string shvdnDllName, string targetExt, System.Collections.Generic.IEnumerable<string> compileFiles)
        {
            var compileNodes = System.Linq.Enumerable.Select(compileFiles, f => $"    <Compile Include=\"{f}\" />");
            string compileString = string.Join("\r\n", compileNodes);

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Project ToolsVersion=""15.0"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
  <PropertyGroup>
    <Configuration Condition="" '$(Configuration)' == '' "">Release</Configuration>
    <Platform Condition="" '$(Platform)' == '' "">AnyCPU</Platform>
    <ProjectGuid>{{{Guid.NewGuid().ToString().ToUpper()}}}</ProjectGuid>
    <OutputType>Library</OutputType>
    <AppDesignerFolder>Properties</AppDesignerFolder>
    <RootNamespace>{projectName}</RootNamespace>
    <AssemblyName>{projectName}</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
    <Deterministic>true</Deterministic>
    <TargetExt>{targetExt}</TargetExt>
  </PropertyGroup>
  <PropertyGroup Condition="" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' "">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include=""System"" />
    <Reference Include=""System.Core"" />
    <Reference Include=""System.Drawing"" />
    <Reference Include=""System.Windows.Forms"" />
    <Reference Include=""System.Xml.Linq"" />
    <Reference Include=""System.Data.DataSetExtensions"" />
    <Reference Include=""Microsoft.CSharp"" />
    <Reference Include=""System.Data"" />
    <Reference Include=""System.Net.Http"" />
    <Reference Include=""System.Xml"" />
    <Reference Include=""{shvdnDllName.Replace(".dll", "")}"">
      <HintPath>lib\{shvdnDllName}</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>
  <ItemGroup>
{compileString}
  </ItemGroup>
  <Import Project=""$(MSBuildToolsPath)\Microsoft.CSharp.targets"" />
</Project>";
        }

        private static string GetSlnTemplate(string projectName, string projectTypeGuid, string ext = ".csproj")
        {
            string projectGuid = Guid.NewGuid().ToString().ToUpper();
            return $@"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{projectTypeGuid}"") = ""{projectName}"", ""{projectName}{ext}"", ""{{{projectGuid}}}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{{{projectGuid}}}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{{{projectGuid}}}.Release|Any CPU.Build.0 = Release|Any CPU
		{{{projectGuid}}}.Release|x64.ActiveCfg = Release|x64
		{{{projectGuid}}}.Release|x64.Build.0 = Release|x64
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
";
        }
    }
}
