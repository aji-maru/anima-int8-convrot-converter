using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

internal sealed class UpdateItem
{
    internal ZipArchiveEntry Entry;
    internal string RelativePath;
    internal string TargetPath;
    internal bool Exists;
    internal string OriginalHash;
    internal string StagedPath;
    internal string BackupPath;
    internal bool Applied;
}

internal static class PackageUpdater
{
    internal const string GuiName = "Anima_INT8_ConvRot_Converter_GUI.exe";
    private const string VersionFile = "release_version.txt";

    internal static string ReadPackageVersion(ZipArchive archive)
    {
        ZipArchiveEntry entry = archive.GetEntry(VersionFile);
        if (entry == null || entry.Length > 128)
            throw new InvalidDataException("배포 버전 정보가 없습니다.");
        using (StreamReader reader = new StreamReader(entry.Open(), Encoding.UTF8, true)) {
            string version = reader.ReadToEnd().Trim();
            if (version.Length == 0 || version.Length > 64 || version.IndexOfAny(new char[] {'\r', '\n'}) >= 0)
                throw new InvalidDataException("배포 버전 정보가 올바르지 않습니다.");
            return version;
        }
    }

    internal static string ReadInstalledVersion(string root)
    {
        string path = Path.Combine(root, VersionFile);
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 128)
            throw new InvalidDataException("설치된 버전 정보가 올바르지 않습니다.");
        string version = File.ReadAllText(path, Encoding.UTF8).Trim();
        if (version.Length == 0 || version.Length > 64 || version.IndexOfAny(new char[] {'\r', '\n'}) >= 0)
            throw new InvalidDataException("설치된 버전 정보가 올바르지 않습니다.");
        return version;
    }

    internal static bool NeedsUpdate(bool existing, string root, string packageVersion)
    {
        return !existing || !String.Equals(ReadInstalledVersion(root), packageVersion, StringComparison.Ordinal);
    }

    internal static bool IsExistingInstallation(string root)
    {
        return File.Exists(Path.Combine(root, GuiName)) &&
            File.Exists(Path.Combine(root, "setup_portable.ps1")) &&
            File.Exists(Path.Combine(root, "converter", "quant_int8_convrot.py"));
    }

    private static string Sha256(Stream stream)
    {
        using (SHA256 hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(stream));
    }

    private static string FullRoot(string root)
    {
        string full = Path.GetFullPath(root);
        string driveRoot = Path.GetPathRoot(full);
        return full.Length > driveRoot.Length ? full.TrimEnd(Path.DirectorySeparatorChar) : full;
    }

    private static void CheckDirectoryPath(string root, string target)
    {
        string relative = target.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar);
        string current = root;
        foreach (string part in relative.Split(Path.DirectorySeparatorChar)) {
            if (part.Length == 0) continue;
            current = Path.Combine(current, part);
            if (Directory.Exists(current) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("연결된 폴더를 통한 업데이트는 지원하지 않습니다: " + current);
        }
    }

    internal static List<UpdateItem> Plan(ZipArchive archive, string root)
    {
        string fullRoot = FullRoot(root);
        string prefix = fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        List<UpdateItem> changed = new List<UpdateItem>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ZipArchiveEntry entry in archive.Entries) {
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal)) continue;
            string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(relative) || relative.IndexOf(':') >= 0 ||
                relative.StartsWith("runtime" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("잘못된 배포 파일 경로: " + entry.FullName);
            string target = Path.GetFullPath(Path.Combine(fullRoot, relative));
            if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !seen.Add(relative))
                throw new InvalidDataException("잘못되거나 중복된 배포 파일 경로: " + entry.FullName);
            CheckDirectoryPath(fullRoot, Path.GetDirectoryName(target));
            if (Directory.Exists(target)) throw new IOException("파일 위치에 폴더가 있습니다: " + target);
            bool exists = File.Exists(target);
            string installed = null;
            if (exists) {
                string incoming;
                using (Stream input = entry.Open()) incoming = Sha256(input);
                using (Stream input = File.OpenRead(target)) installed = Sha256(input);
                if (incoming == installed) continue;
            }
            changed.Add(new UpdateItem {
                Entry = entry, RelativePath = relative, TargetPath = target,
                Exists = exists, OriginalHash = installed
            });
        }
        return changed;
    }

    internal static void Apply(string root, List<UpdateItem> items, Action<int> onProgress)
    {
        Directory.CreateDirectory(root);
        string stage = Path.Combine(root, ".anima-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        bool keepStage = false;
        try {
            for (int i = 0; i < items.Count; i++) {
                UpdateItem item = items[i];
                item.StagedPath = Path.Combine(stage, i.ToString("D4") + ".new");
                item.BackupPath = Path.Combine(stage, i.ToString("D4") + ".bak");
                using (Stream input = item.Entry.Open())
                using (FileStream output = new FileStream(item.StagedPath, FileMode.CreateNew, FileAccess.Write))
                    input.CopyTo(output);
            }
            for (int i = 0; i < items.Count; i++) {
                UpdateItem item = items[i];
                CheckDirectoryPath(FullRoot(root),
                    Path.GetDirectoryName(item.TargetPath));
                Directory.CreateDirectory(Path.GetDirectoryName(item.TargetPath));
                if (item.Exists) {
                    if (!File.Exists(item.TargetPath)) throw new IOException("업데이트 중 파일이 사라졌습니다: " + item.RelativePath);
                    string currentHash;
                    using (Stream current = File.OpenRead(item.TargetPath)) currentHash = Sha256(current);
                    if (currentHash != item.OriginalHash)
                        throw new IOException("업데이트 중 파일이 변경되었습니다: " + item.RelativePath);
                    File.Replace(item.StagedPath, item.TargetPath, item.BackupPath);
                } else {
                    if (File.Exists(item.TargetPath)) throw new IOException("업데이트 중 파일이 생겼습니다: " + item.RelativePath);
                    File.Move(item.StagedPath, item.TargetPath);
                }
                item.Applied = true;
                onProgress(i + 1);
            }
        } catch (Exception original) {
            try {
                for (int i = items.Count - 1; i >= 0; i--) {
                    UpdateItem item = items[i];
                    if (!item.Applied) continue;
                    if (item.Exists) File.Replace(item.BackupPath, item.TargetPath, null);
                    else File.Delete(item.TargetPath);
                }
            } catch (Exception rollback) {
                keepStage = true;
                throw new IOException("업데이트와 복구가 모두 실패했습니다. 백업 위치: " + stage +
                    "\n업데이트 오류: " + original.Message + "\n복구 오류: " + rollback.Message, rollback);
            }
            throw;
        } finally {
            if (!keepStage) {
                foreach (UpdateItem item in items) {
                    if (item.StagedPath != null && File.Exists(item.StagedPath)) File.Delete(item.StagedPath);
                    if (item.BackupPath != null && File.Exists(item.BackupPath)) File.Delete(item.BackupPath);
                }
                Directory.Delete(stage);
            }
        }
    }
}

