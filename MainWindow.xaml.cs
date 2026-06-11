using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Microsoft.Win32;
using ASIAndDLLMaker.Core;

namespace ASIAndDLLMaker
{
    public partial class MainWindow : Window
    {
        private CompilerCpp cppCompiler;
        private CompilerCs csCompiler;
        private string detectedMsBuildPath;
        private string defaultOutputDir;
        private System.Collections.Generic.List<string> selectedCppFiles = new System.Collections.Generic.List<string>();
        private System.Collections.Generic.List<string> selectedCsFiles = new System.Collections.Generic.List<string>();

        public MainWindow()
        {
            InitializeComponent();
            
            cppCompiler = new CompilerCpp();
            csCompiler = new CompilerCs();

            cppCompiler.OnLogReceived += (line) => AppendLogLine(ConsoleCppLog, line);
            csCompiler.OnLogReceived += (line) => AppendLogLine(ConsoleCsLog, line);

            // Default output directory is the user's Desktop
            defaultOutputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ASI_DLL_Mods");
            if (!Directory.Exists(defaultOutputDir))
            {
                Directory.CreateDirectory(defaultOutputDir);
            }

            TxtDefaultOutputDir.Text = defaultOutputDir;
            TxtCppOutputPath.Text = defaultOutputDir;
            TxtCsOutputPath.Text = defaultOutputDir;

            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Set up AvalonEdit Highlighting
            EditorCpp.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("C++");
            EditorCs.SyntaxHighlighting = ICSharpCode.AvalonEdit.Highlighting.HighlightingManager.Instance.GetDefinition("C#");

            // Load default templates
            ComboCppTemplate.SelectedIndex = 0;
            ComboCsTemplate.SelectedIndex = 0;

            // Detect VS/MSBuild
            DetectMSBuild();
        }

        private void DetectMSBuild()
        {
            detectedMsBuildPath = VSLocator.FindMSBuild();
            if (detectedMsBuildPath != null && File.Exists(detectedMsBuildPath))
            {
                TxtMsBuildPath.Text = detectedMsBuildPath;
                StatusDot.Fill = Brushes.LimeGreen;
                TxtStatusLabel.Text = "VS 2022 MSBuild Detected";
                TxtStatusLabel.Foreground = Brushes.LimeGreen;
                AppendLogLine(ConsoleCppLog, $"System: Located MSBuild compiler at {detectedMsBuildPath}");
            }
            else
            {
                TxtMsBuildPath.Text = "Not Found";
                StatusDot.Fill = Brushes.Red;
                TxtStatusLabel.Text = "Not Detected (VS 2022 C++ Workload required)";
                TxtStatusLabel.Foreground = Brushes.Red;
                AppendLogLine(ConsoleCppLog, "System [Warning]: MSBuild not found. C++ compilations will fail unless VS 2022 C++ development tools are installed.");
            }
        }

        // Navigation Click Handlers
        private void BtnCpp_Click(object sender, RoutedEventArgs e)
        {
            TabControlMain.SelectedIndex = 0;
        }

        private void BtnCs_Click(object sender, RoutedEventArgs e)
        {
            TabControlMain.SelectedIndex = 1;
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            TabControlMain.SelectedIndex = 2;
        }

        // Browsing output directory
        private void BtnBrowseCpp_Click(object sender, RoutedEventArgs e)
        {
            var folder = BrowseFolder();
            if (folder != null) TxtCppOutputPath.Text = folder;
        }

        private void BtnBrowseCs_Click(object sender, RoutedEventArgs e)
        {
            var folder = BrowseFolder();
            if (folder != null) TxtCsOutputPath.Text = folder;
        }

