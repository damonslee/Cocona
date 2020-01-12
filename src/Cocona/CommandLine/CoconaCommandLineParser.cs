﻿using Cocona.Command;
using Cocona.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace Cocona.CommandLine
{
    /// <summary>
    /// Cocona default implementation of command-line parser.
    /// </summary>
    public class CoconaCommandLineParser : ICoconaCommandLineParser
    {
        public bool TryGetCommandName(string[] args, [MaybeNullWhen(false)] out string commandName)
        {
            if (args.Length == 0)
            {
                commandName = null;
                return false;
            }

            if (args[0].StartsWith("-"))
            {
                commandName = null;
                return false;
            }
            else
            {
                commandName = args[0];
                return true;
            }
        }

        public ParsedCommandLine ParseCommand(IReadOnlyList<string> args, IReadOnlyList<CommandOptionDescriptor> optionDescs, IReadOnlyList<CommandArgumentDescriptor> argumentDescs)
        {
            var optionbyLongName = optionDescs
                .ToDictionary(k => k.Name);
            var optionbyShortName = optionDescs
                .SelectMany(xs => xs.ShortName.Select(x => (ShortName: x, Option: xs)))
                .ToDictionary(k => k.ShortName, v => v.Option);

            // parse options
            var index = 0;
            var options = new List<CommandOption>();
            var unknownOptions = new List<string>();
            for (var i = 0; i < args.Count; i++)
            {
                if (!args[i].StartsWith('-'))
                {
                    break;
                }

                index++;

                // end of options
                if (args[i] == "--")
                {
                    break;
                }

                if (args[i].StartsWith("--"))
                {
                    // long-named
                    var equalPos = args[i].IndexOf('=');
                    if (equalPos > -1)
                    {
                        // --option=value
                        var partLeft = args[i].Substring(2, equalPos - 2);
                        var partRight = args[i].Substring(equalPos + 1);

                        if (optionbyLongName.TryGetValue(partLeft, out var option))
                        {
                            if (option.OptionType == typeof(bool))
                            {
                                // Boolean (flag)
                                options.Add(new CommandOption(option, "true"));
                            }
                            else
                            {
                                // Non-boolean (the option may have some value)
                                options.Add(new CommandOption(option, partRight));
                            }
                            continue;
                        }
                    }
                    else
                    {
                        // --option
                        // --option value
                        if (optionbyLongName.TryGetValue(args[i].Substring(2), out var option))
                        {
                            if (option.OptionType == typeof(bool))
                            {
                                // Boolean (flag)
                                options.Add(new CommandOption(option, "true"));
                            }
                            else
                            {
                                // Non-boolean (the option may have some value)
                                options.Add(new CommandOption(option, (i + 1 == args.Count) ? null : args[++i])); // consume a next argment
                                index++;
                            }
                            continue;
                        }
                    }

                    unknownOptions.Add(args[i].Substring(2).ToString());
                }
                else
                {
                    // short-named
                    for (var j = 1; j < args[i].Length; j++)
                    {
                        if (optionbyShortName.TryGetValue(args[i][j], out var option))
                        {
                            if (option.OptionType == typeof(bool))
                            {
                                // Boolean (flag)
                                options.Add(new CommandOption(option, "true"));
                            }
                            else
                            {
                                // Non-boolean (the option may have some value)
                                if (args.Count == i + 1 && args[i].Length == j + 1)
                                {
                                    // ["-foo", "-I"]
                                    // -foo -I
                                    // ------^
                                    options.Add(new CommandOption(option, null));
                                }
                                else if (args[i].Length - 1 == j)
                                {
                                    // ["-foo", "-I", "../path/", "-fgh", "-i", "-j"]
                                    // -foo -I ../path/ -fgh -i -j
                                    // ------^^
                                    options.Add(new CommandOption(option, args[++i]));
                                    index++;
                                }
                                else if (args[i][j + 1] == '=')
                                {
                                    // ["-foo", "-I=../path/", "-fgh", "-i", "-j"]
                                    // -foo -I=../path/ -fgh -i -j
                                    // ------^^
                                    options.Add(new CommandOption(option, args[i].Substring(j + 2)));
                                }
                                else
                                {
                                    // ["-foo", "-I../path/", "-fgh", "-i", "-j"]
                                    // -foo -I../path/ -fgh -i -j
                                    // ------^
                                    options.Add(new CommandOption(option, args[i].Substring(j + 1)));
                                }

                                break;
                            }
                        }
                        else
                        {
                            unknownOptions.Add(args[i][j].ToString());
                        }
                    }
                }
            }

            var arguments = args.Skip(index).Select(x => new CommandArgument(x)).ToArray();

            return new ParsedCommandLine(options, arguments, unknownOptions);
        }
    }
}
