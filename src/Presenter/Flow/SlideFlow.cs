using Jint;

namespace Presenter.Flow;

internal sealed class SlideFlow : IDisposable
{
    private const string Prelude = """
        const __presenter = (() => {
            const parts = [];
            let ready = false;

            globalThis.part = action => {
                if (ready || typeof action !== "function")
                    throw new TypeError("Use part(async () => { ... }) before the slide begins.");
                if (parts.length >= 500)
                    throw new RangeError("A slide can contain up to 500 parts.");
                parts.push(action);
            };

            globalThis.fadeIn = (id, ms = 400) => __fade(id, 1, ms);
            globalThis.fadeOut = (id, ms = 400) => __fade(id, 0, ms);
            globalThis.show = id => __set("show", id, "", 0, 0);
            globalThis.hide = id => __set("hide", id, "", 0, 0);
            globalThis.text = (id, value) => __set("text", id, String(value), 0, 0);
            globalThis.move = (id, x, y) => __set("move", id, "", x, y);
            globalThis.rotate = (id, degrees) => __set("rotate", id, "", degrees, 0);
            globalThis.wait = ms => __wait(ms);

            return Object.freeze({
                async start() {
                    if (typeof beforePresent === "function")
                        await beforePresent();
                    ready = true;
                    return parts.length;
                },
                async finish() {
                    if (typeof afterPresent === "function")
                        await afterPresent();
                },
                async step(index) {
                    await parts[index]();
                }
            });
        })();
        """;

    private readonly Engine _engine;
    private readonly string _script;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Func<string, double, int, CancellationToken, Task> _fade;
    private readonly Action<string, string, string, double, double> _set;

    private readonly Func<bool> _isReplaying;

    public int PartCount { get; private set; }

    public SlideFlow(
        string script,
        Func<string, double, int, CancellationToken, Task> fade,
        Action<string, string, string, double, double> set,
        Func<bool>? isReplaying = null)
    {
        _script = script;
        _fade = fade;
        _set = set;
        _isReplaying = isReplaying ?? (() => false);

        _engine = new(options =>
        {
            options.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
            options.MaxStatements(30_000);
            options.LimitMemory(24_000_000);
            options.LimitRecursion(128);
            options.CancellationToken(_lifetime.Token);
            options.Interop.AllowGetType = false;
            options.Interop.AllowSystemReflection = false;
            options.Interop.AllowWrite = false;
            options.Constraints.PromiseTimeout = TimeSpan.FromSeconds(30);
            options.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(350);
            options.Constraints.MaxArraySize = 100_000;
        });

        _engine.SetValue("__fade", (Func<string, double, double, Task>)FadeAsync);
        _engine.SetValue("__wait", (Func<double, Task>)WaitAsync);
        _engine.SetValue("__set", (Action<string, string, string, double, double>)Set);
    }

    public async Task StartAsync()
    {
        await _engine.ExecuteAsync(Prelude, cancellationToken: _lifetime.Token);
        await _engine.ExecuteAsync(_script, "Code Flow", _lifetime.Token);

        PartCount = (int)(await _engine.EvaluateAsync("__presenter.start()", cancellationToken: _lifetime.Token)).AsNumber();
    }

    public Task StepAsync(int index) => _engine.EvaluateAsync(
        $"__presenter.step({index})", cancellationToken: _lifetime.Token);

    public Task FinishAsync() => _engine.EvaluateAsync("__presenter.finish()", cancellationToken: _lifetime.Token);

    public void Cancel() => _lifetime.Cancel();

    public void Dispose()
    {
        Cancel();
        _engine.Dispose();
        _lifetime.Dispose();
    }

    private Task FadeAsync(string id, double opacity, double duration) =>
        _fade(id, Math.Clamp(opacity, 0, 1), Duration(duration), _lifetime.Token);

    private Task WaitAsync(double duration) => Task.Delay(_isReplaying() ? 0 : Duration(duration), _lifetime.Token);

    private void Set(string operation, string id, string text, double x, double y)
    {
        _lifetime.Token.ThrowIfCancellationRequested();

        if (double.IsFinite(x) && double.IsFinite(y))
            _set(operation, id, text, Math.Clamp(x, -20_000, 20_000), Math.Clamp(y, -20_000, 20_000));
    }

    private static int Duration(double milliseconds) =>
        double.IsFinite(milliseconds) ? (int)Math.Clamp(milliseconds, 0, 10_000) : 0;
}
