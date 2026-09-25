using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;

internal sealed class ConverterForm : Form
{
    private const string AppName = "Anima INT8 ConvRot Converter";
    private const string AppVersion = "0.1 Beta";
    private readonly string appDir = AppDomain.CurrentDomain.BaseDirectory;
    private readonly MenuStrip menu = new MenuStrip();
    private readonly ToolStripMenuItem installItem = new ToolStripMenuItem("파이썬 및 의존성 설치");
    private readonly TextBox sourceBox = new TextBox();
    private readonly TextBox outputBox = new TextBox();
    private readonly RadioButton sameRadio = new RadioButton();
    private readonly RadioButton customRadio = new RadioButton();
    private readonly Button sourceBrowse = new Button();
    private readonly Button outputBrowse = new Button();
    private readonly Button validateButton = new Button();
    private readonly Button runButton = new Button();
    private readonly Button stopButton = new Button();
    private readonly Label outputPreview = new Label();
    private readonly Label status = new Label();
    private readonly Label timeLabel = new Label();
    private readonly Label remainingLabel = new Label();
    private readonly ProgressBar progress = new ProgressBar();
    private readonly RichTextBox log = new RichTextBox();
    private readonly System.Windows.Forms.Timer clock = new System.Windows.Forms.Timer();
    private readonly object processLock = new object();
    private Process currentProcess;
    private bool installing;
    private bool validating;
    private bool running;
    private bool cancelRequested;
    private bool closeAfterStop;
    private int completedLayers;
    private int totalLayers;
    private Stopwatch stopwatch;
    private ValidationPlan validatedPlan;

    private sealed class ValidationPlan
    {
        internal string Source;
        internal long Length;
        internal long ModifiedTicks;
        internal int QuantLayers;
        internal int Embeddings;
        internal int Total { get { return QuantLayers + Embeddings; } }
    }

    private string Runtime { get { return Path.Combine(appDir, "runtime"); } }
    private string VenvPython { get { return Path.Combine(Runtime, ".venv", "Scripts", "python.exe"); } }
    private string BasePython { get { return Path.Combine(Runtime, "python310", "python.exe"); } }
    private string Ready { get { return Path.Combine(Runtime, "READY"); } }
    private string CudaRecord { get { return Path.Combine(Runtime, "cuda_build.txt"); } }

