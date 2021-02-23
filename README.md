# Spawnr

Spawnr is a [.NET Standard] library that makes [`Process.Start`][ps] simple.
It has a functional API for spawning a process and harnessing its output as a
reactive stream.


## Usage

The simplest way to use the library is to [statically import] the
`Spawner.Spawnable` class:

```c#
using static Spawnr.Spawnable;
```

This makes the principal `Spawn` method available without requiring any type
qualification. It also adds several extension methods. The remainder of this
document will assume the static import above.

The following statements run `dotnet` with zero command-line arguments:

```c#
var dotnet = Spawn("dotnet", ProgramArguments.Empty);
var exitCode = await dotnet.Execute();
Console.WriteLine($"Exit code = {exitCode}");
```

Note that `Spawn` does not actually run the program. Instead it configures how
the program will be run. In a second and later step, you run the program. In the
example above, the running is actually done by `Execute`.

`Execute` simply runs the program as an _asynchronous_ operation that
completes when the program exits. It does not redirect any of the spawned
program's standard streams and returns a `Task<int>` where the integer result
represents the exit code.

The benefit of separating the configuration and the actual execution of a
program is that you can run the same configuration several times.

```c#
var dotnet = Spawn("dotnet", ProgramArguments.Empty);
// first run
var exitCode1 = await dotnet.Execute();
Console.WriteLine($"Exit code (1) = {exitCode1}");
// second run
var exitCode2 = await dotnet.Execute();
Console.WriteLine($"Exit code (2) = {exitCode2}");
```



The most basic interface

```c#
public interface ISpawner
{
    IObservable<T> Spawn<T>(string path, SpawnOptions options,
                            Func<string, T>? stdout,
                            Func<string, T>? stderr);
}
```


  [ps]: [https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.start]
  [.NET Standard]: https://docs.microsoft.com/en-us/dotnet/standard/net-standard
  [statically import]: https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/using-static
