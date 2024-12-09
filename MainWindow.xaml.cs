namespace TianciWPF
{
    /// <summary>
    /// 资源管理
    /// </summary>
    using System.Windows;
    using System.Diagnostics;
    using System.Linq;
    using System.Management;
    using System.Timers;

    public partial class MainWindow : Window
    {
        /// <summary>
        /// CPU
        /// </summary>
        private PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");

        /// <summary>
        /// 内存
        /// </summary>
        private PerformanceCounter memoryCounter = new PerformanceCounter("Memory", "Available MBytes");

        /// <summary>
        /// 磁盘
        /// </summary>
        private PerformanceCounter diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "0 C: D:");

        /// <summary>
        /// 网速
        /// </summary>

        private PerformanceCounter networkCounter = new PerformanceCounter("Network Interface", "Bytes Total/sec", "Realtek PCIe GbE Family Controller");//Qualcomm Atheros QCA9377 Wireless Network Adapter

        /// <summary>
        /// 定时器
        /// </summary>
        private Timer timer = new Timer(1000); // 每秒更新一次

        /// <summary>
        /// CPU数
        /// </summary>
        private Dictionary<int, PerformanceCounter> cpuCounters;

        /// <summary>
        /// 刷新进程的定时器
        /// </summary>
        private Timer refreshTimer = new Timer(2000); // 每2秒更新一次
        public MainWindow()
        {
            InitializeComponent();
            // 列出所有网络适配器
            //foreach (var instance in new PerformanceCounterCategory("Network Interface").GetInstanceNames())
            //{
            //     MessageBox.Show(instance);
            //}
            // 禁用最大化按钮
            this.ResizeMode = ResizeMode.CanMinimize;

            // 窗口启动时居中显示
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            cpuCounters = new Dictionary<int, PerformanceCounter>();

            timer.Elapsed += UpdateStats;
            timer.Start();

            refreshTimer.Elapsed += (s, e) => LoadProcesses();
            refreshTimer.Start();

            LoadProcesses();
        }

        /// <summary>
        /// 获取电脑内存总数
        /// </summary>
        /// <returns></returns>
        private float GetTotalMemory()
        {
            // 依赖System.Management需要安装nuget包
            var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
            ulong totalMemory = 0;
            foreach (var obj in searcher.Get())
            {
                totalMemory += (ulong)obj["Capacity"];
            }
            return totalMemory / 1024 / 1024; // 转换为 MB
        }

        /// <summary>
        /// 更新页面上半部分进度条
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UpdateStats(object? sender, ElapsedEventArgs e)
        {
            var totalMemory = GetTotalMemory();
            Dispatcher.Invoke(() =>
            {
                // CPU
                var cpuUsage = cpuCounter.NextValue();
                CpuUsageBar.Value = cpuUsage;
                CpuUsageText.Text = $"{cpuUsage:F1}% / 100%";

                // 内存
                var availableMemory = memoryCounter.NextValue();
                var usedMemory = totalMemory - availableMemory;
                var memoryUsage = (usedMemory / totalMemory) * 100;
                MemoryUsageBar.Value = memoryUsage;
                MemoryUsageText.Text = $"{usedMemory:F1} MB / {totalMemory:F1} MB";

                // 硬盘
                var diskUsage = diskCounter.NextValue();
                DiskUsageBar.Value = diskUsage;
                DiskUsageText.Text = $"{diskUsage:F1}% / 100%";

                // 网络速度
                var networkSpeed = networkCounter.NextValue() / 1024f / 100f;// 转为Mbps
                NetworkSpeedBar.Value = networkSpeed;
                NetworkSpeedText.Text = $"{(networkSpeed):F1} Mbps";
            });
        }

        /// <summary>
        /// 进程数据
        /// </summary>
        private void LoadProcesses()
        {
            var processes = Process.GetProcesses();

            // 创建或更新 PerformanceCounter
            foreach (var process in processes)
            {
                if (!cpuCounters.ContainsKey(process.Id))
                {
                    try
                    {
                        cpuCounters[process.Id] = new PerformanceCounter("Process", "% Processor Time", process.ProcessName, true);
                        cpuCounters[process.Id].NextValue(); // 初始化数据
                    }
                    catch
                    {
                        // 忽略可能的异常（例如权限不足导致某些进程无法创建性能计数器）
                    }
                }
            }

            // 收集进程信息
            var processList = processes.Select(p =>
            {
                float cpuUsage = 0;

                // 获取 CPU 使用率
                if (cpuCounters.ContainsKey(p.Id))
                {
                    try
                    {
                        cpuUsage = cpuCounters[p.Id].NextValue() / Environment.ProcessorCount;
                    }
                    catch
                    {
                        cpuUsage = 0;
                    }
                }

                return new ProcessList
                {
                    Name = p.ProcessName,
                    Id = p.Id,
                    CpuUsage = cpuUsage,
                    MemoryUsage = p.WorkingSet64 / 1024 / 1024
                };
            }).ToList();

            // 更新 UI
            Dispatcher.Invoke(() =>
            {
                ProcessListView.ItemsSource = processList.OrderByDescending(p=> p.CpuUsage);
            });
        }
    }

    /// <summary>
    /// 进程模型类
    /// </summary>
    public class ProcessList
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public float CpuUsage { get; set; }

        public float MemoryUsage { get; set; }
    }
}