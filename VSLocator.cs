using System;
using System.Diagnostics;
using System.IO;

namespace ASIAndDLLMaker.Core
{
    public static class VSLocator
    {
        private static string cachedMsBuildPath = null;

        public static string FindMSBuild()
        {
            if (cachedMsBuildPath != null)
                return cachedMsBuildPath;

            // 1. Try using vswhere.exe
            string vswherePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft Visual Studio", "Installer", "vswhere.exe");

            if (File.Exists(vswherePath))
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = vswherePath,
                        Arguments = "-latest -requires Microsoft.Component.MSBuild -property installationPath",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(startInfo))
                    {
                        string output = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();

                        if (!string.IsNullOrEmpty(output) && Directory.Exists(output))
                        {
                            string msbuild = Path.Combine(output, "MSBuild", "Current", "Bin", "MSBuild.exe");
                            if (File.Exists(msbuild))
                            {
                                cachedMsBuildPath = msbuild;
                                return msbuild;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error running vswhere: {ex.Message}");
                }
            }

            // 2. Fallbacks for standard Visual Studio 2022 installations
            string[] editions = { "Community", "Professional", "Enterprise" };
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            foreach (var edition in editions)
            {
                string path = Path.Combine(programFiles, "Microsoft Visual Studio", "2022", edition, "MSBuild", "Current", "Bin", "MSBuild.exe");
                if (File.Exists(path))
                {
                    cachedMsBuildPath = path;
                    return path;
                }
            }

            return null;
        }

        public static string FindPlatformToolset()
        {
            // For Visual Studio 2022, the toolset is v143
            string msbuild = FindMSBuild();
            if (msbuild != null)
            {
                if (msbuild.Contains("2022"))
                    return "v143";
                if (msbuild.Contains("2019"))
                    return "v142";
                if (msbuild.Contains("2017"))
                    return "v141";
            }
            return "v143"; // Default fallback
        }
    }
}
