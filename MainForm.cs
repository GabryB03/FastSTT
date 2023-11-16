using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using MetroSuite;

public partial class MainForm : MetroForm
{
    private string[] _tempFiles = new string[]
    {
        "transcription.txt",
        "finished.txt",
        "model.txt",
        "device.txt",
        "compute_type.txt",
        "language.txt",
        "enable_auto_language_detect.txt",
        "disable_auto_language_detect.txt",
        "ready_to_transcribe.txt",
        "input.mp3",
        "enable_report_timings.txt",
        "disable_report_timings.txt",
        "reload_model.txt",
        "precision.txt"
    };

    private Process _inferWaiter;
    private string _previousModel = "", _previousDevice = "", _previousComputeType = "";

    private void DeleteTempFiles()
    {
        foreach (string tempFile in _tempFiles)
        {
            if (File.Exists($"runtime\\{tempFile}"))
            {
                File.Delete($"runtime\\{tempFile}");
            }
        }
    }

    public MainForm()
    {
        InitializeComponent();

        CloseAllPythonInstances();
        DeleteTempFiles();
        StartInferWaiter();

        guna2ComboBox1.SelectedIndex = guna2ComboBox1.Items.Count - 1;
        guna2ComboBox2.SelectedIndex = 0;
    }

    private void CloseAllPythonInstances()
    {
        foreach (Process process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == Process.GetCurrentProcess().Id)
                {
                    continue;
                }

                if (process.MainModule.ModuleName.ToLower().Contains("python") || process.ProcessName.ToLower().Contains("python")
                    || process.ProcessName.ToLower().Contains("ffmpeg") || process.ProcessName.ToLower().Contains("faststt")
                    || process.MainModule.FileName.ToLower().Contains("python")
                    || process.MainModule.FileName.ToLower().Contains("ffmpeg")
                    || process.MainModule.FileName.ToLower().Contains("faststt")
                    || process.ProcessName.ToLower().Equals("cmd"))
                {
                    process.Kill();
                }
            }
            catch
            {

            }
        }

        try
        {
            _inferWaiter.Kill();
        }
        catch
        {

        }
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
        CloseAllPythonInstances();
        DeleteTempFiles();
    }

    private void RunFFMpeg(string arguments)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg.exe",
            Arguments = $"-threads {Environment.ProcessorCount} {arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        }).WaitForExit();
    }

    private void CompressAudioFile(string inputAudioPath, string outputAudioPath)
    {
        RunFFMpeg($"-i \"{inputAudioPath}\" -af aresample=osf=s16:dither_method=triangular_hp -sample_fmt s16 -ar 48000 -ac 1 -b:a 96k -filter:a \"highpass=f=50, lowpass=f=15000\" -map a \"{outputAudioPath}\"");

        while (!File.Exists(outputAudioPath))
        {
            Thread.Sleep(1);
        }
    }

    private void StartInferWaiter()
    {
        _inferWaiter = new Process();
        _inferWaiter.StartInfo.FileName = "_infer_waiter_.bat";
        _inferWaiter.StartInfo.WorkingDirectory = Path.GetFullPath("runtime");
        _inferWaiter.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        _inferWaiter.StartInfo.CreateNoWindow = true;
        _inferWaiter.Start();

        while (!File.Exists("runtime\\ready_to_transcribe.txt"))
        {
            Thread.Sleep(1);
        }

        File.Delete("runtime\\ready_to_transcribe.txt");
    }

    private void guna2Button1_Click(object sender, EventArgs e)
    {
        openFileDialog1.FileName = "";

        if (openFileDialog1.ShowDialog().Equals(DialogResult.OK))
        {
            guna2TextBox1.Text = openFileDialog1.FileName;
        }
    }

    private void guna2Button2_Click(object sender, EventArgs e)
    {
        saveFileDialog1.FileName = "";

        if (saveFileDialog1.ShowDialog().Equals(DialogResult.OK))
        {
            guna2TextBox2.Text = saveFileDialog1.FileName;
        }
    }

    private void CreateRuntimeFile(string name, string content = null)
    {
        if (content == null)
        {
            File.Create($"runtime\\{name}.txt").Close();
        }
        else
        {
            File.WriteAllText($"runtime\\{name}.txt", content);
        }

        while (File.Exists($"runtime\\{name}.txt"))
        {
            Thread.Sleep(1);
        }
    }

    private string GetSelectedModel()
    {
        switch (guna2ComboBox1.SelectedIndex)
        {
            case 0:
                return "tiny.en";
            case 1:
                return "base.en";
            case 2:
                return "small.en";
            case 3:
                return "medium.en";
            case 4:
                return "tiny";
            case 5:
                return "base";
            case 6:
                return "small";
            case 7:
                return "medium";
            case 8:
                return "large-v1";
            case 9:
                return "large-v2";
            case 10:
                return "large-v3";
        }

        return "large-v3";
    }

    private string GetSelectedLanguage()
    {
        return guna2ComboBox2.SelectedItem.ToString().Split('(')[1].Split(')')[0];
    }

    private string GetFloatPrecision()
    {
        if (guna2CheckBox3.Checked)
        {
            if (guna2CheckBox4.Checked)
            {
                return "float16";
            }
            else
            {
                return "int8_float16";
            }
        }
        else
        {
            if (guna2CheckBox4.Checked)
            {
                return "float32";
            }
            else
            {
                return "int8";
            }
        }
    }

    private void guna2TrackBar1_Scroll(object sender, ScrollEventArgs e)
    {
        label1.Text = $"Model precision: {guna2TrackBar1.Value}/10";
    }

    private string GetDevice()
    {
        return guna2CheckBox3.Checked ? "cuda" : "cpu";
    }

    private void guna2Button3_Click(object sender, EventArgs e)
    {
        if (!File.Exists(openFileDialog1.FileName))
        {
            MessageBox.Show("The input audio file does not exist.", "FastSTT", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (File.Exists(saveFileDialog1.FileName))
        {
            File.Delete(saveFileDialog1.FileName);
        }

        if (File.Exists("input.mp3"))
        {
            File.Delete("input.mp3");
        }

        DeleteTempFiles();

        string model = GetSelectedModel(), device = GetDevice(), computeType = GetFloatPrecision();

        CreateRuntimeFile("precision", guna2TrackBar1.Value.ToString());
        CreateRuntimeFile("language", GetSelectedLanguage());
        CreateRuntimeFile(guna2CheckBox1.Checked ? "enable_auto_language_detect" : "disable_auto_language_detect");
        CreateRuntimeFile(guna2CheckBox2.Checked ? "enable_report_timings" : "disable_report_timings");

        CreateRuntimeFile("model", model);
        CreateRuntimeFile("device", device);
        CreateRuntimeFile("compute_type", computeType);

        if (model != _previousModel || device != _previousDevice || computeType != _previousComputeType)
        {
            CreateRuntimeFile("reload_model");
        }

        _previousModel = model;
        _previousDevice = device;
        _previousComputeType = computeType;

        CompressAudioFile(Path.GetFullPath(guna2TextBox1.Text), Path.GetFullPath("input.mp3"));

        while (!File.Exists("input.mp3"))
        {
            Thread.Sleep(1);
        }

        File.Move(Path.GetFullPath("input.mp3"), Path.GetFullPath("runtime\\input.mp3"));

        while (!File.Exists("runtime\\transcription.txt") && !File.Exists("runtime\\finished.txt"))
        {
            Thread.Sleep(1);
        }

        File.Move(Path.GetFullPath("runtime\\transcription.txt"), Path.GetFullPath(guna2TextBox2.Text));
        DeleteTempFiles();
        MessageBox.Show("Succesfully generated your transcription!", "FastSTT", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}