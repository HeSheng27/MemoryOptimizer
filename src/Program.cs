using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MemoryOptimizer
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (HasFlag(args, "--cli"))
            {
                Environment.Exit(RunCli(args));
                return;
            }

            string shotPath = GetValue(args, "--shot");
            if (!string.IsNullOrEmpty(shotPath))
            {
                RunShot(shotPath, HasFlag(args, "--optimize"));
                return;
            }

            string shotDialog = GetValue(args, "--shot-dialog");
            if (!string.IsNullOrEmpty(shotDialog))
            {
                RunShotDialog(shotDialog);
                return;
            }

            Native.SetDpiAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            bool startHidden = HasFlag(args, "--minimized");
            bool created;
            using (Mutex mutex = new Mutex(true, "Local\\MemoryOptimizer-8f2c1a37", out created))
            {
                if (!created)
                {
                    if (!startHidden)
                    {
                        MessageBox.Show("内存优化器已经在运行。\r\n可以在任务栏通知区域找到它的图标。",
                            "内存优化器", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return;
                }

                Settings settings = Settings.Load();
                Application.Run(new MainForm(settings, startHidden, false));
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            try
            {
                MessageBox.Show("程序出现未处理的异常：\r\n" + e.Exception.Message,
                    "内存优化器", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Exception exception = e.ExceptionObject as Exception;
                MessageBox.Show("程序出现未处理的异常：\r\n" + (exception == null ? "未知错误" : exception.Message),
                    "内存优化器", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }

        private static int RunCli(string[] args)
        {
            string outputPath = GetValue(args, "--out");
            if (string.IsNullOrEmpty(outputPath))
                outputPath = Path.Combine(Settings.Directory, "cli-result.txt");

            StringBuilder report = new StringBuilder();
            report.AppendLine("MemoryOptimizer CLI 自检报告");
            report.AppendLine("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("管理员: " + Native.IsAdministrator());
            report.AppendLine("配置文件: " + Settings.FilePath);
            report.AppendLine();

            try
            {
                MemorySnapshot before = MemorySnapshot.Capture(true);
                report.AppendLine(string.Format("优化前: 已用 {0} / 共 {1} ({2:0.0}%), 可用 {3}, 系统缓存 {4}, 进程 {5}, 工作集 {6}",
                    Fmt.Bytes(before.UsedPhys), Fmt.Bytes(before.TotalPhys), before.UsedPercent,
                    Fmt.Bytes(before.AvailPhys), Fmt.Bytes(before.SystemCache), before.ProcessCount,
                    Fmt.Bytes(before.WorkingSetSum)));

                Settings settings = Settings.Load();
                OptimizeOptions options = new OptimizeOptions();
                options.TrimWorkingSets = settings.TrimWorkingSets;
                options.PurgeStandby = settings.PurgeStandby;
                options.ClearFileCache = settings.ClearFileCache;
                options.ExcludedProcesses = settings.GetExcludedNames();
                options.ProtectForeground = settings.ProtectForeground;

                OptimizeResult result = Optimizer.Run(options, delegate(string message, LogLevel level)
                {
                    report.AppendLine(string.Format("[{0}] {1}", level, message));
                });

                if (result != null)
                {
                    report.AppendLine();
                    report.AppendLine("结果字段:");
                    report.AppendLine("  FreedBytes=" + result.FreedBytes);
                    report.AppendLine("  TrimmedProcesses=" + result.TrimmedProcesses);
                    report.AppendLine("  BlockedProcesses=" + result.BlockedProcesses);
                    report.AppendLine("  TotalProcesses=" + result.TotalProcesses);
                    report.AppendLine("  SkippedProcesses=" + result.SkippedProcesses);
                    report.AppendLine("  StandbyPurged=" + result.StandbyPurged);
                    report.AppendLine("  FileCacheCleared=" + result.FileCacheCleared);
                    report.AppendLine("  ActionsSucceeded=" + result.ActionsSucceeded + ", ActionsFailed=" + result.ActionsFailed);
                    report.AppendLine("  Elapsed=" + result.ElapsedSeconds.ToString("0.00") + " s");
                }

                MemorySnapshot after = MemorySnapshot.Capture(true);
                report.AppendLine();
                report.AppendLine(string.Format("优化后: 已用 {0} / 共 {1} ({2:0.0}%), 可用 {3}, 系统缓存 {4}, 进程 {5}, 工作集 {6}",
                    Fmt.Bytes(after.UsedPhys), Fmt.Bytes(after.TotalPhys), after.UsedPercent,
                    Fmt.Bytes(after.AvailPhys), Fmt.Bytes(after.SystemCache), after.ProcessCount,
                    Fmt.Bytes(after.WorkingSetSum)));
            }
            catch (Exception ex)
            {
                report.AppendLine("异常: " + ex);
            }

            try
            {
                File.WriteAllText(outputPath, report.ToString(), new UTF8Encoding(false));
            }
            catch { }

            return 0;
        }

        private static void RunShotDialog(string path)
        {
            Native.SetDpiAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            ExcludeForm form = new ExcludeForm("chrome|obs64|MyGame", true);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-5000, -5000);
            form.ShowInTaskbar = false;

            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Interval = 900;
            timer.Tick += delegate
            {
                timer.Stop();
                try
                {
                    form.Refresh();
                    using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                        bitmap.Save(path, ImageFormat.Png);
                    }
                }
                catch (Exception ex)
                {
                    try { File.WriteAllText(path + ".error.txt", ex.ToString()); }
                    catch { }
                }
                form.Close();
            };

            timer.Start();
            form.ShowDialog();
            timer.Dispose();
        }

        private static void RunShot(string path, bool optimize)
        {
            Native.SetDpiAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Settings settings = Settings.Load();
            MainForm form = new MainForm(settings, false, true);
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-5000, -5000);
            form.ShowInTaskbar = false;

            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            int phase = 0;
            timer.Interval = optimize ? 700 : 1500;
            timer.Tick += delegate
            {
                if (phase == 0 && optimize)
                {
                    phase = 1;
                    form.TriggerOptimizeForPreview();
                    timer.Interval = 5000;
                    return;
                }

                timer.Stop();
                try
                {
                    form.Refresh();
                    using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                        bitmap.Save(path, ImageFormat.Png);
                    }
                }
                catch (Exception ex)
                {
                    try { File.WriteAllText(path + ".error.txt", ex.ToString()); }
                    catch { }
                }
                form.Close();
            };

            timer.Start();
            Application.Run(form);
            timer.Dispose();
        }

        private static bool HasFlag(string[] args, string flag)
        {
            if (args == null) return false;
            foreach (string argument in args)
            {
                if (string.Equals(argument, flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string GetValue(string[] args, string key)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return i + 1 < args.Length ? args[i + 1] : null;

                string prefix = key + "=";
                if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return args[i].Substring(prefix.Length);
            }
            return null;
        }
    }
}