internal sealed class ExtractorForm : Form
{
    private readonly TextBox destination = new TextBox();
    private readonly Button browse = new Button();
    private readonly Button extract = new Button();
    private readonly Button closeButton = new Button();
    private readonly ProgressBar progress = new ProgressBar();
    private readonly Label status = new Label();
    private readonly Label versionLabel = new Label();
    private bool extracting;

    internal ExtractorForm()
    {
        Text = "anima_int8_convrot_converter";
        ClientSize = new Size(590, 230);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F);

        Label title = new Label();
        title.Text = "새 설치 폴더 또는 기존 설치 폴더를 선택하세요.";
        title.SetBounds(20, 20, 540, 25);
        Controls.Add(title);

        destination.SetBounds(20, 56, 485, 27);
        string exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        destination.Text = Path.Combine(exeDirectory, "anima_int8_convrot_converter");
        Controls.Add(destination);

        string packageVersion = "읽기 실패";
        try {
            using (Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")) {
                if (payload != null)
                    using (ZipArchive zip = new ZipArchive(payload, ZipArchiveMode.Read))
                        packageVersion = PackageUpdater.ReadPackageVersion(zip);
            }
        } catch (Exception) { /* The install action displays the detailed error. */ }
        versionLabel.Text = "설치 버전: " + packageVersion;
        versionLabel.SetBounds(20, 89, 555, 20);
        Controls.Add(versionLabel);

