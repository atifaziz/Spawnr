using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;

try
{
    var inputConsumed = new StrongBox<bool>(false);
    return Run(new Queue<string>(args), inputConsumed);
}
catch (Exception e)
{
    Console.Error.WriteLine(e.GetBaseException().Message);
    return 0xbd;
}

static int Run(Queue<string> args, StrongBox<bool> inputConsumedCell)
{
    static string ParseString(string v) => v;
    static int ParseInt(string v) => int.Parse(v, NumberStyles.None, CultureInfo.InvariantCulture);
    static double ParseDouble(string v) => double.Parse(v, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    for (var c = 0; args.TryDequeue(out var command) || c is 0; c++)
    {
        bool TryDequeueArg<T>([NotNullWhen(true)] out T? value, Func<string, T> parser)
        {
            (var success, value) =
                args.TryDequeue(out var arg) && arg is not ";"
                ? (true, parser(arg))
                : default;
            return success;
        }

        switch (command)
        {
            case null or "-":
            {
                InputDo(line =>
                {
                    try
                    {
                        _ = Run(new Queue<string>(line.Split(' ')), inputConsumedCell);
                    }
                    catch (InvalidCommandException e)
                    {
                        Console.Error.WriteLine(e.Message);
                    }
                });
                break;
            }
            case ";":
                break;
            case "prefix":
            {
                var prefix = TryDequeueArg(out var arg, ParseString) ? arg : "> ";
                TransformInput(s => prefix + s);
                break;
            }
            case "upper":
                TransformInput(s => s.ToUpperInvariant());
                break;
            case "lower":
                TransformInput(s => s.ToLowerInvariant());
                break;
            case "passthru" or "pass-thru" or "passthrough" or "pass-through":
                TransformInput(s => s);
                break;
            case "nop":
                break;
            case "lorem":
            {
                var streams = new[] { Console.Out, Console.Error };
                var i = 0;
                // cycle through counts & streams
                for (var si = 0; TryDequeueArg(out var count, ParseInt); si = (si + 1) % streams.Length)
                {
                    var stream = streams[si];
                    for (; count > 0; count--, i = (i + 1) % LoremIpsum.Samples.Length)
                        stream.WriteLine(LoremIpsum.Samples[i]);
                }
                break;
            }
            case "error":
            {
                throw new ApplicationException(TryDequeueArg(out var message, ParseString)
                                               && message.Length > 0 ? message : null);
            }
            case "exit":
            {
                var code = TryDequeueArg(out var arg, ParseInt) ? arg : 0;
                Environment.Exit(code);
                return code; // should never get here
            }
            case "sleep":
            {
                Thread.Sleep(TryDequeueArg(out var seconds, ParseDouble)
                             ? TimeSpan.FromSeconds(seconds)
                             : throw new Exception("Missing seconds argument."));
                break;
            }
            default:
            {
                throw new InvalidCommandException($"Unknown command: {command}.");
            }
        }
    }

    return 0;

    void InputDo(Action<string> action)
    {
        if (inputConsumedCell.Value)
            throw new Exception("Input has already been consumed.");

        inputConsumedCell.Value = true;

        while (true)
        {
            var line = Console.In.ReadLine();
            if (line is null)
                break;
            action(line);
        }
    }

    void TransformInput(Func<string, string> transformer) =>
        InputDo(line => Console.WriteLine(transformer(line)));
}

sealed class InvalidCommandException : Exception
{
    public InvalidCommandException(string message) : base(message) {}
}

static class LoremIpsum
{
    public static readonly string[] Samples =
    {
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
        "Nullam suscipit nunc non nulla euismod ornare.",
        "Ut auctor felis lectus, eu cursus dolor ullamcorper ac.",
        "Nam nec gravida justo.",
        "Cras sed semper elit.",
        "Orci varius natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus.",
        "Cras at ligula ut odio molestie egestas.",
        "Sed sit amet dui porttitor, bibendum libero sed, porta velit.",
        "Donec tristique risus vulputate elit hendrerit rutrum.",
        "Pellentesque mattis vestibulum purus, at hendrerit risus placerat et.",
        "Morbi est sem, convallis nec ultricies in, placerat a lorem.",
        "Vivamus vulputate euismod erat, in rutrum dui pellentesque at.",
        "Nulla eget rutrum eros, at porta augue.",
        "Nullam ac lectus vel neque efficitur faucibus ac ut augue.",
        "Ut vitae justo malesuada, consectetur odio in, pretium sapien.",
        "Nam a rutrum ante.",
        "Nulla aliquam lectus et ante congue, pellentesque vulputate sem porttitor.",
    };
}
