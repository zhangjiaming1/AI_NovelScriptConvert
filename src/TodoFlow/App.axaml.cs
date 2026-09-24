using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TodoFlow.Infrastructure;
using TodoFlow.ViewModels;
using TodoFlow.Views;

namespace TodoFlow;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var repository = new JsonTodoRepository();
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(repository)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}