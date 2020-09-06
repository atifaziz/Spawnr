using System;
using System.Collections.Generic;
using System.Globalization;

static class Program
{
    static int Main(string[] args)
    {
        try
        {
            return Run(new Queue<string>(args));
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e.GetBaseException().Message);
            return 0xbd;
        }
    }

    static int Run(Queue<string> args)
    {
        switch (args.TryDequeue(out var command) ? command : null)
        {
            case null:
            {
                while (true)
                {
                    var line = Console.In.ReadLine();
                    if (line is null)
                        return 0;
                    try
                    {
                        _ = Run(new Queue<string>(line.Split(' ')));
                    }
                    catch (InvalidCommandException e)
                    {
                        Console.Error.WriteLine(e.Message);
                    }
                }
            }
            case "prefix":
            {
                var prefix = args.TryDequeue(out var arg) ? arg : "> ";
                return TransformInput(s => prefix + s);
            }
            case "upper":
                return TransformInput(s => s.ToUpperInvariant());
            case "lower":
                return TransformInput(s => s.ToLowerInvariant());
            case "nop":
                return 0;
            case "lorem":
            {
                var streams = new[] { Console.Out, Console.Error };
                var i = 0;
                // cycle through counts & streams
                for (var si = 0; args.TryDequeue(out var arg);
                     si = (si + 1) % streams.Length)
                {
                    var stream = streams[si];
                    var count = int.Parse(arg, NumberStyles.None, CultureInfo.InvariantCulture);
                    for (; count > 0; count--, i = (i + 1) % LoremIpsums.Length)
                        stream.WriteLine(LoremIpsums[i]);
                }
                return 0;
            }
            case "error":
            {
                var message = args.TryDequeue(out var arg) && arg.Length > 0
                            ? arg
                            : null;
                throw new ApplicationException(message);
            }
            case "exit":
            {
                var code = args.TryDequeue(out var arg)
                         ? int.Parse(arg, NumberStyles.None, CultureInfo.InvariantCulture)
                         : 0;
                Environment.Exit(code);
                return code; // should never get here
            }
            default:
            {
                throw new InvalidCommandException($"Unknown command: {command}.");
            }
        }

        static int TransformInput(Func<string, string> transformer)
        {
            while (true)
            {
                var line = Console.In.ReadLine();
                if (line is null)
                    return 0;
                Console.WriteLine(transformer(line));
            }
        }
    }

    sealed class InvalidCommandException : System.Exception
    {
        public InvalidCommandException(string message) : base(message) {}
    }

    static readonly string[] LoremIpsums =
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