    internal ConverterForm()
    {
        Text = AppName;
        ClientSize = new Size(820, 590);
        MinimumSize = new Size(680, 520);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        ToolStripMenuItem options = new ToolStripMenuItem("옵션");
        options.DropDownItems.Add(installItem);
        installItem.Click += delegate { StartInstall(); };
        menu.Items.Add(options);
        ToolStripMenuItem help = new ToolStripMenuItem("도움말");
        ToolStripMenuItem usage = new ToolStripMenuItem("사용법");
        ToolStripMenuItem about = new ToolStripMenuItem("정보");
        usage.Click += delegate { ShowUsage(); };
        about.Click += delegate { ShowAbout(); };
        help.DropDownItems.Add(usage);
        help.DropDownItems.Add(about);
        menu.Items.Add(help);
        MainMenuStrip = menu;
        Controls.Add(menu);

        Label sourceTitle = new Label();
        sourceTitle.Text = "변환 대상 safetensors 파일";
        sourceTitle.SetBounds(16, 42, 380, 22);
        Controls.Add(sourceTitle);
        sourceBox.SetBounds(16, 69, 674, 26);
        sourceBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        sourceBox.TextChanged += delegate { InvalidateValidation(); UpdatePreview(); };
        Controls.Add(sourceBox);
        sourceBrowse.Text = "찾아보기";
        sourceBrowse.SetBounds(700, 68, 104, 28);
        sourceBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        sourceBrowse.Click += delegate { BrowseSource(); };
        Controls.Add(sourceBrowse);

        GroupBox outputGroup = new GroupBox();
        outputGroup.Text = "출력 위치";
        outputGroup.SetBounds(16, 110, 788, 120);
        outputGroup.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(outputGroup);
        sameRadio.Text = "원본 모델과 같은 위치";
        sameRadio.Checked = true;
        sameRadio.SetBounds(12, 25, 240, 24);
        sameRadio.CheckedChanged += delegate { UpdateOutputMode(); };
        outputGroup.Controls.Add(sameRadio);
        customRadio.Text = "직접 경로 지정";
        customRadio.SetBounds(12, 51, 240, 24);
        customRadio.CheckedChanged += delegate { UpdateOutputMode(); };
        outputGroup.Controls.Add(customRadio);
        outputBox.SetBounds(12, 80, 648, 25);
        outputBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        outputBox.TextChanged += delegate { UpdatePreview(); };
        outputGroup.Controls.Add(outputBox);
        outputBrowse.Text = "📁";
        outputBrowse.SetBounds(670, 78, 102, 28);
        outputBrowse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        outputBrowse.Click += delegate { BrowseOutput(); };
        outputGroup.Controls.Add(outputBrowse);
        UpdateOutputMode();

        outputPreview.SetBounds(16, 236, 788, 25);
        outputPreview.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        outputPreview.AutoEllipsis = true;
        Controls.Add(outputPreview);
        UpdatePreview();

        validateButton.Text = "검증";
        validateButton.SetBounds(16, 270, 90, 30);
        validateButton.Click += delegate { StartValidation(); };
        Controls.Add(validateButton);
        runButton.Text = "실행";
        runButton.SetBounds(114, 270, 90, 30);
        runButton.Click += delegate { StartConversion(); };
        runButton.Enabled = false;
        Controls.Add(runButton);
        stopButton.Text = "종료";
        stopButton.SetBounds(212, 270, 90, 30);
        stopButton.Click += delegate { StopOrClose(); };
        Controls.Add(stopButton);
        status.Text = "대기 중";
        status.TextAlign = ContentAlignment.MiddleRight;
        status.SetBounds(330, 272, 474, 25);
        status.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(status);

        progress.SetBounds(16, 314, 535, 20);
        progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(progress);
        timeLabel.Text = "경과 00:00 / 예상 총 --:--";
        timeLabel.TextAlign = ContentAlignment.MiddleRight;
        timeLabel.SetBounds(555, 310, 249, 27);
        timeLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        Controls.Add(timeLabel);
        remainingLabel.Text = "남은 시간: 계산 전";
        remainingLabel.ForeColor = Color.DimGray;
        remainingLabel.TextAlign = ContentAlignment.MiddleRight;
        remainingLabel.SetBounds(555, 339, 249, 21);
        remainingLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        Controls.Add(remainingLabel);

        log.ReadOnly = true;
        log.WordWrap = true;
        log.SetBounds(16, 369, 788, 205);
        log.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(log);
        clock.Interval = 1000;
        clock.Tick += delegate { UpdateClock(); };
        FormClosing += OnFormClosing;
    }