        browse.Text = "찾아보기";
        browse.SetBounds(510, 55, 65, 28);
        browse.Click += delegate {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog()) {
                dialog.Description = "설치 폴더 선택";
                if (Directory.Exists(destination.Text)) dialog.SelectedPath = destination.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) destination.Text = dialog.SelectedPath;
            }
        };
        Controls.Add(browse);

        progress.SetBounds(20, 117, 555, 19);
        Controls.Add(progress);
        status.Text = "새 폴더에 설치하거나 기존 설치의 변경 파일만 업데이트합니다.";
        status.SetBounds(20, 142, 555, 26);
        Controls.Add(status);

        extract.Text = "설치 / 업데이트";
        extract.SetBounds(370, 183, 105, 30);
        extract.Click += ExtractClick;
        Controls.Add(extract);

        closeButton.Text = "닫기";
        closeButton.SetBounds(480, 183, 95, 30);
        closeButton.Click += delegate { Close(); };
        Controls.Add(closeButton);
        FormClosing += delegate(object sender, FormClosingEventArgs e) {
            if (extracting) e.Cancel = true;
        };
    }

    private void ExtractClick(object sender, EventArgs args)
    {
        string path;
        try { path = Path.GetFullPath(destination.Text.Trim()); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "경로 오류"); return; }
        bool existing = Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length != 0;
        if (existing && !PackageUpdater.IsExistingInstallation(path)) {
            MessageBox.Show(this, "이 프로그램의 기존 설치 폴더가 아닙니다. 빈 폴더 또는 새 폴더를 지정하세요.", "경로 오류");
            return;
        }
        extracting = true;
        extract.Enabled = false;
        browse.Enabled = false;
        destination.Enabled = false;
        closeButton.Enabled = false;
        try {
            using (Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")) {
                if (payload == null) throw new InvalidOperationException("압축 데이터가 없습니다.");
                using (ZipArchive zip = new ZipArchive(payload, ZipArchiveMode.Read)) {
                    string packageVersion = PackageUpdater.ReadPackageVersion(zip);
                    string installedVersion = existing ? PackageUpdater.ReadInstalledVersion(path) : null;
                    if (!PackageUpdater.NeedsUpdate(existing, path, packageVersion)) {
                        status.Text = "같은 버전이 설치되어 있습니다.";
                        MessageBox.Show(this, "설치된 버전과 배포 버전이 모두 " + packageVersion +
                            "입니다. 파일을 변경하지 않았습니다.", "업데이트 없음");
                        return;
                    }
                    status.Text = "파일을 비교하는 중...";
                    Application.DoEvents();
                    List<UpdateItem> changed = PackageUpdater.Plan(zip, path);
                    if (changed.Count == 0) {
                        throw new InvalidDataException("버전이 다르지만 교체할 배포 파일이 없습니다.");
                    }
                    string details = (existing ? "기존 설치를 업데이트합니다.\n현재 버전: " +
                        (installedVersion ?? "기록 없음") : "새 폴더에 설치합니다.") +
                        "\n설치 버전: " + packageVersion +
                        "\n추가·교체할 파일: " + changed.Count + "개" +
                        "\n\nruntime 폴더와 사용자 모델 파일은 유지합니다. 계속하시겠습니까?";
                    if (MessageBox.Show(this, details, "설치 / 업데이트 확인",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) {
                        status.Text = "취소되었습니다.";
                        return;
                    }
                    progress.Maximum = changed.Count;
                    progress.Value = 0;
                    status.Text = existing ? "업데이트 중..." : "설치 중...";
                    PackageUpdater.Apply(path, changed, delegate(int done) {
                        progress.Value = done;
                        Application.DoEvents();
                    });
                }
            }
            status.Text = existing ? "업데이트 완료" : "설치 완료";
            MessageBox.Show(this,
                (existing ? "업데이트" : "설치") + "가 완료되었습니다. 폴더 안의 " +
                PackageUpdater.GuiName + "를 실행하세요.", "완료");
            extracting = false;
            Close();
        } catch (Exception ex) {
            status.Text = "설치 / 업데이트 실패";
            MessageBox.Show(this, ex.Message, "오류");
        } finally {
            extracting = false;
            extract.Enabled = true;
            browse.Enabled = true;
            destination.Enabled = true;
            closeButton.Enabled = true;
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
        Application.Run(new ExtractorForm());
    }
}
