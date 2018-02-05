﻿//-----------------------------------------------------------------------------
// FILE:	    DockerCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>docker</b> commands.
    /// </summary>
    public class DockerCommand : CommandBase
    {
        private const string usage = @"
Runs a Docker command on the cluster.  All command line arguments
and options as well are passed through to the Docker CLI.

USAGE:

    neon [OPTIONS] docker [ARGS...]     - Invokes a Docker command

ARGUMENTS:

    ARGS        - The standard Docker command arguments and options

OPTIONS :

    --help          - Prints this command's help.
    --node=NODE     - Specifies the target node.  The Docker command 
                      will be executed on the first manager node when  
                      this is not specified.
    --no-upload     - Do not upload file arguments (see note below).

NOTE:

The [neon] tool automatically uploads command file arguments from
the local operator's workstation to the node where the Docker command
will be executed as a convienence.  You can use the [--no-upload]
option to disable this behavior.

The commands currently supporting auto upload are:

    neon docker deploy
    neon docker stack deploy
    neon docker secret create

The other Docker commands supporting file arguments or that take
input from [stdin] will need to be run directly on the host using
the [neon exec] command.
";
        private ClusterProxy        cluster;

        private const string remoteDockerPath = "/usr/bin/docker";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "docker" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--node", "--no-upload" }; }
        }

        /// <inheritdoc/>
        public override string SplitItem
        {
            get { return "docker"; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            // Split the command line on "docker".

            var split = commandLine.Split("docker");

            var leftCommandLine  = split.Left;
            var rightCommandLine = split.Right;

            // Basic initialization.

            if (leftCommandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            // Initialize the cluster and connect to a manager.

            var clusterLogin = Program.ConnectCluster();

            cluster = new ClusterProxy(clusterLogin, Program.CreateNodeProxy<NodeDefinition>);

            // Determine which node we're going to target.

            SshProxy<NodeDefinition>    node;
            var                         nodeName = leftCommandLine.GetOption("--node", null);

            if (!string.IsNullOrEmpty(nodeName))
            {
                node = cluster.GetNode(nodeName);
            }
            else
            {
                node = cluster.FirstManager;
            }

            // A handful commands upload files and need to be run as a bundle.

            if (!leftCommandLine.HasOption("--no-upload"))
            {
                var arg1 = rightCommandLine.Arguments.AtIndexOrDefault(0);
                var arg2 = rightCommandLine.Arguments.AtIndexOrDefault(1);

                if (arg1 == "deploy")
                {
                    Deploy(node, rightCommandLine);
                }
                else if (arg1 == "stack" && arg2 == "deploy")
                {
                    Deploy(node, rightCommandLine);
                }
                else if (arg1 == "secret" && arg2 == "create")
                {
                    SecretCreate(node, rightCommandLine);
                }
            }

            // Otherwise, we're just going to execute the command as is.

            var response = node.SudoCommand($"{remoteDockerPath} {rightCommandLine}", RunOptions.IgnoreRemotePath);

            Console.Write(response.AllText);
            Program.Exit(response.ExitCode);
        }

        /// <summary>
        /// Executes a <b>docker deploy</b> or <b>docker stack deploy</b> command.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="rightCommandLine">The right split of the command line.</param>
        private void Deploy(SshProxy<NodeDefinition> node, CommandLine rightCommandLine)
        {
            string path = null;

            // We're going to upload the file specified by the first
            // [--bundle-file], [--compose-file], or [-c] option.

            for (int i = 0; i < rightCommandLine.Items.Length; i++)
            {
                switch (rightCommandLine.Items[i])
                {
                    case "--bundle-file":
                    case "--compose-file":
                    case "-c":

                        path = rightCommandLine.Items.Skip(i + 1).FirstOrDefault();
                        break;
                }

                if (path != null)
                {
                    // Convert the command line argument to a bundle relative path.

                    rightCommandLine.Items[i + 1] = Path.GetFileName(rightCommandLine.Items[i + 1]);
                    break;
                }
            }

            if (path == null)
            {
                // If that didn't work, try looking for arguments like:
                //
                //      --bundle-file=PATH

                var patterns =
                    new string[]
                    {
                        "--bundle-file=",
                        "--compose-file=",
                        "-c="
                    };

                for (int i = 0; i < rightCommandLine.Items.Length; i++)
                {
                    var item = rightCommandLine.Items[i];

                    foreach (var pattern in patterns)
                    {
                        if (item.StartsWith(pattern))
                        {
                            path = item.Substring(pattern.Length);

                            // Convert the command line argument to a bundle relative path.

                            rightCommandLine.Items[i] = pattern + Path.GetFileName(path);
                            break;
                        }
                    }

                    if (path != null)
                    {
                        break;
                    }
                }
            }

            if (path == null)
            {
                Console.Error.WriteLine("*** ERROR: No DAB or compose file specified.");
                Program.Exit(0);
            }

            var bundle = new CommandBundle("docker", rightCommandLine.Items);

            bundle.AddFile(Path.GetFileName(path), NeonHelper.ReadAllTextReadOnly(path));

            var response = node.SudoCommand(bundle);

            Console.Write(response.AllText);
            Program.Exit(response.ExitCode);
        }

        /// <summary>
        /// Executes a <b>docker secret create</b> command.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="rightCommandLine">The right split of the command line.</param>
        private void SecretCreate(SshProxy<NodeDefinition> node, CommandLine rightCommandLine)
        {
            // We're expecting a command like: 
            //
            //      docker secret create [OPTIONS] SECRET file|-
            //
            // where SECRET is the name of the secret and and [file]
            // is the path to the secret file or [-] indicates that
            // the secret is streaming in on stdin.
            //
            // We're going to run this as a command bundle that includes
            // the secret file.

            if (rightCommandLine.Arguments.Length != 4)
            {
                Console.Error.WriteLine("*** ERROR: Expected: docker secret create [OPTIONS] SECRET file|-");
                Program.Exit(0);
            }

            string  fileArg = rightCommandLine.Arguments[3];
            byte[]  secretData;

            if (fileArg == "-")
            {
                secretData = NeonHelper.ReadStandardInputBytes();
            }
            else
            {
                secretData = File.ReadAllBytes(fileArg);
            }

            // Create and execute a command bundle.  Note that we're going to hardcode
            // the secret data path to [secret.data].

            rightCommandLine.Items[rightCommandLine.Items.Length - 1] = "secret.data";

            var bundle = new CommandBundle("docker", rightCommandLine.Items);

            bundle.AddFile("secret.data", secretData);

            var response = node.SudoCommand(bundle, RunOptions.Redact);

            Console.Write(response.AllText);
            Program.Exit(response.ExitCode);
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            Program.LogPath = null;

            // We're going to upload files for a handful of Docker commands unless this is disabled.

            var split            = shim.CommandLine.Split("docker");
            var leftCommandLine  = split.Left;
            var rightCommandLine = split.Right;

            if (leftCommandLine.HasOption("--no-upload"))
            {
                return new ShimInfo(isShimmed: true);
            }

            var arg1 = rightCommandLine.Arguments.FirstOrDefault();
            var arg2 = rightCommandLine.Arguments.AtIndexOrDefault(1);

            switch (arg1)
            {
                case "deploy":

                    ShimDeploy(shim, rightCommandLine);
                    break;

                case "stack":

                    if (arg2 == "deploy")
                    {
                        ShimDeploy(shim, rightCommandLine);
                    }
                    break;

                case "secret":

                    if (arg2 == "create")
                    {
                        var path = rightCommandLine.Arguments.Skip(3).FirstOrDefault();

                        if (path == null)
                        {
                            return new ShimInfo(isShimmed: true, ensureConnection: true);   // This is an error but we'll let Docker report it.
                        }

                        if (path == "-")
                        {
                            shim.AddStdin();
                        }
                        else
                        {
                            shim.AddFile(path);
                        }
                    }
                    break;
            }

            return new ShimInfo(isShimmed: true, ensureConnection: true);
        }

        /// <summary>
        /// Handles the shim for the <b>docker deploy</b> and <b>docker stack deploy</b> commands.
        /// </summary>
        /// <param name="shim">The shim.</param>
        /// <param name="rightCommandLine">The right split of the command line.</param>
        private void ShimDeploy(DockerShim shim, CommandLine rightCommandLine)
        {
            string path;

            // We're going to shim the file specified by the first
            // [--bundle-file], [--compose-file], or [-c] option.

            for (int i = 0; i < rightCommandLine.Items.Length; i++)
            {
                switch (rightCommandLine.Items[i])
                {
                    case "--bundle-file":
                    case "--compose-file":
                    case "-c":

                        path = rightCommandLine.Items.Skip(i + 1).FirstOrDefault();

                        if (path != null)
                        {
                            shim.AddFile(path);
                            return;
                        }
                        break;
                }
            }

            // If that didn't work, try looking for arguments like:
            //
            //      --bundle-file=PATH

            var patterns = 
                new string[]
                {
                    "--bundle-file=",
                    "--compose-file=",
                    "-c="
                };

            foreach (var item in rightCommandLine.Items)
            {
                foreach (var pattern in patterns)
                {
                    if (item.StartsWith(pattern))
                    {
                        path = item.Substring(pattern.Length);

                        var shimFile = shim.AddFile(path, dontReplace: true);

                        shim.ReplaceItem(pattern + path, pattern + shimFile);
                        return;
                    }
                }
            }
        }
    }
}