    private void ShowUsage()
    {
        MessageBox.Show(this,
            "처음 사용할 때\n" +
            "1. 옵션 → 파이썬 및 의존성 설치를 누릅니다.\n" +
            "2. 설치 확인 창에서 예를 누르고, 설치 완료 창이 나올 때까지 기다립니다.\n\n" +
            "모델을 변환할 때\n" +
            "1. 변환할 .safetensors 파일을 선택합니다.\n" +
            "2. 출력 위치를 선택합니다.\n" +
            "3. 검증을 누르고 완료될 때까지 기다립니다.\n" +
            "4. 실행을 눌러 변환을 시작합니다.",
            "사용법", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowAbout()
    {
        MessageBox.Show(this,
            AppName + "\n" +
            "버전 " + AppVersion + "\n" +
            "developed by aji-maru\n\n" +
            "Comfy-Org의 GPL-3.0 변환 코드를 포함합니다.\n" +
            "출처와 라이선스는 THIRD_PARTY_NOTICES.txt 및 converter/LICENSE를 참고하세요.",
            "정보", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void BrowseSource()
    {
        using (OpenFileDialog dialog = new OpenFileDialog()) {
            dialog.Filter = "Safetensors (*.safetensors)|*.safetensors|All files (*.*)|*.*";
            dialog.InitialDirectory = Directory.Exists(@"D:\ComfyUI\models\diffusion_models")
                ? @"D:\ComfyUI\models\diffusion_models" : appDir;
            if (dialog.ShowDialog(this) == DialogResult.OK) sourceBox.Text = dialog.FileName;
        }
    }

    private void BrowseOutput()
    {
        using (FolderBrowserDialog dialog = new FolderBrowserDialog()) {
            dialog.Description = "출력 폴더 선택";
            if (Directory.Exists(outputBox.Text)) dialog.SelectedPath = outputBox.Text;
            if (dialog.ShowDialog(this) == DialogResult.OK) outputBox.Text = dialog.SelectedPath;
        }
    }

    private void UpdateOutputMode()
    {
        outputBox.Enabled = customRadio.Checked && !running;
        outputBrowse.Enabled = customRadio.Checked && !running;
        UpdatePreview();
    }

    private string DestinationFor(string source)
    {
        string directory = customRadio.Checked ? outputBox.Text.Trim().Trim('"') : Path.GetDirectoryName(source);
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(source) + "_int8_convrot.safetensors");
    }

    private void UpdatePreview()
    {
        string source = sourceBox.Text.Trim().Trim('"');
        if (source.Length == 0) { outputPreview.Text = "출력: 원본 파일을 선택하세요"; return; }
        if (customRadio.Checked && outputBox.Text.Trim().Length == 0) {
            outputPreview.Text = "출력: 직접 경로를 지정하세요"; return;
        }
        try { outputPreview.Text = "출력: " + DestinationFor(source); }
        catch (Exception) { outputPreview.Text = "출력: 경로를 확인하세요"; }
    }

    private void AppendLog(string line)
    {
        if (IsDisposed) return;
        if (InvokeRequired) { BeginInvoke(new Action<string>(AppendLog), line); return; }
        log.AppendText(line + Environment.NewLine);
        log.SelectionStart = log.TextLength;
        log.ScrollToCaret();
    }

    private void Post(Action action)
    {
        if (!IsDisposed && IsHandleCreated) BeginInvoke(action);
    }

    private static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }

    private int RunProcess(string executable, string arguments, Action<string> onLine, bool track)
    {
        ProcessStartInfo info = new ProcessStartInfo(executable, arguments);
        info.WorkingDirectory = appDir;
        info.UseShellExecute = false;
        info.CreateNoWindow = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        using (Process process = new Process()) {
            process.StartInfo = info;
            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) {
                if (e.Data != null) onLine(e.Data);
            };
            process.Start();
            if (track) lock (processLock) currentProcess = process;
            if (track && cancelRequested && !process.HasExited) process.Kill();
            process.BeginErrorReadLine();
            string line;
            while ((line = process.StandardOutput.ReadLine()) != null) onLine(line);
            process.WaitForExit();
            if (track) lock (processLock) currentProcess = null;
            return process.ExitCode;
        }
    }

    private bool IsInstalled()
    {
        if (!File.Exists(Ready) || !File.Exists(BasePython) || !File.Exists(VenvPython) || !File.Exists(CudaRecord)) return false;
        string cudaBuild;
        try { cudaBuild = File.ReadAllText(CudaRecord).Trim(); }
        catch (Exception) { return false; }
        if (cudaBuild != "cu118" && cudaBuild != "cu126" && cudaBuild != "cu128") return false;
        string verifyScript = Path.Combine(appDir, "verify_runtime.py");
        if (!File.Exists(verifyScript)) return false;
        try { return RunProcess(VenvPython, "-B " + Quote(verifyScript) + " " + cudaBuild,
                delegate(string line) {}, false) == 0; }
        catch (Exception) { return false; }
    }

    private void SetBusy(bool value)
    {
        validateButton.Enabled = !value;
        runButton.Enabled = !value && validatedPlan != null;
        sourceBox.Enabled = !value;
        sourceBrowse.Enabled = !value;
        sameRadio.Enabled = !value;
        customRadio.Enabled = !value;
        outputBox.Enabled = !value && customRadio.Checked;
        outputBrowse.Enabled = !value && customRadio.Checked;
        installItem.Enabled = !value;
        stopButton.Enabled = !installing;
    }

    private void InvalidateValidation()
    {
        validatedPlan = null;
        runButton.Enabled = false;
        if (!running && !validating && !installing) status.Text = "검증 필요";
    }

    private static bool MatchesPlan(string source, ValidationPlan plan)
    {
        if (plan == null || !File.Exists(source)) return false;
        FileInfo file = new FileInfo(source);
        return String.Equals(Path.GetFullPath(source), plan.Source, StringComparison.OrdinalIgnoreCase)
            && file.Length == plan.Length && file.LastWriteTimeUtc.Ticks == plan.ModifiedTicks;
    }

    private void StartValidation()
    {
        if (validating || running || installing) return;
        string source = sourceBox.Text.Trim().Trim('"');
        if (!File.Exists(source) || !source.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase)) {
            MessageBox.Show(this, "변환할 safetensors 파일을 선택하세요.", "파일 오류"); return;
        }
        if (!IsInstalled()) {
            MessageBox.Show(this, "옵션 > 파이썬 및 의존성 설치를 실행하세요.", "설치 필요"); return;
        }
        InvalidateValidation();
        source = Path.GetFullPath(source);
        FileInfo file = new FileInfo(source);
        ValidationPlan snapshot = new ValidationPlan {
            Source = source, Length = file.Length, ModifiedTicks = file.LastWriteTimeUtc.Ticks
        };
        validating = true;
        cancelRequested = false;
        SetBusy(true);
        progress.Style = ProgressBarStyle.Marquee;
        stopButton.Text = "작업 종료";
        status.Text = "검증 중";
        AppendLog("검증 시작: " + source);
        new Thread(delegate() { ValidateWorker(snapshot); }).Start();
    }

