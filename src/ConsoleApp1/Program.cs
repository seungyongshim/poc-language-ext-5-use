using LanguageExt;
using LanguageExt.Traits;
using Microsoft.Extensions.DependencyInjection;
using static LanguageExt.Prelude;

Console.WriteLine("## 1. Expect");
await Expect();

Console.WriteLine("\n## 2. use using IO");
await Case2();

Console.WriteLine("\n## 3. use using IO w/ EnvIO");
await Case3();

Console.WriteLine("\n## 4. use using Eff");
await Case4();

Console.WriteLine("\n## 5. use using Eff w/ Runtime");
await Case5();


async Task Expect()
{
    await using var x = new DisposableClass("1");
}

async Task Case2()
{
    var io =
        from _1 in use(() => new DisposableClass("2"))
        from __ in liftIO(() => throw new Exception("crash"))
        from _2 in release(_1)
        select unit;

    try
    {
        await io.RunAsync();
    }
    catch { }
}

async Task Case3()
{
    var io =
        from _1 in use(() => new DisposableClass("3"))
        from __ in liftIO(() => throw new Exception("crash"))
        from _2 in release(_1)
        select unit;

    try
    {
        using var envio = EnvIO.New();
        await io.RunAsync(envio);
    }
    catch { }
}

async Task Case4()
{
    Eff<Unit> effect =
        from _1 in use(() => new DisposableClass("4"))
        from __ in liftIO(() => throw new Exception("crash"))
        from _2 in release(_1)
        select unit;

    await effect.RunAsync();
}



async Task Case5()
{
    ServiceCollection services = new();
    services.AddKeyedScoped<DisposableClass>("5");
    await using var sp = services.BuildServiceProvider();

    var effect =
        from scope in use(liftEff((IServiceProvider rt) => rt.CreateAsyncScope()))
        from _0 in localEff<IServiceProvider, IServiceProvider, Unit>(rt => scope.ServiceProvider, 
            from _1 in liftEff((IServiceProvider rt) => rt.GetRequiredKeyedService<DisposableClass>("5"))
            from __ in liftIO(action: static () => throw new Exception("crash"))
            select unit)
        select unit;

    await effect.RunAsync(sp);
}

public class DisposableClass([ServiceKey] string Id) : IDisposable, IAsyncDisposable
{
    public void Dispose()
    {
        Console.WriteLine($"- Disposed {Id}");
    }
    public ValueTask DisposeAsync()
    {
        Console.WriteLine($"- DisposedAsync {Id}");

        return ValueTask.CompletedTask;
    }
}

public readonly record struct Runtime
(
    IServiceProvider ServiceProvider
) : Local<Eff<Runtime>, IServiceProvider>
{
    static K<Eff<Runtime>, A> asks<A>(Func<Runtime, A> f) =>
    Readable.asks<Eff<Runtime>, Runtime, A>(f);

    static K<Eff<Runtime>, A> local<A>(Func<Runtime, Runtime> f, K<Eff<Runtime>, A> ma) =>
        Readable.local(f, ma);
    static K<Eff<Runtime>, A> Local<Eff<Runtime>, IServiceProvider>.With<A>(Func<IServiceProvider, IServiceProvider> f, K<Eff<Runtime>, A> ma) =>
        local(rt => rt with
        {
            ServiceProvider = f(rt.ServiceProvider)
        }, ma);

    static K<Eff<Runtime>, IServiceProvider> Has<Eff<Runtime>, IServiceProvider>.Ask =>
        asks(rt => rt.ServiceProvider);
}
