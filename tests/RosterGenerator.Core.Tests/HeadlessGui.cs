using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Headless;

using RosterGenerator.Gui;

namespace RosterGenerator.Core.Tests;

/// <summary>
/// One headless Avalonia UI thread, shared by every GUI test.
///
/// <para>Avalonia's setup is process-global — a second
/// <c>SetupWithoutStarting</c> anywhere in the test run throws "Setup was
/// already called" — and the dispatcher belongs to the thread that called it.
/// So there is exactly one thread here, started once, and every test posts its
/// work to it.</para>
/// </summary>
internal static class HeadlessGui
{
    private static readonly BlockingCollection<Action> Work = new();
    private static readonly object Gate = new();
    private static bool _started;

    /// <summary>Builds a window, runs the body against it, and closes it.</summary>
    public static void Run(Action<MainWindow> body)
    {
        Start();

        Exception? failure = null;
        using var done = new ManualResetEventSlim();
        Work.Add(() =>
        {
            MainWindow? window = null;
            try
            {
                window = new MainWindow();
                window.Show();
                body(window);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                try { window?.Close(); } catch { /* closing a failed window must not mask the failure */ }
                done.Set();
            }
        });

        done.Wait();
        if (failure is not null)
        {
            throw failure;
        }
    }

    private static void Start()
    {
        lock (Gate)
        {
            if (_started)
            {
                return;
            }

            using var ready = new ManualResetEventSlim();
            var thread = new Thread(() =>
            {
                AppBuilder.Configure<App>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                    .SetupWithoutStarting();
                ready.Set();

                foreach (var item in Work.GetConsumingEnumerable())
                {
                    item();
                }
            })
            {
                IsBackground = true,
                Name = "headless-avalonia",
            };

            thread.Start();
            ready.Wait();
            _started = true;
        }
    }
}
