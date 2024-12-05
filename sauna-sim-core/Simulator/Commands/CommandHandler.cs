using SaunaSim.Core.Simulator.Aircraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SaunaSim.Core.Simulator.Commands
{
    public class CommandHandler
    {
        private Queue<IAircraftCommand> commandQueue;
        private object commandQueueLock;
        private bool processingCommand;
        private SimAircraftHandler _handler;

        public CommandHandler(SimAircraftHandler handler)
        {
            commandQueue = new Queue<IAircraftCommand>();
            processingCommand = false;
            commandQueueLock = new object();
            _handler = handler;
        }

        private void ProcessNextCommand()
        {
            if (processingCommand)
            {
                return;
            }

            processingCommand = true;
            while (commandQueue.Count > 0)
            {
                IAircraftCommand cmd;
                lock (commandQueueLock)
                {
                    cmd = commandQueue.Dequeue();
                }

                // Generate random delay
                int delay = new Random().Next(0, 3000);
                Thread.Sleep(delay);

                cmd.ExecuteCommand();
            }
            processingCommand = false;
        }

        public bool QueueCommand(IAircraftCommand command)
        {
            if (command != null)
            {
                // Add to Queue
                lock (commandQueueLock)
                {
                    commandQueue.Enqueue(command);
                }

                // Launch thread to execute queue
                Thread t = new Thread(ProcessNextCommand);
                t.Start();
                return true;
            }
            return false;
        }

        public List<string> HandleCommand(string commandName, SimAircraft aircraft, List<string> args, Action<string> logger)
        {
            string cmdNameNormalized = commandName.ToLower();
            IAircraftCommand cmd = null;

            // Get Command
            switch (cmdNameNormalized)
            {
                case "pauseall":
                case "pall":
                    _handler.AllPaused = true;
                    logger("All Aircraft Paused");
                    break;
                case "unpauseall":
                case "upall":
                    _handler.AllPaused = false;
                    logger("All Aircraft Unpaused");
                    break;
                case "pause":
                case "p":
                    logger($"{aircraft.Callsign} paused");
                    aircraft.Paused = true;
                    break;
                case "unpause":
                case "up":
                    logger($"{aircraft.Callsign} unpaused");
                    aircraft.Paused = false;
                    break;
                case "remove":
                case "delete":
                case "del":
                    logger($"Removing {aircraft.Callsign}");
                    Task.Run(() =>
                    {
                        _handler.RemoveAircraftByCallsign(aircraft.Callsign);
                    });
                    break;
                case "fh":
                    cmd = new FlyHeadingCommand();
                    break;
                case "tl":
                    cmd = new TurnLeftHeadingCommand();
                    break;
                case "tr":
                    cmd = new TurnRightHeadingCommand();
                    break;
                case "tlb":
                    cmd = new TurnLeftByHeadingCommand();
                    break;
                case "trb":
                    cmd = new TurnRightByHeadingCommand();
                    break;
                case "fph":
                    cmd = new FlyPresentHeadingCommand();
                    break;
                case "dephdg":
                case "dh":
                case "lh":
                case "leavehdg":
                    cmd = new DepartOnHeadingCommand();
                    break;
                case "hold":
                    cmd = new HoldCommand();
                    break;
                case "loc":
                case "fac":
                case "fat":
                    cmd = new LocCommand(_handler.MagTileManager);
                    break;
                case "ils":
                case "app":
                case "apch":
                    cmd = new IlsCommand(_handler.MagTileManager);
                    break;
                case "to":
                case "takeoff":
                    cmd = new TakeOffCommand(_handler.MagTileManager);
                    break;
                case "ga":
                case "goaround":
                    cmd = new GoAround();
                    break;
                case "int":
                case "intercept":
                    cmd = new InterceptCourseCommand();
                    break;
                case "direct":
                case "dct":
                case "dir":
                    cmd = new DirectWaypointCommand();
                    break;
                case "speed":
                case "spd":
                    cmd = new SpeedCommand();
                    break;
                case "alt":
                case "cm":
                case "dm":
                case "clm":
                case "climb":
                case "des":
                case "descend":
                    cmd = new AltitudeCommand();
                    break;

                // Temp commands
                case "spdbrk":
                    try
                    {
                        double spdBrake = Convert.ToDouble(args[0]);
                        aircraft.Data.SpeedBrakePos = spdBrake;
                        args.RemoveAt(0);
                        logger($"{aircraft.Callsign} Speed Brake set to {aircraft.Data.SpeedBrakePos:0.00}");
                    } catch (Exception)
                    {
                        logger($"ERROR: Speed Brake Pos Invalid");
                    }
                    break;

                case "sid":
                    cmd = new SidCommand();
                    break;
                case "config":
                    try
                    {
                        int config = Convert.ToInt32(args[0]);
                        if (config < 0)
                        {
                            config = 0;
                        } else if (config >= aircraft.PerformanceData.ConfigList.Count)
                        {
                            config = aircraft.PerformanceData.ConfigList.Count - 1;
                        }
                        aircraft.Data.Config = config;
                        args.RemoveAt(0);
                        logger($"{aircraft.Callsign} Config set to {aircraft.Data.Config}");
                    } catch (Exception)
                    {
                        logger($"ERROR: Config Invalid");
                    }
                    break;
                default:
                    logger($"ERROR: Command {commandName} not valid!");
                    return args;
            }

            if (cmd != null)
            {
                // Get new args after processing command
                cmd.Aircraft = aircraft;
                cmd.Logger = logger;

                // Make sure command is valid before running.
                if (cmd.HandleCommand(ref args))
                {
                    // Add to Queue
                    lock (commandQueueLock)
                    {
                        commandQueue.Enqueue(cmd);
                    }

                    // Launch thread to execute queue
                    Thread t = new Thread(ProcessNextCommand);
                    t.Start();
                }
            }

            // Return args
            return args;
        }

        
    }
}
