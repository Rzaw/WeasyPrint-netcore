﻿using Balbarak.WeasyPrint.Internals;
using Balbarak.WeasyPrint.Resources;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace Balbarak.WeasyPrint
{
    public class WeasyPrintClient : IDisposable
    {
        private readonly string _libDir = Path.Combine(Directory.GetCurrentDirectory(), "weasyprint-v48");
        private Process _nativeProccess;
        private readonly FilesManager _fileManager;
        private readonly ProcessInvoker _invoker;
        private readonly ITraceWriter _trace;
        private Dictionary<string, string> _environmentVariables;

        public delegate void WeasyPrintEventHandler(OutputEventArgs e);
        public event WeasyPrintEventHandler OnDataOutput;
        public event WeasyPrintEventHandler OnDataError;

        public WeasyPrintClient()
        {
            _fileManager = new FilesManager();

            SetEnviromentVariables();

            _invoker = new ProcessInvoker(_environmentVariables);
        }

        public WeasyPrintClient(ITraceWriter traceWriter) : this()
        {
            _invoker = new ProcessInvoker(_environmentVariables, traceWriter);

            _trace = traceWriter;
        }

        public byte[] GeneratePdf(string htmlText)
        {
            if (!CheckFiles())
                InitFiles();

            byte[] result = null;

            try
            {
                LogOutput("Generating pdf from html text ...");

                var fileName = $"{Guid.NewGuid().ToString().ToLower()}";
                var dirSeparator = Path.DirectorySeparatorChar;

                var inputFileName = $"{fileName}.html";
                var outputFileName = $"{fileName}.pdf";

                var inputFullName = Path.Combine(_libDir, inputFileName);
                var outputFullName = Path.Combine(_libDir, outputFileName);

                File.WriteAllText(Path.Combine(_libDir, inputFileName), htmlText);

                ExcuteCommand($"python.exe weasyprint.exe {inputFileName} {outputFileName} -e utf8");

                result = File.ReadAllBytes(outputFullName);

                if (File.Exists(inputFullName))
                    File.Delete(inputFullName);

                if (File.Exists(outputFullName))
                    File.Delete(outputFullName);

                LogOutput("Pdf generated successfully");

            }
            catch (Exception ex)
            {
                OnDataError?.Invoke(new OutputEventArgs(ex.ToString()));
            }

            return result;
        }

        public async Task<byte[]> GeneratePdfAsync(string htmlText)
        {
            byte[] result = null;

            try
            {
                if (!_fileManager.IsFilesExsited())
                    await _fileManager.InitFiles();

                var data = Encoding.UTF8.GetBytes(htmlText);
                var fileName = $"{Guid.NewGuid().ToString().ToLower()}";

                var inputFileName = $"{fileName}.html";
                var outputFileName = $"{fileName}.pdf";

                var fullFilePath = await _fileManager.CreateFile(inputFileName, data)
                    .ConfigureAwait(false);

                var cmd = $"/c python.exe scripts/weasyprint.exe {fullFilePath} {outputFileName} -e utf8";

                var workingDir = _fileManager.FolderPath;

                await _invoker.ExcuteAsync(workingDir, "cmd.exe", cmd)
                    .ConfigureAwait(false);

                await _fileManager.Delete(fullFilePath)
                    .ConfigureAwait(false);

                result = await _fileManager.ReadFile(outputFileName)
                    .ConfigureAwait(false);

                await _fileManager.Delete(outputFileName)
                    .ConfigureAwait(false);

                return result;
            }
            catch (Exception ex)
            {
                LogError(ex.ToString());
            }

            return result;
        }

        public void GeneratePdf(string inputPathFile, string outputPathFile)
        {
            if (!CheckFiles())
                InitFiles();

            if (!File.Exists(inputPathFile))
                throw new FileNotFoundException(inputPathFile);

            try
            {
                LogOutput($"Generating pdf from html file {inputPathFile} to {outputPathFile}");

                ExcuteCommand($"python.exe weasyprint.exe {inputPathFile} {outputPathFile} -e utf8");

                LogOutput("Pdf generated successfully");

            }
            catch (Exception ex)
            {
                OnDataError?.Invoke(new OutputEventArgs(ex.ToString()));
            }

        }

        public async Task GeneratePdfAsync(string inputPathFile, string outputPathFile)
        {
            if (!CheckFiles())
                InitFiles();

            if (!File.Exists(inputPathFile))
                throw new FileNotFoundException(inputPathFile);

            try
            {
                LogOutput($"Generating pdf from html file {inputPathFile} to {outputPathFile}");

                ExcuteCommand($"python.exe weasyprint.exe {inputPathFile} {outputPathFile} -e utf8");

                LogOutput("Pdf generated successfully");

            }
            catch (Exception ex)
            {
                OnDataError?.Invoke(new OutputEventArgs(ex.ToString()));
            }

        }

        public byte[] GeneratePdfFromUrl(string url)
        {
            if (!CheckFiles())
                InitFiles();

            byte[] result = null;

            try
            {
                LogOutput($"Generating pdf from url {url} ...");

                var fileName = $"{Guid.NewGuid().ToString().ToLower()}";
                var dirSeparator = Path.DirectorySeparatorChar;

                var outputFileName = $"{fileName}.pdf";

                var outputFullName = Path.Combine(_libDir, outputFileName);

                ExcuteCommand($"python.exe weasyprint.exe {url} {outputFileName} ");

                result = File.ReadAllBytes(outputFullName);

                if (File.Exists(outputFullName))
                    File.Delete(outputFullName);

                LogOutput("Pdf generated successfully");

            }
            catch (Exception ex)
            {
                OnDataError?.Invoke(new OutputEventArgs(ex.ToString()));
            }

            return result;
        }

        public void GeneratePdfFromUrl(string url, string outputFilePath)
        {
            if (!CheckFiles())
                InitFiles();

            try
            {
                LogOutput($"Generating pdf from url {url} ...");

                ExcuteCommand($"python.exe weasyprint.exe {url} {outputFilePath} ");

                LogOutput("Pdf generated successfully");

            }
            catch (Exception ex)
            {
                OnDataError?.Invoke(new OutputEventArgs(ex.ToString()));
            }

        }

        private void ExcuteCommand(string cmd)
        {
            InitProccess();

            _nativeProccess.StartInfo.Arguments = $@"/c {cmd}";

            _nativeProccess.Start();

            _nativeProccess.BeginOutputReadLine();
            _nativeProccess.BeginErrorReadLine();

            _nativeProccess.WaitForExit();

        }

        private bool CheckFiles()
        {
            LogOutput("Checking files ...");

            if (!Directory.Exists(_libDir))
                return false;

            var files = Directory.GetFiles(_libDir);

            if (files.Count() < 22)
                return false;

            var containPython = files.Where(a => a.Contains("python.exe")).FirstOrDefault() != null;

            if (!containPython)
                return false;

            return true;
        }

        private void InitFiles()
        {
            LogOutput($"Checking {_libDir} direcoty");

            if (!Directory.Exists(_libDir))
            {
                LogOutput("Creating direcotry");

                Directory.CreateDirectory(_libDir);
            }
            else
            {
                LogOutput("Deleting corrupted files ...");

                Directory.Delete(_libDir, true);

                Directory.CreateDirectory(_libDir);
            }

            var filesData = FileResx.libCompress;

            var zipFileName = Path.Combine(_libDir, "weasyFile.zip");

            File.WriteAllBytes(zipFileName, filesData);

            LogOutput("Extracting files ...");

            ZipFile.ExtractToDirectory(zipFileName, _libDir);

            LogOutput($"Deleting {zipFileName}");

            File.Delete(zipFileName);
        }

        private void InitProccess()
        {
            KillProc();

            var workingDir = _libDir;

            _nativeProccess = new Process();

            _nativeProccess.StartInfo.FileName = @"cmd.exe";

            _nativeProccess.StartInfo.EnvironmentVariables["PATH"] = "gtk3;%PATH%";

            _nativeProccess.StartInfo.EnvironmentVariables["FONTCONFIG_FILE"] = $"{workingDir}\\gtk3\\fonts.config";

            _nativeProccess.StartInfo.WorkingDirectory = workingDir;
            _nativeProccess.StartInfo.UseShellExecute = false;
            _nativeProccess.StartInfo.RedirectStandardInput = true;
            _nativeProccess.StartInfo.RedirectStandardOutput = true;
            _nativeProccess.StartInfo.RedirectStandardError = true;
            _nativeProccess.StartInfo.CreateNoWindow = true;

            _nativeProccess.OutputDataReceived += OnOutputDataReceived;
            _nativeProccess.ErrorDataReceived += OnErrorDataReceived;
            _nativeProccess.Exited += OnExited;

        }

        private void OnExited(object sender, EventArgs e)
        {
            Debug.WriteLine("Proccess exited");
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            LogError(e.Data);

            Debug.WriteLine($"Error: {e.Data}");
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            LogOutput(e.Data);

            Debug.WriteLine(e.Data);
        }

        private void LogOutput(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return;

            OnDataOutput?.Invoke(new OutputEventArgs(data));

            _trace?.Info(data);
        }

        private void LogError(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return;

            OnDataError?.Invoke(new OutputEventArgs(data));

            _trace?.Info($"Error: {data}");
        }

        public void Dispose()
        {
            KillProc();
        }

        private void KillProc()
        {
            if (_nativeProccess != null)
            {
                try
                {
                    _nativeProccess.Kill();
                }
                catch
                {

                }

                _nativeProccess.Dispose();
            }
        }

        private void SetEnviromentVariables()
        {
            var variables = new Dictionary<string, string>()
            {
                ["PATH"] = "Scripts;gtk3;%PATH%",
                ["FONTCONFIG_FILE"] = $@"{_fileManager.FolderPath}\gtk3\etc\fonts"
            };

            _environmentVariables = variables;

        }
    }
}
