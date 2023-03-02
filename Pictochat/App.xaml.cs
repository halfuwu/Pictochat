﻿using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace Pictochat;

public partial class App
{
    [DllImport("kernel32")]
    private static extern bool AllocConsole();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
#if DEBUG
        AllocConsole();
#endif
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Console.WriteLine(e.Exception.Message + e.Exception.StackTrace);
        e.Handled = true;
    }
}