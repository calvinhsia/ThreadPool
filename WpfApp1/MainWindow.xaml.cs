// see https://github.com/microsoft/vs-threading/blob/master/doc/cookbook_vs.md
//  https://github.com/microsoft/vs-threading/blob/master/doc/threadpool_starvation.md

using Microsoft.VisualStudio.Threading; // add ref to //Ref: "%VSRoot%\VSSDK\VisualStudioIntegration\Common\Assemblies\v4.0\Microsoft.VisualStudio.Threading.dll"
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private TextBox _txtStatus;
        private Button _btnGo;
        private Button _btnDbgBreak;
        private TextBox _txtUI; // not databound so must be updated from main thread

        public int NTasks { get; set; } = 10;
        public bool TaskDoAwait { get; set; } = true;
        public bool UiThreadDoAwait { get; set; } = true;
        public bool UseJTF { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            WindowState = WindowState.Maximized;
            this.Loaded += MainWindow_Loaded;
        }
        public void AddStatusMsg(string msg, params object[] args)
        {
            if (_txtStatus != null)
            {
                // we want to read the threadid 
                //and time immediately on current thread
                var dt = string.Format("[{0}],TID={1,2},",
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
        private async void MainWindow_Loaded(object sender, RoutedEventArgs eLoaded)
        {
            Title = "ThreadPool Demo";

            // xmlns:l="clr-namespace:WpfApp1;assembly=WpfApp1"
            // the C# string requires quotes to be doubled
            var strxaml =
$@"<Grid
xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
xmlns:l=""clr-namespace:{this.GetType().Namespace};assembly={
                System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().Location)}"" 
        Margin=""5,5,5,5"">
        <Grid.RowDefinitions>
            <RowDefinition Height=""auto""/>
            <RowDefinition Height=""*""/>
        </Grid.RowDefinitions>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width = ""auto""/>
            <ColumnDefinition Width = ""*""/>
        </Grid.ColumnDefinitions>

        <StackPanel Grid.Row=""0"" HorizontalAlignment=""Left"" Height=""30"" VerticalAlignment=""Top"" Orientation=""Horizontal"">
            <Label Content=""#Tasks""/>
            <TextBox Text=""{{Binding NTasks}}"" Width=""40"" />
            <CheckBox Margin=""15,0,0,10"" Content=""_TaskDoAwait""  IsChecked=""{{Binding TaskDoAwait}}"" ToolTip=""In the task, use Await, else use Thread.Sleep""/>
            <CheckBox Margin=""15,0,0,10"" Content=""_UiThreadDoAwait""  IsChecked=""{{Binding UiThreadDoAwait}}"" ToolTip=""In the main (UI) thread, use Await, else use Thread.Sleep (and the UI is not responsive!!)""/>
            <CheckBox Margin=""15,0,0,10"" Content=""Use _JTF""  IsChecked=""{{Binding UseJTF}}"" ToolTip=""Use Joinable Task Factory""/>
            <Button x:Name=""_btnGo"" Content=""_Go"" Width=""45"" />
            <Button x:Name=""_btnDbgBreak"" Content=""_DebugBreak""/>
        </StackPanel>
        <StackPanel Orientation=""Horizontal"" Grid.Column=""1"" HorizontalAlignment=""Right"">
            <TextBox x:Name=""_txtUI"" Grid.Column=""1"" Text=""sample text"" Width=""200"" IsReadOnly=""True"" IsUndoEnabled=""False"" HorizontalAlignment=""Right""/>
        </StackPanel>
        
        <TextBox x:Name=""_txtStatus"" Grid.Row=""1"" FontFamily=""Consolas"" FontSize=""10""

        IsReadOnly=""True"" VerticalScrollBarVisibility=""Auto"" IsUndoEnabled=""False"" VerticalAlignment=""Top""/>
    </Grid>
";
            var strReader = new System.IO.StringReader(strxaml);
            var xamlreader = XmlReader.Create(strReader);
            var grid = (Grid)(XamlReader.Load(xamlreader));
            grid.DataContext = this;
            this.Content = grid;
            this._txtStatus = (TextBox)grid.FindName("_txtStatus");
            this._btnGo = (Button)grid.FindName("_btnGo");
            this._btnGo.Click += BtnGo_Click;
            this._btnDbgBreak = (Button)grid.FindName("_btnDbgBreak");
            this._txtUI = (TextBox)grid.FindName("_txtUI");
            this._btnDbgBreak.Click += (o, e) =>
            {
                Debugger.Break();
            };

            _txtStatus.MouseDoubleClick += (od, ed) =>
            {
                var fname = System.IO.Path.ChangeExtension(System.IO.Path.GetTempFileName(), ".txt");
                System.IO.File.WriteAllText(fname, _txtStatus.Text);
                Process.Start(fname);
            };
            await Task.Yield();
        }
#if false
if you see tasks starting 1 second apart: 
    ThreadPool Tasks are queued...
    The scheduler waits up to 1 second for an available thread.
        If available, the task is run on that thread
        else Starvation: another threadpool thread is created.
PerfView trace, Events View, ThreadPoolWorkerThreadAdjustment Events:
Event Name                                                                 	Time MSec	Process Name  	Reason      	AverageThroughput
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Adjustment	3,370.323	WpfApp1 (7260)	Initializing	     0.000       
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Sample    	3,370.329	WpfApp1 (7260)	            	                 
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Stats     	3,370.332	WpfApp1 (7260)	            	                 
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Adjustment	4,366.461	WpfApp1 (7260)	Starvation  	     0.000       
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Adjustment	5,367.261	WpfApp1 (7260)	Starvation  	     0.000       
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Adjustment	6,366.221	WpfApp1 (7260)	Starvation  	     0.000       
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Adjustment	7,366.493	WpfApp1 (7260)	Starvation  	     0.000       
Microsoft-Windows-DotNETRuntime/ThreadPoolWorkerThreadAdjustment/Adjustment	8,366.488	WpfApp1 (7260)	Starvation  	     0.000       

#endif
        private async void BtnGo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var oWatcher = new MyThreadPoolWatcher(this))
                {
                    _btnGo.IsEnabled = false;
                    _txtStatus.Clear();
                    if (!UseJTF)
                    {
                        await DoThreadPoolAsync();
                    }
                    else
                    {
                        await DoJTFAsync();
                    }
                    AddStatusMsg($"Done");
                    ShowThreadPoolStats();
                }
            }
            catch (Exception ex)
            {
                AddStatusMsg(ex.ToString());
            }
            _btnGo.IsEnabled = true;
        }

        private async Task DoThreadPoolAsync()
        {
            var tcs = new TaskCompletionSource<int>();
            var lstTasks = new List<Task>();

            ShowThreadPoolStats();
            for (int ii = 0; ii < NTasks; ii++)
            {
                var i = ii;// local copy of iteration var
                lstTasks.Add(Task.Run(async () =>
                {
                    var tid = Thread.CurrentThread.ManagedThreadId;
                    AddStatusMsg($"Task {i} Start");
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
                            Thread.Sleep(TimeSpan.FromSeconds(0.5)); // 1 sec is the threadpool starvation threshold
                        }
                    }
                    AddStatusMsg($"Task {i} Done on " + (tid == Thread.CurrentThread.ManagedThreadId ? "Same" : "diff") + " Thread");
                }));
            }
            var taskSetDone = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                AddStatusMsg("Setting Task Completion Source");
                tcs.TrySetResult(1);
            });
            if (UiThreadDoAwait)
            {
                await Task.WhenAll(lstTasks.Union(new[] { taskSetDone }));
            }
            else
            {  // keeps the UI thread really busy
                while (lstTasks.Union(new[] { taskSetDone }).Any(t => !t.IsCompleted))
                {
                    //                    await Task.Yield();
                    Thread.Sleep(TimeSpan.FromSeconds(1));// Sleep surrenders the CPU, but the thread is still in use.
                }
            }
        }

        //async Task UpdateCounterFromAnyThread(JoinableTaskFactory jtf, int secsDelay)
        //{
        //    await jtf.SwitchToMainThreadAsync();
        //    Thread.Sleep(TimeSpan.FromSeconds(2));
        //    UpdateUiTxt();
        //}

        //private async Task DoJTFTestAsync(JoinableTaskFactory jtf)
        //{
        //    AddStatusMsg($"In {nameof(DoJTFTestAsync)}");
        //    await TaskScheduler.Default;
        //    jtf.Run(async () =>
        //       {
        //           await jtf.SwitchToMainThreadAsync();
        //           UpdateUiTxt();
        //           Thread.Sleep(TimeSpan.FromSeconds(1));

        //       });

        //}

        private async Task DoJTFAsync()
        {
            var tcs = new TaskCompletionSource<int>();
            var jtfContext = new JoinableTaskContext();

            var jtf = jtfContext.CreateFactory(jtfContext.CreateCollection());
            _txtUI.Text = "0"; // must be done on UI thread

            //            await DoJTFAsync();

            jtf.Run(async delegate
            {
                AddStatusMsg($"In Jtf.Run");
                await TaskScheduler.Default;
                AddStatusMsg($"In Jtf.Run after switch to bkgd");
                await Task.Yield();
            });

            await jtf.RunAsync(async delegate
            {
                AddStatusMsg($"In Jtf.RunAsync");
                await TaskScheduler.Default;
                AddStatusMsg($"In Jtf.RunAsync after switch to bkgd");
                await Task.Yield();
            });

            var lstTasks = new List<JoinableTask>();
            for (int ii = 0; ii < NTasks; ii++)
            {
                var i = ii;// local copy of iteration var
                //await TaskScheduler.Default; // switch to threadpool thread
                //var x = jtf.Run(async () =>
                //{
                //    return 1;
                //});


                lstTasks.Add(jtf.RunAsync(async () =>
                   {
                       AddStatusMsg($"In Task jtf.runasync {i}");
                       await TaskScheduler.Default;
                       AddStatusMsg($"In Task jtf.runasync {i} bgd");
                       await jtf.SwitchToMainThreadAsync();
                       UpdateUiTxt();
                       AddStatusMsg($"In Task jtf.runasync {i} set txt");
                   }));

            }
            lstTasks.Add(jtf.RunAsync(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                AddStatusMsg("Setting Task Completion Source");
                tcs.TrySetResult(1);
            }));
            if (UiThreadDoAwait)
            {
                await Task.WhenAll(lstTasks.Select(j => j.Task));
            }
            else
            {
                while (lstTasks.Where(j => !j.Task.IsCompleted).Count() > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1)); // need this await so ui thread can be used by other tasks. Else deadlock
                    await Task.Yield();
                }
            }
        }

        private void UpdateUiTxt()
        {
            // will throw if not on UI thread
            var val = int.Parse(_txtUI.Text);
            _txtUI.Text = (val + 1).ToString();
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }


        private void ShowThreadPoolStats()
        {
            ThreadPool.GetMaxThreads(out var workerThreads, out var completionPortThreads);  /// 2047, 1000
            AddStatusMsg($"  Max    #workerThreads={workerThreads} #completionPortThreads={completionPortThreads}");
            ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);   // 8, 8
            AddStatusMsg($"  Min    #workerThreads={minWorkerThreads} #completionPortThreads={minCompletionPortThreads}");
            ThreadPool.GetAvailableThreads(out var availWorkerThreads, out var availCompletionPortThreads);
            AddStatusMsg($"  Avail  #workerThreads={availWorkerThreads} #completionPortThreads={availCompletionPortThreads}");
        }
    }

    // https://devdiv.visualstudio.com/DevDiv/_git/VS?path=%2Fsrc%2Fenv%2Fshell%2FUIInternal%2FPackages%2FDiagnostics%2FThreadPoolWatcher.cs&_a=contents&version=GBmaster
    internal class MyThreadPoolWatcher : IDisposable
    {
        private readonly MainWindow _mainWindow;
        private readonly TaskCompletionSource<int> _tcsWatcherThread;
        private readonly CancellationTokenSource _ctsWatcherThread;
        private readonly Thread _threadWatcher;

        public MyThreadPoolWatcher(MainWindow mainWindow)
        {
            this._mainWindow = mainWindow;
            this._tcsWatcherThread = new TaskCompletionSource<int>();
            this._ctsWatcherThread = new CancellationTokenSource();
            this._threadWatcher = new Thread(async () =>
            {
                var sw = new Stopwatch();
                mainWindow.AddStatusMsg($"{nameof(MyThreadPoolWatcher)}");
                while (!_ctsWatcherThread.IsCancellationRequested)
                {
                    sw.Restart();
                    var tcs = new TaskCompletionSource<int>(0);
                    ThreadPool.QueueUserWorkItem((o) =>
                    {
                        tcs.SetResult(0);
                    });
                    await tcs.Task;
                    sw.Stop();
                    if (sw.Elapsed > TimeSpan.FromSeconds(0.5))
                    {
                        mainWindow.AddStatusMsg($"Detected ThreadPool Starvation  {sw.Elapsed.TotalSeconds:n2} secs");
                    }
                }
                _tcsWatcherThread.TrySetResult(0);
            })
            {
                Name = nameof(MyThreadPoolWatcher)
            };
            this._threadWatcher.Start();
        }

        public void Dispose()
        {
            _mainWindow.AddStatusMsg($"{nameof(MyThreadPoolWatcher)} Dispose");
            this._ctsWatcherThread.Cancel();
            while (!_tcsWatcherThread.Task.IsCompleted)
            {
                Task.Delay(TimeSpan.FromSeconds(1));
            }
            _mainWindow.AddStatusMsg($"{nameof(MyThreadPoolWatcher)} Disposed");
        }
    }
}