    private void ValidateWorker(ValidationPlan snapshot)
    {
        bool success = false;
        try {
            string inspector = Path.Combine(appDir, "model_inspect.py");
            StringBuilder report = new StringBuilder();
            int inspectCode = RunProcess(VenvPython, Quote(inspector) + " " + Quote(snapshot.Source),
                delegate(string line) { report.AppendLine(line); }, true);
            if (cancelRequested) throw new OperationCanceledException();
            if (inspectCode != 0) throw new InvalidDataException("입력 파일 검사 실패: " + report.ToString());
            if (ParseJson(report.ToString()) == null)
                throw new InvalidDataException("입력 파일 검사 결과가 올바르지 않습니다.");
            string converter = Path.Combine(appDir, "converter", "quant_int8_convrot.py");
            StringBuilder dryRun = new StringBuilder();
            int dryRunCode = RunProcess(VenvPython,
                "-u " + Quote(converter) + " " + Quote(snapshot.Source) + " --dry-run",
                delegate(string line) { dryRun.AppendLine(line); AppendLog(line); }, true);
            if (cancelRequested) throw new OperationCanceledException();
            if (dryRunCode != 0)
                throw new InvalidDataException("--dry-run 실패 (종료 코드 " + dryRunCode + ")");
            Match quantPlan = Regex.Match(dryRun.ToString(), @"QUANTIZE\s+(\d+)\s+layers");
            Match embeddingPlan = Regex.Match(dryRun.ToString(), @"EMBEDDINGS\s+(\d+)\s+table");
            if (!quantPlan.Success) throw new InvalidDataException("변환 대상 층 수를 확인하지 못했습니다.");
            snapshot.QuantLayers = int.Parse(quantPlan.Groups[1].Value);
            snapshot.Embeddings = embeddingPlan.Success ? int.Parse(embeddingPlan.Groups[1].Value) : 0;
            if (snapshot.QuantLayers == 0)
                throw new InvalidDataException("ConvRot으로 변환할 선형 층이 없습니다.");
            if (!MatchesPlan(snapshot.Source, snapshot))
                throw new InvalidDataException("검증 중 입력 파일이 변경되었습니다. 다시 검증하세요.");
            success = true;
            AppendLog("검증 완료: ConvRot " + snapshot.QuantLayers + "층, 임베딩 " + snapshot.Embeddings + "개");
        } catch (OperationCanceledException) { AppendLog("검증이 중지되었습니다."); }
        catch (Exception ex) { AppendLog("검증 오류: " + ex.Message); }
        bool passed = success;
        Post(delegate {
            validating = false;
            validatedPlan = passed ? snapshot : null;
            progress.Style = ProgressBarStyle.Blocks;
            progress.Value = 0;
            SetBusy(false);
            stopButton.Text = "종료";
            status.Text = passed ? "검증 완료 · 실행 가능" : cancelRequested ? "검증 중지됨" : "검증 실패";
            if (closeAfterStop) Close();
        });
    }

