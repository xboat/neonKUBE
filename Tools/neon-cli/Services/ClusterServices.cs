﻿//-----------------------------------------------------------------------------
// FILE:	    ClusterServices.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Cluster;
using Neon.Common;
using Neon.IO;
using Neon.Net;
using Neon.Time;

namespace NeonCli
{
    /// <summary>
    /// Handles the provisioning of the global cluster proxy services including: 
    /// <b>neon-cluster-manager</b>, <b>neon-proxy-manager</b>,
    /// <b>neon-proxy-public</b> and <b>neon-proxy-private</b>, and the
    /// <b>neon-proxy-public-bridge</b> and <b>neon-proxy-private-bridge</b>
    /// containers on any pet nodes.
    /// </summary>
    public class ClusterServices
    {
        private ClusterProxy cluster;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cluster">The cluster proxy.</param>
        public ClusterServices(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            this.cluster = cluster;
        }

        /// <summary>
        /// Configures the cluster proxy related services.
        /// </summary>
        /// <param name="firstManager">The first cluster proxy manager.</param>
        public void Configure(NodeProxy<NodeDefinition> firstManager)
        {
            firstManager.InvokeIdempotentAction("setup-cluster-services",
                () =>
                {
                    // Ensure that Vault has been initialized.

                    if (cluster.ClusterLogin.VaultCredentials == null)
                    {
                        throw new InvalidOperationException("Vault has not been initialized yet.");
                    }

                    //---------------------------------------------------------
                    // Deploy [neon-cluster-manager] as a service on each manager node.

                    string unsealSecretOption = null;

                    if (cluster.Definition.Vault.AutoUnseal)
                    {
                        var vaultCredentials = NeonHelper.JsonClone<VaultCredentials>(cluster.ClusterLogin.VaultCredentials);

                        // We really don't want to include the root token in the credentials
                        // passed to [neon-cluster-manager], which needs the unseal keys.

                        vaultCredentials.RootToken = null;

                        cluster.DockerSecret.Set("neon-cluster-manager-vaultkeys", Encoding.UTF8.GetBytes(NeonHelper.JsonSerialize(vaultCredentials, Formatting.Indented)));

                        unsealSecretOption = "--secret=neon-cluster-manager-vaultkeys";
                    }

                    cluster.FirstManager.Status = "start: neon-cluster-manager";

                    var response = cluster.FirstManager.DockerCommand(RunOptions.Redact,
                        "docker service create",
                            "--name", "neon-cluster-manager",
                            "--mount", "type=bind,src=/etc/neoncluster/env-host,dst=/etc/neoncluster/env-host,readonly=true",
                            "--mount", "type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true",
                            "--mount", "type=bind,src=/var/run/docker.sock,dst=/var/run/docker.sock",
                            "--env", "LOG_LEVEL=INFO",
                            unsealSecretOption,
                            "--constraint", "node.role==manager",
                            "--replicas", 1,
                            "--restart-delay", cluster.Definition.Docker.RestartDelay,
                            Program.ResolveDockerImage(cluster.Definition.ClusterManagerImage));

                    foreach (var manager in cluster.Managers)
                    {
                        manager.UploadText(LinuxPath.Combine(NodeHostFolders.Scripts, "neon-cluster-manager.sh"), response.BashCommand);
                    }

                    //---------------------------------------------------------
                    // Deploy proxy related services

                    // Obtain the AppRole credentials from Vault for the proxy manager as well as the
                    // public and private proxy services and persist these as Docker secrets.

                    firstManager.Status = "secrets: proxy services";

                    cluster.DockerSecret.Set("neon-proxy-manager-credentials", NeonHelper.JsonSerialize(cluster.Vault.GetAppRoleCredentialsAsync("neon-proxy-manager").Result, Formatting.Indented));
                    cluster.DockerSecret.Set("neon-proxy-public-credentials", NeonHelper.JsonSerialize(cluster.Vault.GetAppRoleCredentialsAsync("neon-proxy-public").Result, Formatting.Indented));
                    cluster.DockerSecret.Set("neon-proxy-private-credentials", NeonHelper.JsonSerialize(cluster.Vault.GetAppRoleCredentialsAsync("neon-proxy-private").Result, Formatting.Indented));

                    // Initialize the public and private proxies.

                    cluster.PublicProxy.UpdateSettings(
                        new ProxySettings()
                        {
                            FirstPort = NeonHostPorts.ProxyPublicFirst,
                            LastPort = NeonHostPorts.ProxyPublicLast
                        });

                    cluster.PrivateProxy.UpdateSettings(
                        new ProxySettings()
                        {
                            FirstPort = NeonHostPorts.ProxyPrivateFirst,
                            LastPort = NeonHostPorts.ProxyPrivateLast
                        });

                    // Deploy the proxy manager service.

                    firstManager.Status = "start: neon-proxy-manager";

                    response = firstManager.DockerCommand(
                        "docker service create",
                            "--name", "neon-proxy-manager",
                            "--mount", "type=bind,src=/etc/neoncluster/env-host,dst=/etc/neoncluster/env-host,readonly=true",
                            "--mount", "type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true",
                            "--mount", "type=bind,src=/var/run/docker.sock,dst=/var/run/docker.sock",
                            "--env", "VAULT_CREDENTIALS=neon-proxy-manager-credentials",
                            "--env", "LOG_LEVEL=INFO",
                            "--secret", "neon-proxy-manager-credentials",
                            "--constraint", "node.role==manager",
                            "--replicas", 1,
                            "--restart-delay", cluster.Definition.Docker.RestartDelay,
                            Program.ResolveDockerImage(cluster.Definition.ProxyManagerImage));

                    foreach (var manager in cluster.Managers)
                    {
                        manager.UploadText(LinuxPath.Combine(NodeHostFolders.Scripts, "neon-proxy-manager.sh"), response.BashCommand);
                    }

                    // Docker mesh routing seemed unstable on versions 17.03.0-ce
                    // thru 17.06.0-ce so we're going to provide an option to work
                    // around this by running the PUBLIC, PRIVATE and VAULT proxies 
                    // on all nodes and  publishing the ports to the host (not the mesh).
                    //
                    //      https://github.com/jefflill/NeonForge/issues/104
                    //
                    // Note that this mode feature is documented (somewhat poorly) here:
                    //
                    //      https://docs.docker.com/engine/swarm/services/#publish-ports

                    var publicPublish   = new List<string>();
                    var privatePublish  = new List<string>();
                    var proxyConstraint = new List<string>();

                    if (cluster.Definition.Docker.AvoidIngressNetwork)
                    {
                        // The parameterized [service create --publish] option doesn't handle port ranges so we need to 
                        // specify multiple publish options.

                        for (int port = NeonHostPorts.ProxyPublicFirst; port <= NeonHostPorts.ProxyPublicLast; port++)
                        {
                            publicPublish.Add($"--publish");
                            publicPublish.Add($"mode=host,published={port},target={port}");
                        }

                        for (int port = NeonHostPorts.ProxyPrivateFirst; port <= NeonHostPorts.ProxyPrivateLast; port++)
                        {
                            privatePublish.Add($"--publish");
                            privatePublish.Add($"mode=host,published={port},target={port}");
                        }
                    }
                    else
                    {
                        publicPublish.Add($"--publish");
                        publicPublish.Add($"{NeonHostPorts.ProxyPublicFirst}-{NeonHostPorts.ProxyPublicLast}:{NeonHostPorts.ProxyPublicFirst}-{NeonHostPorts.ProxyPublicLast}");

                        privatePublish.Add($"--publish");
                        privatePublish.Add($"{NeonHostPorts.ProxyPrivateFirst}-{NeonHostPorts.ProxyPrivateLast}:{NeonHostPorts.ProxyPrivateFirst}-{NeonHostPorts.ProxyPrivateLast}");

                        proxyConstraint.Add($"--constraint");

                        if (cluster.Definition.Workers.Count() > 0)
                        {
                            // Constrain proxies to worker nodes if there are any.

                            proxyConstraint.Add($"node.role!=manager");
                        }
                        else
                        {
                            // Constrain proxies to manager nodes nodes if there are no workers.

                            proxyConstraint.Add($"node.role==manager");
                        }
                    }

                    firstManager.Status = "start: neon-proxy-public";

                    response = firstManager.DockerCommand(
                        "docker service create",
                            "--name", "neon-proxy-public",
                            "--mount", "type=bind,src=/etc/neoncluster/env-host,dst=/etc/neoncluster/env-host,readonly=true",
                            "--mount", "type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true",
                            "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/public/conf",
                            "--env", "VAULT_CREDENTIALS=neon-proxy-public-credentials",
                            "--env", "LOG_LEVEL=INFO",
                            "--env", "DEBUG=false",
                            "--secret", "neon-proxy-public-credentials",
                            publicPublish,
                            proxyConstraint,
                            "--mode", "global",
                            "--restart-delay", cluster.Definition.Docker.RestartDelay,
                            "--network", NeonClusterConst.PublicNetwork,
                            Program.ResolveDockerImage(cluster.Definition.ProxyImage));

                    foreach (var manager in cluster.Managers)
                    {
                        manager.UploadText(LinuxPath.Combine(NodeHostFolders.Scripts, "neon-proxy-public.sh"), response.BashCommand);
                    }

                    firstManager.Status = "start: neon-proxy-private";

                    response = firstManager.DockerCommand(
                        "docker service create",
                            "--name", "neon-proxy-private",
                            "--mount", "type=bind,src=/etc/neoncluster/env-host,dst=/etc/neoncluster/env-host,readonly=true",
                            "--mount", "type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true",
                            "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/private/conf",
                            "--env", "VAULT_CREDENTIALS=neon-proxy-private-credentials",
                            "--env", "LOG_LEVEL=INFO",
                            "--env", "DEBUG=false",
                            "--secret", "neon-proxy-private-credentials",
                            privatePublish,
                            proxyConstraint,
                            "--mode", "global",
                            "--restart-delay", cluster.Definition.Docker.RestartDelay,
                            "--network", NeonClusterConst.PrivateNetwork,
                            Program.ResolveDockerImage(cluster.Definition.ProxyImage));

                    foreach (var manager in cluster.Managers)
                    {
                        manager.UploadText(LinuxPath.Combine(NodeHostFolders.Scripts, "neon-proxy-private.sh"), response.BashCommand);
                    }

                    firstManager.Status = string.Empty;
                });

            // Start the [neon-proxy-public-bridge] and [neon-proxy-private-bridge] containers
            // on the cluster pets.

            foreach (var pet in cluster.Pets)
            {
                pet.InvokeIdempotentAction("setup-neon-proxy-public-bridge",
                    () =>
                    {
                        pet.Status = "start: neon-proxy-public-bridge";

                        var response = pet.DockerCommand(
                            "docker run",
                                "--detach",
                                "--name", "neon-proxy-public-bridge",
                                "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/public-bridge/conf",
                                "--env", "LOG_LEVEL=INFO",
                                "--network", "host",
                                "--restart", "always",
                                Program.ResolveDockerImage(cluster.Definition.ProxyImage));

                        pet.UploadText(LinuxPath.Combine(NodeHostFolders.Scripts, "neon-proxy-public-bridge.sh"), response.BashCommand);
                        pet.Status = string.Empty;
                    });

                pet.InvokeIdempotentAction("setup-neon-proxy-private-bridge",
                    () =>
                    {
                        pet.Status = "start: neon-proxy-private-bridge";

                        var response = pet.DockerCommand(
                            "docker run",
                                "--detach",
                                "--name", "neon-proxy-private-bridge",
                                "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/private-bridge/conf",
                                "--env", "LOG_LEVEL=INFO",
                                "--network", "host",
                                "--restart", "always",
                                Program.ResolveDockerImage(cluster.Definition.ProxyImage));

                        pet.UploadText(LinuxPath.Combine(NodeHostFolders.Scripts, "neon-proxy-private-bridge.sh"), response.BashCommand);
                        pet.Status = string.Empty;
                    });
            }
        }
    }
}