        private void BtnBrowseCppInput_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "C++ source/header files (*.cpp;*.h;*.hpp;*.cc;*.cxx)|*.cpp;*.h;*.hpp;*.cc;*.cxx|All files (*.*)|*.*",
                InitialDirectory = defaultOutputDir
            };
            if (dialog.ShowDialog() == true)
            {
                selectedCppFiles.Clear();
                selectedCppFiles.AddRange(dialog.FileNames);
                if (selectedCppFiles.Count == 1)
                {
                    TxtCppInputFiles.Text = selectedCppFiles[0];
                    TxtCppProjectName.Text = Path.GetFileNameWithoutExtension(selectedCppFiles[0]);
                    EditorCpp.Text = File.ReadAllText(selectedCppFiles[0]);
                }
                else
                {
                    TxtCppInputFiles.Text = $"{selectedCppFiles.Count} files selected: " + string.Join(", ", System.Linq.Enumerable.Select(selectedCppFiles, Path.GetFileName));
                    TxtCppProjectName.Text = Path.GetFileNameWithoutExtension(selectedCppFiles[0]);
                    EditorCpp.Text = File.ReadAllText(selectedCppFiles[0]);
                }
            }
        }

        private void BtnBrowseCsInput_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "C# files (*.cs)|*.cs|All files (*.*)|*.*",
                InitialDirectory = defaultOutputDir
            };
            if (dialog.ShowDialog() == true)
            {
                selectedCsFiles.Clear();
                selectedCsFiles.AddRange(dialog.FileNames);
                if (selectedCsFiles.Count == 1)
                {
                    TxtCsInputFiles.Text = selectedCsFiles[0];
                    TxtCsProjectName.Text = Path.GetFileNameWithoutExtension(selectedCsFiles[0]);
                    EditorCs.Text = File.ReadAllText(selectedCsFiles[0]);
                }
                else
                {
                    TxtCsInputFiles.Text = $"{selectedCsFiles.Count} files selected: " + string.Join(", ", System.Linq.Enumerable.Select(selectedCsFiles, Path.GetFileName));
                    TxtCsProjectName.Text = Path.GetFileNameWithoutExtension(selectedCsFiles[0]);
                    EditorCs.Text = File.ReadAllText(selectedCsFiles[0]);
                }
            }
        }

        private void BtnBrowseDefaultOut_Click(object sender, RoutedEventArgs e)
        {
            var folder = BrowseFolder();
            if (folder != null)
            {
                defaultOutputDir = folder;
                TxtDefaultOutputDir.Text = folder;
                TxtCppOutputPath.Text = folder;
                TxtCsOutputPath.Text = folder;
            }
        }

        private string? BrowseFolder()
        {
            // .NET 8.0 has OpenFolderDialog
            var dialog = new OpenFolderDialog
            {
                InitialDirectory = defaultOutputDir
            };
            if (dialog.ShowDialog() == true)
            {
                return dialog.FolderName;
            }
            return null;
        }

        // C++ Template selections
        private void ComboCppTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EditorCpp == null) return;

            string templateName = ComboCppTemplate.SelectedIndex == 1 ? "cpp_vehicle.cpp" : "cpp_base.cpp";
            string resPath = $"ASIAndDLLMaker.Resources.Templates.{templateName}";
            string code = LoadResourceString(resPath);
            EditorCpp.Text = code;
        }

        // C# Template selections
        private void ComboCsTemplate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EditorCs == null) return;

            string templateName = ComboCsTemplate.SelectedIndex == 1 ? "cs_vehicle.cs" : "cs_base.cs";
            string resPath = $"ASIAndDLLMaker.Resources.Templates.{templateName}";
            string code = LoadResourceString(resPath);
            EditorCs.Text = code;
        }

        private string LoadResourceString(string resourcePath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream? stream = assembly.GetManifestResourceStream(resourcePath))
            {
                if (stream == null) return string.Empty;
                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        // Clear log actions
        private void BtnClearCppLog_Click(object sender, RoutedEventArgs e)
        {
            ParaCppLog.Inlines.Clear();
        }

        private void BtnClearCsLog_Click(object sender, RoutedEventArgs e)
        {
            ParaCsLog.Inlines.Clear();
        }

        private void BtnRedetect_Click(object sender, RoutedEventArgs e)
        {
            DetectMSBuild();
        }

        // COMPILATIONS
        private async void BtnCompileCppAsi_Click(object sender, RoutedEventArgs e)
        {
            await CompileCppInternal(".asi");
        }

        private async void BtnCompileCppDll_Click(object sender, RoutedEventArgs e)
        {
            await CompileCppInternal(".dll");
        }

        private async Task CompileCppInternal(string extension)
        {
            ParaCppLog.Inlines.Clear();
            string code = EditorCpp.Text;
            string projectName = TxtCppProjectName.Text.Trim();
            string outputDir = TxtCppOutputPath.Text;
            string outputPath = Path.Combine(outputDir, $"{projectName}{extension}");

            if (string.IsNullOrEmpty(projectName))
            {
                AppendLogLine(ConsoleCppLog, "[Error] Project Name cannot be empty.");
                return;
            }

            BtnCompileCpp_Click_Action(false);
            await cppCompiler.CompileAsync(code, selectedCppFiles.ToArray(), outputPath, projectName);
            BtnCompileCpp_Click_Action(true);
        }

        private void BtnCompileCpp_Click_Action(bool enable)
        {
            // Toggle controls during compile
            ComboCppTemplate.IsEnabled = enable;
            TxtCppProjectName.IsEnabled = enable;
        }

        private async void BtnCompileCsAsi_Click(object sender, RoutedEventArgs e)
        {
            await CompileCsInternal(".asi");
        }

        private async void BtnCompileCsDll_Click(object sender, RoutedEventArgs e)
        {
            await CompileCsInternal(".dll");
        }

        private async Task CompileCsInternal(string extension)
        {
            ParaCsLog.Inlines.Clear();
            string code = EditorCs.Text;
            string projectName = TxtCsProjectName.Text.Trim();
            string outputDir = TxtCsOutputPath.Text;
            string outputPath = Path.Combine(outputDir, $"{projectName}{extension}");

            if (string.IsNullOrEmpty(projectName))
            {
                AppendLogLine(ConsoleCsLog, "[Error] Project Name cannot be empty.");
                return;
            }

            int shvdnVersion = RadioShvdn2.IsChecked == true ? 2 : 3;

            BtnCompileCs_Click_Action(false);
            await csCompiler.CompileAsync(code, selectedCsFiles.ToArray(), outputPath, shvdnVersion, projectName);
            BtnCompileCs_Click_Action(true);
        }

        private void BtnCompileCs_Click_Action(bool enable)
        {
            RadioShvdn2.IsEnabled = enable;
            RadioShvdn3.IsEnabled = enable;
            TxtCsProjectName.IsEnabled = enable;
            ComboCsTemplate.IsEnabled = enable;
        }

        // SOLUTIONS EXPORTING
        private void BtnExportCppSolutionAsi_Click(object sender, RoutedEventArgs e)
        {
            ExportCppSolutionInternal(".asi");
        }

        private void BtnExportCppSolutionDll_Click(object sender, RoutedEventArgs e)
        {
            ExportCppSolutionInternal(".dll");
        }

        private void ExportCppSolutionInternal(string targetExt)
        {
            string projectName = TxtCppProjectName.Text.Trim();
            string code = EditorCpp.Text;

            var dialog = new OpenFolderDialog
            {
                Title = $"Select Destination Folder to Export C++ VS Solution ({targetExt.ToUpper()})",
                InitialDirectory = defaultOutputDir
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string exportDir = Path.Combine(dialog.FolderName, projectName);
                    ProjectGenerator.ExportCppProject(exportDir, projectName, code, selectedCppFiles.ToArray(), targetExt);
                    MessageBox.Show($"Visual Studio 2022 C++ Solution ({targetExt.ToUpper()}) successfully exported to:\n{exportDir}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    AppendLogLine(ConsoleCppLog, $"System: Exported C++ VS Solution ({targetExt}) to {exportDir}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnExportCsSolutionAsi_Click(object sender, RoutedEventArgs e)
        {
            ExportCsSolutionInternal(".asi");
        }

        private void BtnExportCsSolutionDll_Click(object sender, RoutedEventArgs e)
        {
            ExportCsSolutionInternal(".dll");
        }

        private void ExportCsSolutionInternal(string targetExt)
        {
            string projectName = TxtCsProjectName.Text.Trim();
            string code = EditorCs.Text;
            int shvdnVersion = RadioShvdn2.IsChecked == true ? 2 : 3;

            var dialog = new OpenFolderDialog
            {
                Title = $"Select Destination Folder to Export C# VS Solution ({targetExt.ToUpper()})",
                InitialDirectory = defaultOutputDir
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string exportDir = Path.Combine(dialog.FolderName, projectName);
                    ProjectGenerator.ExportCsProject(exportDir, projectName, code, shvdnVersion, selectedCsFiles.ToArray(), targetExt);
                    MessageBox.Show($"Visual Studio 2022 C# Solution ({targetExt.ToUpper()}) successfully exported to:\n{exportDir}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    AppendLogLine(ConsoleCsLog, $"System: Exported C# VS Solution ({targetExt}) to {exportDir}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // COLOR-CODED LOG PARSING
        private void AppendLogLine(RichTextBox rtb, string line)
        {
            Dispatcher.Invoke(() =>
            {
                Paragraph? paragraph = null;
                if (rtb == ConsoleCppLog) paragraph = ParaCppLog;
                else if (rtb == ConsoleCsLog) paragraph = ParaCsLog;

                if (paragraph == null) return;

                Run run = new Run(line + "\n");
                
                // Set color based on line keywords
                string lowerLine = line.ToLower();
                if (lowerLine.Contains("[error]") || lowerLine.Contains("error cs") || lowerLine.Contains(": error") || lowerLine.Contains("failed"))
                {
                    run.Foreground = Brushes.Crimson;
                    run.FontWeight = FontWeights.Bold;
                }
                else if (lowerLine.Contains("[warning]") || lowerLine.Contains("warning cs") || lowerLine.Contains(": warning"))
                {
                    run.Foreground = Brushes.Orange;
                }
                else if (lowerLine.Contains("success!") || lowerLine.Contains("correcta") || lowerLine.Contains("compilación correcta") || lowerLine.Contains("complete"))
                {
                    run.Foreground = Brushes.LimeGreen;
                    run.FontWeight = FontWeights.Bold;
                }
                else if (lowerLine.Contains("system:"))
                {
                    run.Foreground = Brushes.DodgerBlue;
                    run.FontWeight = FontWeights.SemiBold;
                }
                else
                {
                    run.Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 195));
                }

                paragraph.Inlines.Add(run);
                rtb.ScrollToEnd();
            });
        }
    }
}
