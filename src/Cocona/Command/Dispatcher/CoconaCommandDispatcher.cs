﻿using Cocona.Command.BuiltIn;
using Cocona.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cocona.Command.Dispatcher
{
    public class CoconaCommandDispatcher : ICoconaCommandDispatcher
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ICoconaCommandProvider _commandProvider;
        private readonly ICoconaCommandLineParser _commandLineParser;
        private readonly ICoconaCommandLineArgumentProvider _commandLineArgumentProvider;
        private readonly ICoconaCommandDispatcherPipelineBuilder _dispatcherPipelineBuilder;

        public CoconaCommandDispatcher(
            IServiceProvider serviceProvider,
            ICoconaCommandProvider commandProvider,
            ICoconaCommandLineParser commandLineParser,
            ICoconaCommandLineArgumentProvider commandLineArgumentProvider,
            ICoconaCommandDispatcherPipelineBuilder dispatcherPipelineBuilder
        )
        {
            _serviceProvider = serviceProvider;
            _commandProvider = commandProvider;
            _commandLineParser = commandLineParser;
            _commandLineArgumentProvider = commandLineArgumentProvider;
            _dispatcherPipelineBuilder = dispatcherPipelineBuilder;
        }

        public ValueTask<int> DispatchAsync()
        {
            var commandCollection = _commandProvider.GetCommandCollection();
            var args = _commandLineArgumentProvider.GetArguments();

            var matchedCommand = default(CommandDescriptor);
            if (commandCollection.All.Count > 1)
            {
                // multi-commands hosted style
                if (_commandLineParser.TryGetCommandName(args, out var commandName))
                {
                    matchedCommand = commandCollection.All
                        .FirstOrDefault(x =>
                            string.Compare(x.Name, commandName, StringComparison.OrdinalIgnoreCase) == 0 ||
                            x.Aliases.Any(y => string.Compare(y, args[0], StringComparison.OrdinalIgnoreCase) == 0)
                        );

                    if (matchedCommand == null)
                    {
                        throw new CommandNotFoundException(
                            commandName,
                            commandCollection,
                            $"The specified command '{commandName}' was not found."
                        );
                    }

                    // NOTE: Skip a first argument that is command name.
                    args = args.Skip(1).ToArray();
                }
                else
                {
                    // Use default command (NOTE: The default command must have no argument.)
                    matchedCommand = commandCollection.Primary ?? BuiltInPrimaryCommand.GetCommand();
                }
            }
            else
            {
                // single-command style
                if (commandCollection.All.Any())
                {
                    matchedCommand = commandCollection.All[0];
                }
            }

            // Found a command and dispatch.
            if (matchedCommand != null)
            {
                var commandInstance = ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, matchedCommand.CommandType);
                var ctx = new CommandDispatchContext(matchedCommand, _commandLineParser.ParseCommand(args, matchedCommand.Options, matchedCommand.Arguments), commandInstance);
                var dispatchAsync = _dispatcherPipelineBuilder.Build();
                return dispatchAsync(ctx);
            }

            throw new CommandNotFoundException(
                string.Empty,
                commandCollection,
                $"No commands are implemented yet."
            );
        }
    }
}
