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

        public int NTasks { get; set; } = 12;
        public bool TaskDoAwait { get; set; }
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
        private async void MainWindow_Loaded(object sender, RoutedEventArgs eLoaded)
        {
            Title = "ThreadPool Demo";
            var nameSpace = this.GetType().Namespace;
            var asm = System.IO.Path.GetFileNameWithoutExtension(
                Assembly.GetExecutingAssembly().Location);

            var xmlns = string.Format(
@"xmlns:l=""clr-namespace:{0};assembly={1}""", nameSpace, asm);
            //there are a lot of quotes (and braces) in XAML
            //and the C# string requires quotes to be doubled
            var strxaml =
@"<Grid
xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
" + xmlns + // add our xaml namespace. Can't use @"" because binding in braces
@" Margin=""5,5,5,5"">
        <Grid.RowDefinitions>
            <RowDefinition Height=""auto""/>
            <RowDefinition Height=""*""/>
        </Grid.RowDefinitions>
        <StackPanel Grid.Row=""0"" HorizontalAlignment=""Left"" Height=""30"" VerticalAlignment=""Top"" Orientation=""Horizontal"">
            <Label Content=""#Tasks""/>
            <TextBox Text=""{Binding NTasks}"" Width=""40"" />
            <CheckBox Content=""_TaskDoAwait""  IsChecked=""{Binding TaskDoAwait}"" ToolTip=""In the task, use Await, else use Thread.Sleep""/>
            <CheckBox Content=""_UiThreadDoAwait""  IsChecked=""{Binding UiThreadDoAwait}"" ToolTip=""In the main (UI) thread, use Await, else use Thread.Sleep""/>
            <CheckBox Content=""Use JTF""  IsChecked=""{Binding UseJTF}"" ToolTip=""Use Joinable Task Factory""/>
            <Button x:Name=""btnGo"" Content=""_Go"" Width=""45"" />
            <Button x:Name=""btnDbgBreak"" Content=""_DebugBreak""/>

        </StackPanel>
        <TextBox x:Name=""_txtStatus"" Grid.Row=""1"" IsReadOnly=""True"" IsUndoEnabled=""False"" MaxHeight=""400"" VerticalAlignment=""Top""/>
    </Grid>
";
            var strReader = new System.IO.StringReader(strxaml);
            var xamlreader = XmlReader.Create(strReader);
            var grid = (Grid)(XamlReader.Load(xamlreader));
            grid.DataContext = this;
            this.Content = grid;
            this._txtStatus = (TextBox)grid.FindName("_txtStatus");
            this._btnGo = (Button)grid.FindName("btnGo");
            this._btnGo.Click += BtnGo_Click;
            this._btnDbgBreak = (Button)grid.FindName("btnDbgBreak");
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
                _btnGo.IsEnabled = false;
                _txtStatus.Clear();
                if (!UseJTF)
                {
                    await DoThreadPoolAsync();
                }
                else
                {
                    var tcs = new TaskCompletionSource<int>();
                    var jtfContext = new JoinableTaskContext();
                    var jtf = new JoinableTaskFactory(jtfContext);

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
                }

                AddStatusMsg($"Done");
                ShowThreadPoolStats();
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
            var aTasks = new MyTask[NTasks];
            ShowThreadPoolStats();
            for (int ii = 0; ii < NTasks; ii++)
            {
                var i = ii;
                aTasks[i] = new MyTask($"T{i}", async () =>
                {
                    var tid = Thread.CurrentThread.ManagedThreadId;
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
                            Thread.Sleep(TimeSpan.FromSeconds(0.5)); // 1 sec is the threadpool starvation threshold
                        }
                    }
                    AddStatusMsg($"Task {aTasks[i]} Done" + (tid == Thread.CurrentThread.ManagedThreadId ? "Same" : "diff") + "Thread");
                });
            }
            var taskSetDone = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                AddStatusMsg("Setting Task Completion Source");
                tcs.TrySetResult(1);
            });
            if (UiThreadDoAwait)
            {
                await Task.WhenAll(aTasks.Select(t => t.GetTask()).Union(new[] { taskSetDone }));
            }
            else
            {
                while (aTasks.Select(t => t.GetTask()).Union(new[] { taskSetDone }).Any(t => !t.IsCompleted))
                {
                    await Task.Yield();
                    Thread.Sleep(TimeSpan.FromSeconds(1));

                }

            }
        }

        private void ShowThreadPoolStats()
        {
            ThreadPool.GetMaxThreads(out var workerThreads, out var completionPortThreads);  /// 2047, 1000
            AddStatusMsg($" #workerThreads={workerThreads} #completionPortThreads={completionPortThreads}");
            ThreadPool.GetMinThreads(out var minWorkerThreads, out var minCompletionPortThreads);   // 8, 8
            AddStatusMsg($"    Min  #workerThreads={minWorkerThreads} #completionPortThreads={minCompletionPortThreads}");
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
