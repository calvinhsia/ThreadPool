using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public int NTasks { get; set; } = 12;
        public bool TaskDoAwait { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            WindowState = WindowState.Maximized;
            this.DataContext = this;
            this.Loaded += MainWindow_Loaded;
        }
        public void AddStatusMsg(string msg, params object[] args)
        {
            if (_txtStatus != null)
            {
                // we want to read the threadid 
                //and time immediately on current thread
                var dt = string.Format("[{0}],{1,2},",
                    DateTime.Now.ToString("hh:mm:ss:fff"),
                    Thread.CurrentThread.ManagedThreadId);
                _txtStatus.Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        // this action executes on main thread
                        if (args.Length == 0) // in cases the msg has embedded special chars like "{"
                        {
                            var str = string.Format(dt + "{0}" + Environment.NewLine, new object[] { msg });
                            _txtStatus.AppendText(str);
                        }
                        else
                        {
                            var str = string.Format(dt + msg + "\r\n", args);
                            _txtStatus.AppendText(str);

                        }
                        _txtStatus.ScrollToEnd();
                    }));
            }
        }
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _txtStatus.MouseDoubleClick += (od, ed) =>
            {
                var fname = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".txt");
                System.IO.File.WriteAllText(fname, _txtStatus.Text);
                Process.Start(fname);
            };
            await Task.Yield();
        }

        private async void BtnGo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _txtStatus.Clear();
                var tcs = new TaskCompletionSource<int>();
                var aTasks = new MyTask[NTasks];
                ShowThreadPoolStats();
                for (int ii = 0; ii < NTasks; ii++)
                {
                    var i = ii;
                    aTasks[i] = new MyTask($"T{i}", async () =>
                    {
                        AddStatusMsg($"Task {aTasks[i]} Start");
                        // in this method we do the Task work that might take a long time (several seconds)
                        // if the work can be run async, then do so.
                        // keep in mind how the thread that does the work is used: 
                        // if it's calling Thread.Sleep, the CPU load will be low, but the threadpool thread will be occupied


                        if (TaskDoAwait)
                        {
                            await tcs.Task;
                        }
                        else
                        {
                            while (!tcs.Task.IsCompleted)
                            {
//                                await Task.Delay(TimeSpan.FromSeconds(1));
                                Thread.Sleep(TimeSpan.FromSeconds(3));
                            }
                        }
                        AddStatusMsg($"Task {aTasks[i]} Done");
                    });
                }
                var taskSetDone = Task.Run(async () =>
                  {
                      await Task.Delay(TimeSpan.FromSeconds(10));
                      AddStatusMsg("Setting Task Completion Source");
                      tcs.TrySetResult(1);
                  });
                await Task.WhenAll(aTasks.Select(t => t.GetTask()).Union(new[] { taskSetDone }));
                AddStatusMsg($"Done");
                ShowThreadPoolStats();
            }
            catch (Exception ex)
            {
                AddStatusMsg(ex.ToString());
            }

        }

        private void ShowThreadPoolStats()
        {
            ThreadPool.GetMaxThreads(out var workerThreads, out var completionPortThreads);  /// 2047, 1000
            AddStatusMsg($" #workerThreads={workerThreads} #completionPortThreads={completionPortThreads}");
            ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);   // 8, 8
            AddStatusMsg($"    Min  #workerThreads={minWorkerThreads} #completionPortThreads={minCompletionPortThreads}");
        }

        private void BtnDbgBreak_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debugger.Break();

        }
    }
    class MyTask
    {
        private readonly string _desc;
        public readonly Action _act;

        public MyTask(string desc, Action act)
        {
            _desc = desc;
            _act = act;
        }
        public Task GetTask()
        {
            return Task.Run(_act);
        }
        public override string ToString()
        {
            return $"{_desc}";
        }
    }
}
