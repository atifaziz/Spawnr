#r "bin/Debug/netstandard2.1/Spawnr.dll"
#r "nuget: System.Reactive, 4.0.0"

using System.Reactive.Linq;
using Spawnr;
using static Spawnr.Spawnable;

var wc = Spawn("wc", ProgramArguments.Empty);

_ = await
    wc.EmptyInput()
      .Execute(ExecuteOptions.HideStandardError | ExecuteOptions.HideStandardOutput)
      //.Execute()
    ;

Environment.Exit(0);

var dotnet = Spawn("dotnet", ProgramArguments.Empty);

//var spawn =

//        .AddArgument("--list-runtimes")
        //.Input(Observable.Empty<string>())
      //  .Input(new[] { "The quick brown fox jumps over the lazy dog. "}.ToObservable())
        ;

Console.WriteLine(
    dotnet.CaptureOutputs()
          .Options(opts => opts.IgnoreExitCode())
          .IgnoreElements());