    private void StartInstall()
    {
        if (installing || validating || running) return;
        if (MessageBox.Show(this, "파이썬 및 의존성을 설치하시겠습니까?", "설치 확인",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        installing = true;
        SetBusy(true);
        status.Text = "설치 확인 중";
        progress.Style = ProgressBarStyle.Marquee;
        AppendLog("앱 폴더의 Python 3.10 및 의존성을 확인합니다.");
        new Thread(delegate() {
            bool success = false;
            try {
                if (IsInstalled()) success = true;
                else {
                    string script = Path.Combine(appDir, "setup_portable.ps1");
                    if (!File.Exists(script)) throw new FileNotFoundException("설치 코드가 없습니다.", script);
                    Post(delegate { status.Text = "설치 중"; });
                    int code = RunProcess("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File " + Quote(script), AppendLog, false);
                    if (code != 0) throw new InvalidOperationException("설치 프로세스 종료 코드: " + code);
                    success = IsInstalled();
                    if (!success) throw new InvalidOperationException("설치 후 검증에 실패했습니다.");
                }
            } catch (Exception ex) { AppendLog("설치 오류: " + ex.Message); }
            bool completed = success;
            Post(delegate {
                installing = false;
                progress.Style = ProgressBarStyle.Blocks;
                progress.Value = completed ? 100 : 0;
                SetBusy(false);
                status.Text = completed ? "설치가 완료되었습니다" : "설치에 실패했습니다";
                AppendLog(status.Text);
                MessageBox.Show(this, status.Text, completed ? "설치 완료" : "설치 실패",
                    MessageBoxButtons.OK, completed ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            });
        }).Start();
    }

    private void StartConversion()
    {
        if (running || validating || installing) return;
        string source = sourceBox.Text.Trim().Trim('"');
        if (!File.Exists(source) || !source.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase)) {
            MessageBox.Show(this, "변환할 safetensors 파일을 선택하세요.", "파일 오류"); return;
        }
        if (!IsInstalled()) {
            MessageBox.Show(this, "옵션 > 파이썬 및 의존성 설치를 실행하세요.", "설치 필요"); return;
        }
        ValidationPlan plan = validatedPlan;
        if (!MatchesPlan(source, plan)) {
            InvalidateValidation();
            MessageBox.Show(this, "입력 파일이 변경되었거나 검증되지 않았습니다. 검증을 다시 실행하세요.", "검증 필요");
            return;
        }
        if (customRadio.Checked && !Directory.Exists(outputBox.Text.Trim().Trim('"'))) {
            MessageBox.Show(this, "기존 출력 폴더를 지정하세요.", "출력 경로 오류"); return;
        }
        string destination = DestinationFor(source);
        if (File.Exists(destination)) {
            MessageBox.Show(this, "출력 파일이 이미 있습니다.\n" + destination, "출력 파일 존재"); return;
        }
        running = true;
        cancelRequested = false;
        completedLayers = 0;
        totalLayers = plan.Total;
        stopwatch = Stopwatch.StartNew();
        clock.Start();
        progress.Style = ProgressBarStyle.Blocks;
        progress.Value = 0;
        stopButton.Text = "작업 종료";
        SetBusy(true);
        status.Text = "변환 중";
        AppendLog("저장된 검증 결과 사용: ConvRot " + plan.QuantLayers + "층, 임베딩 " + plan.Embeddings + "개");
        new Thread(delegate() { ConvertWorker(source, destination, plan); }).Start();
    }

    private static Dictionary<string, object> ParseJson(string json)
    {
        JavaScriptSerializer parser = new JavaScriptSerializer();
        parser.MaxJsonLength = 100000000;
        return parser.DeserializeObject(json) as Dictionary<string, object>;
    }

    private static void VerifyOutput(string path, int expected)
    {
        using (FileStream file = File.OpenRead(path)) {
            byte[] length = new byte[8];
            if (file.Read(length, 0, 8) != 8) throw new InvalidDataException("출력 헤더가 없습니다.");
            ulong count = BitConverter.ToUInt64(length, 0);
            if (count > 100000000 || count > (ulong)(file.Length - 8))
                throw new InvalidDataException("출력 헤더가 올바르지 않습니다.");
            byte[] bytes = new byte[(int)count];
            int offset = 0;
            while (offset < bytes.Length) {
                int read = file.Read(bytes, offset, bytes.Length - offset);
                if (read == 0) throw new EndOfStreamException();
                offset += read;
            }
            Dictionary<string, object> header = ParseJson(Encoding.UTF8.GetString(bytes));
            int quantized = 0;
            foreach (KeyValuePair<string, object> item in header) {
                if (!item.Key.EndsWith(".comfy_quant", StringComparison.Ordinal)) continue;
                string weightKey = item.Key.Substring(0, item.Key.Length - ".comfy_quant".Length) + ".weight";
                object data;
                if (!header.TryGetValue(weightKey, out data)) throw new InvalidDataException("INT8 가중치가 없습니다.");
                Dictionary<string, object> weight = data as Dictionary<string, object>;
                if (weight == null || !Equals(weight["dtype"], "I8"))
                    throw new InvalidDataException("일부 가중치가 INT8이 아닙니다.");
                quantized++;
            }
            if (quantized != expected)
                throw new InvalidDataException("INT8 층 " + quantized + "개, 예상 " + expected + "개");
        }
    }

    private void ConvertWorker(string source, string destination, ValidationPlan plan)
    {
        string temporary = Path.Combine(Path.GetDirectoryName(destination), "." +
            Path.GetFileNameWithoutExtension(destination) + "." + Guid.NewGuid().ToString("N") + ".partial.safetensors");
        bool success = false;
        try {
            string converter = Path.Combine(appDir, "converter", "quant_int8_convrot.py");
            int expected = plan.Total;
            Post(delegate {
                AppendLog("원본: " + source);
                AppendLog("출력: " + destination);
            });
            if (cancelRequested) throw new OperationCanceledException();
            Regex progressPattern = new Regex(@"^\s*(\d+)/(\d+)\s+\.\.\.");
            Regex embeddingPattern = new Regex(@"^\s*embedding\s+");
            int completedQuant = 0;
            int completedEmbeddings = 0;
            int code = RunProcess(VenvPython,
                "-u " + Quote(converter) + " " + Quote(source) + " " + Quote(temporary),
                delegate(string line) {
                    AppendLog(line);
                    Match match = progressPattern.Match(line);
                    if (match.Success) completedQuant = int.Parse(match.Groups[1].Value);
                    else if (embeddingPattern.IsMatch(line)) completedEmbeddings++;
                    else return;
                    int done = Math.Min(expected, completedQuant + completedEmbeddings);
                    Post(delegate {
                        completedLayers = done;
                        progress.Value = Math.Min(95, 95 * done / Math.Max(1, expected));
                        status.Text = done >= expected ? "저장 및 검증 중" :
                            "변환 중 " + (100 * done / expected) + "%";
                        UpdateClock();
                    });
                }, true);
            if (cancelRequested) throw new OperationCanceledException();
            if (code != 0) throw new InvalidOperationException("변환 실패 (종료 코드 " + code + ")");
            if (!File.Exists(temporary) || new FileInfo(temporary).Length == 0)
                throw new InvalidDataException("출력 파일이 생성되지 않았습니다.");
            VerifyOutput(temporary, expected);
            if (File.Exists(destination)) throw new IOException("출력 파일이 이미 있습니다.");
            File.Move(temporary, destination);
            success = true;
            AppendLog("완료: " + destination);
        } catch (OperationCanceledException) { AppendLog("작업이 중지되었습니다."); }
        catch (Exception ex) { AppendLog("오류: " + ex.Message); }
        finally {
            if (!success && File.Exists(temporary)) {
                try { File.Delete(temporary); } catch (Exception ex) { AppendLog("임시 파일 정리 실패: " + ex.Message); }
            }
            bool done = success;
            Post(delegate {
                running = false;
                clock.Stop();
                stopwatch.Stop();
                SetBusy(false);
                stopButton.Text = "종료";
                stopButton.Enabled = true;
                if (done) progress.Value = 100;
                status.Text = done ? "완료" : cancelRequested ? "중지됨" : "실패";
                UpdateClock();
                if (closeAfterStop) Close();
            });
        }
    }

    private static string Duration(double seconds)
    {
        TimeSpan span = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return span.TotalHours >= 1 ? span.ToString(@"hh\:mm\:ss") : span.ToString(@"mm\:ss");
    }

    private void UpdateClock()
    {
        if (stopwatch == null) return;
        double elapsed = stopwatch.Elapsed.TotalSeconds;
        if (!running) {
            timeLabel.Text = "경과 " + Duration(elapsed) + " / 총 " + Duration(elapsed);
            remainingLabel.Text = "남은 시간: " + (progress.Value == 100 ? "00:00" : "표시 불가");
        } else if (completedLayers >= totalLayers && totalLayers > 0) {
            timeLabel.Text = "경과 " + Duration(elapsed) + " / 예상 총 계산 중";
            remainingLabel.Text = "남은 시간: 저장 및 검증 중";
        } else if (completedLayers > 0 && totalLayers > 0) {
            double estimate = elapsed * totalLayers / completedLayers * 1.05;
            timeLabel.Text = "경과 " + Duration(elapsed) + " / 예상 총 " + Duration(estimate);
            remainingLabel.Text = "남은 시간: 약 " + Duration(estimate - elapsed);
        } else {
            timeLabel.Text = "경과 " + Duration(elapsed) + " / 예상 총 --:--";
            remainingLabel.Text = "남은 시간: 초기 처리 속도 측정 중";
        }
    }

    private void StopOrClose()
    {
        if (installing) return;
        if (!running && !validating) { Close(); return; }
        cancelRequested = true;
        status.Text = "작업 종료 중";
        stopButton.Enabled = false;
        Process process;
        lock (processLock) process = currentProcess;
        try {
            if (process != null && !process.HasExited) {
                int pid = process.Id;
                new Thread(delegate() {
                    try { RunProcess("taskkill.exe", "/PID " + pid + " /T /F", AppendLog, false); }
                    catch (Exception ex) { AppendLog("작업 종료 오류: " + ex.Message); }
                }).Start();
            }
        } catch (InvalidOperationException) {
            // The process ended while the stop button was being handled.
        }
    }

    private void OnFormClosing(object sender, FormClosingEventArgs args)
    {
        if (installing) {
            args.Cancel = true;
            MessageBox.Show(this, "설치가 끝나면 창을 닫을 수 있습니다.", "설치 중");
        } else if (running || validating) {
            args.Cancel = true;
            closeAfterStop = true;
            StopOrClose();
        }
    }
}

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new ConverterForm());
    }
}
