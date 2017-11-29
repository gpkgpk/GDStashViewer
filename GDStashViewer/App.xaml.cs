using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using WpfExceptionViewer;

namespace GDStashViewer
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
#if !DEBUG
			DispatcherUnhandledException += Application_DispatcherUnhandledException;
#endif
		}
#if !DEBUG
		private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
		{
			ExceptionViewer ev = new ExceptionViewer("Unhandled Exception (Error)", e.Exception,this.MainWindow);
			ev.ShowDialog();
			//if (ev.ShowDialog() == true)
			//	e.Handled = true;
			//else
			//	e.Handled = true;
			e.Handled = true;
			this.MainWindow.Cursor = Cursors.Arrow;
		}
#endif
	}
}