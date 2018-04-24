﻿//-----------------------------------------------------------------------------
// FILE:	    ClusterFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using YamlDotNet.RepresentationModel;

using Neon.Cluster;
using Neon.Common;

namespace Xunit
{
    /// <summary>
    /// An Xunit test fixture used to run unit tests on a neonCLUSTER.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> The Neon <see cref="TestFixture"/> implementation <b>DOES NOT</b>
    /// support parallel test execution because fixtures may impact global machine state
    /// like starting a Couchbase Docker container, modifying the local DNS <b>hosts</b>
    /// file or managing a Docker Swarm or neonCLUSTER.
    /// </para>
    /// <para>
    /// You should explicitly disable parallel execution in all test assemblies that
    /// rely on test fixtures by adding a C# file called <c>AssemblyInfo.cs</c> with:
    /// </para>
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true)]
    /// </code>
    /// </note>
    /// <para>
    /// This Xunit test fixture can be used to run unit tests running on a fullly 
    /// provisioned neonCLUSTER.  This is useful for performing integration tests
    /// within a fully functional environment.  This fixture is similar to <see cref="DockerFixture"/>
    /// and in fact, inherits some functionality from that, but <see cref="DockerFixture"/>
    /// hosts tests against a local single node Docker Swarm rather than a full
    /// neonCLUSTER.
    /// </para>
    /// <para>
    /// neonCLUSTERs do not allow the <see cref="ClusterFixture"/> to perform unit
    /// tests by default, as a safety measure.  You can enable this before cluster
    /// deployment by setting <see cref="ClusterDefinition.AllowUnitTesting"/><c>=true</c>
    /// or by manually invoking this command for an existing cluster:
    /// </para>
    /// <code>
    /// neon cluster set allow-unit-testing=yes
    /// </code>
    /// <para>
    /// This fixture is pretty easy to use.  Simply have your test class inherit
    /// from <see cref="IClassFixture{ClusterFixture}"/> and add a public constructor
    /// that accepts a <see cref="ClusterFixture"/> as the only argument.  Then
    /// you can call it's <see cref="ClusterFixture.Initialize(string, Action)"/> method within
    /// the constructor passing the cluster login name as well as an <see cref="Action"/>.
    /// You may also use the fixture to initialize cluster services, networks, secrets,
    /// routes, etc. within your custom action.
    /// </para>
    /// <note>
    /// Do not call the base <see cref="TestFixture.Initialize(Action)"/> method
    /// is not supported by this fixture and will throw an exception.
    /// </note>
    /// <para>
    /// The specified cluster login file must be already present on the current
    /// machine for the current user.  The <see cref="Login(string)"/> method will
    /// logout from the current cluster (if any) and then login to the one specified.
    /// </para>
    /// <note>
    /// You can also specify a <c>null</c> or empty login name.  In this case,
    /// the fixture will attempt to retrieve the login name from the <b>NEON_TEST_CLUSTER</b>
    /// environment variable.  This is very handy because it allows developers to
    /// specify different target test clusters without having to bake this into the 
    /// unit tests themselves.
    /// </note>
    /// <para>
    /// This fixture provides several methods for managing the cluster state.
    /// These may be called within the test class constructor's action method,
    /// within the test constructor but outside of the action, or within
    /// the test methods:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>Misc</b></term>
    ///     <description>
    ///     <see cref="Reset"/><br/>
    ///     <see cref="DockerExecute(string)"/><br/>
    ///     <see cref="DockerExecute(object[])"/>
    ///     <see cref="NeonExecute(string)"/><br/>
    ///     <see cref="NeonExecute(object[])"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Services</b></term>
    ///     <description>
    ///     <see cref="DockerFixture.CreateService(string, string, string[], string[], string[])"/><br/>
    ///     <see cref="DockerFixture.ListServices()"/><br/>
    ///     <see cref="DockerFixture.RemoveService(string)"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Containers</b></term>
    ///     <description>
    ///     <para>
    ///     <b>Container functionality is not currently implemented by the fixture.</b>
    ///     </para>
    ///     <para>
    ///     <see cref="CreateContainer(string, string, string[], string[], string[])"/><br/>
    ///     <see cref="ListContainers()"/><br/>
    ///     <see cref="RemoveContainer(string)"/>
    ///     </para>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Stacks</b></term>
    ///     <description>
    ///     <see cref="DockerFixture.DeployStack(string, string, string[], TimeSpan, TimeSpan)"/><br/>
    ///     <see cref="DockerFixture.ListStacks()"/><br/>
    ///     <see cref="DockerFixture.RemoveStack(string)"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Secrets</b></term>
    ///     <description>
    ///     <see cref="DockerFixture.CreateSecret(string, byte[], string[])"/><br/>
    ///     <see cref="DockerFixture.CreateSecret(string, string, string[])"/><br/>
    ///     <see cref="DockerFixture.ListSecrets()"/><br/>
    ///     <see cref="DockerFixture.RemoveSecret(string)"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Configs</b></term>
    ///     <description>
    ///     <see cref="DockerFixture.CreateConfig(string, byte[], string[])"/><br/>
    ///     <see cref="DockerFixture.CreateConfig(string, string, string[])"/><br/>
    ///     <see cref="DockerFixture.ListConfigs()"/><br/>
    ///     <see cref="DockerFixture.RemoveConfig(string)"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Networks</b></term>
    ///     <description>
    ///     <see cref="DockerFixture.CreateNetwork(string, string[])"/><br/>
    ///     <see cref="DockerFixture.ListNetworks()"/><br/>
    ///     <see cref="DockerFixture.RemoveNetwork(string)"/>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>Proxy Routes</b></term>
    ///     <description>
    ///     <see cref="PutProxyRoute(string, ProxyRoute)"/><br/>
    ///     <see cref="ListProxyRoutes(string)"/><br/>
    ///     <see cref="RemoveProxyRoute(string, string)"/><br/>
    ///     <see cref="RestartProxies()"/><br/>
    ///     <see cref="RestartPublicProxies()"/><br/>
    ///     <see cref="RestartPrivateProxies()"/>
    ///     </description>
    /// </item>
    /// </list>
    /// <note>
    /// <see cref="ClusterFixture"/> derives from <see cref="TestFixtureSet"/> so you can
    /// use <see cref="TestFixtureSet.AddFixture(string, ITestFixture, Action)"/> to add
    /// additional fixtures within your custom initialization action for advanced scenarios.
    /// </note>
    /// <para>
    /// There are two basic patterns for using this fixture.
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>initialize once</b></term>
    ///     <description>
    ///     <para>
    ///     The basic idea here is to have your test class initialize the cluster
    ///     once within the test class constructor inside of the initialize action
    ///     with common state and services that all of the tests can access.
    ///     </para>
    ///     <para>
    ///     This will be quite a bit faster than reconfiguring the cluster at the
    ///     beginning of every test and can work well for many situations.
    ///     </para>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>initialize every test</b></term>
    ///     <description>
    ///     For scenarios where the cluster must be cleared before every test,
    ///     you can use the <see cref="Reset()"/> method to reset its
    ///     state within each test method, populate the cluster as necessary,
    ///     and then perform your tests.
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public class ClusterFixture : DockerFixture
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Used to track how many fixture instances for the current test run
        /// remain so we can determine when to reset the cluster.
        /// </summary>
        private static int RefCount = 0;

        //---------------------------------------------------------------------
        // Instance members

        private ClusterProxy    cluster;
        private bool            resetOnInitialize;
        private bool            disableChecks;

        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public ClusterFixture()
            : base(reset: false)
        {
            if (RefCount++ == 0)
            {
                // We need to wait until after we've connected to the
                // cluster before calling [Reset()].

                resetOnInitialize = true;
            }
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~ClusterFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected override void Dispose(bool disposing)
        {
            if (!base.IsDisposed)
            {
                if (--RefCount <= 0)
                {
                    Reset();
                }

                Covenant.Assert(RefCount >= 0, "Reference count underflow.");
            }
        }

        /// <summary>
        /// <b>DO NOT USE:</b> This method is not supported by <see cref="ClusterFixture"/>.
        /// Use <see cref="Initialize(string, Action)"/> instead.
        /// </summary>
        /// <param name="action">The optional custom initialization action.</param>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        public new void Initialize(Action action = null)
        {
            throw new NotSupportedException($"[{nameof(ClusterFixture)}.Initialize(Action)] is not supported.  Use {nameof(ClusterFixture)}.Initialize(string, Action)] instead.");
        }

        /// <summary>
        /// Initializes the fixture if it hasn't already been intialized
        /// by connecting the specified including invoking the optional
        /// <see cref="Action"/>.
        /// </summary>
        /// <param name="login">
        /// Specifies a cluster login like <b>USER@CLUSTER</b> or you can pass
        /// <c>null</c> to connect to the cluster specified by the <b>NEON_TEST_CLUSTER</b>
        /// environment variable.
        /// </param>
        /// <param name="action">The optional initialization action.</param>
        /// <exception cref="InvalidOperationException">Thrown if this is called from within the <see cref="Action"/>.</exception>
        public void Initialize(string login, Action action = null)
        {
            CheckDisposed();

            if (InAction)
            {
                throw new InvalidOperationException($"[{nameof(Initialize)}()] cannot be called recursively from within the fixture initialization action.");
            }

            if (IsInitialized)
            {
                return;
            }

            // We need to connect the cluster before calling the base initialization
            // method.  We're going to use [neon-cli] to log out of the current
            // cluster and log into the new one.

            if (string.IsNullOrEmpty(login))
            {
                login = Environment.GetEnvironmentVariable("NEON_TEST_CLUSTER");
            }

            if (string.IsNullOrEmpty(login))
            {
                throw new ArgumentException($"[{nameof(login)}] or the NEON_TEST_CLUSTER environment variable must specify the target cluster login.");
            }

            var loginInfo = NeonClusterHelper.SplitLogin(login);

            if (!loginInfo.IsOK)
            {
                throw new ArgumentException($"Invalid username/cluster [{login}].  Expected something like: USER@CLUSTER");
            }

            var loginPath = NeonClusterHelper.GetLoginPath(loginInfo.Username, loginInfo.ClusterName);

            if (!File.Exists(loginPath))
            {
                throw new ArgumentException($"Cluster login [{login}] does not exist on the current machine and user account.");
            }

            // Use [neon-cli] to login the local machine and user account to the cluster.
            // We're going temporarily set [disableChecks=true] so [NeonExecute()] won't
            // barf because we're not connected to the cluster yet.

            try
            {
                disableChecks = true;

                var result = NeonExecute("login", login);

                if (result.ExitCode != 0)
                {
                    throw new NeonClusterException($"[neon login {login}] command failed: {result.ErrorText}");
                }
            }
            finally
            {
                disableChecks = false;
            }

            // Open a proxy to the cluster.

            cluster = NeonClusterHelper.OpenRemoteCluster(loginPath: loginPath);

            // We needed to defer the [Reset()] call until after the cluster
            // was connected.

            if (resetOnInitialize)
            {
                Reset();
            }

            // Initialize the inherited classes.

            base.Initialize(action);
        }

        /// <summary>
        /// Connects the fixture to a cluster.
        /// </summary>
        /// <param name="login">
        /// The cluster login, like: <b>USER@CLUSTER</b> or <c>null</c> to login
        /// to the cluster named by the <b>NEON_TEST_CLUSTER</b> environment 
        /// variable.
        /// </param>
        /// <remarks>
        /// <note>
        /// <para>
        /// neonCLUSTERs do not allow the <see cref="ClusterFixture"/> to perform unit
        /// tests by default, as a safety measure.  You can enable this before cluster
        /// deployment by setting <see cref="ClusterDefinition.AllowUnitTesting"/><c>=true</c>
        /// or by manually invoking this command for an existing cluster:
        /// </para>
        /// <code>
        /// neon cluster set allow-unit-testing=yes
        /// </code>
        /// </note>
        /// <para>
        /// The specified <paramref name="login"/> must be already present on the current
        /// machine for the current user.  This method will logout from the current cluster
        /// (if any) and then login to the specified cluster.
        /// </para>
        /// </remarks>
        public void Login(string login = null)
        {
            if (cluster != null)
            {
                throw new InvalidOperationException($"[{nameof(Login)}()] has already been called for this [{nameof(ClusterFixture)}].");
            }

            if (string.IsNullOrEmpty(login))
            {
                login = Environment.GetEnvironmentVariable("NEON_TEST_CLUSTER");

                if (string.IsNullOrEmpty(login))
                {
                    throw new ArgumentException($"You must pass a valid cluster login name to [{nameof(ClusterFixture)}.{nameof(login)}()] or specify this as the NEON_TEST_CLUSTER environment variable.");
                }
            }

            throw new NotImplementedException("$todo(jeff.lill)");
        }

        /// <summary>
        /// Ensures that cluster is connected.
        /// </summary>
        private void CheckCluster()
        {
            if (disableChecks)
            {
                return;
            }

            if (cluster == null)
            {
                throw new InvalidOperationException("Cluster is not connected.");
            }

            var currentClusterLogin = CurrentClusterLogin.Load();

            if (currentClusterLogin == null)
            {
                throw new InvalidOperationException("Somebody logged out from under the test cluster while tests were running.");
            }

            var loginInfo = NeonClusterHelper.SplitLogin(currentClusterLogin.Login);

            if (!loginInfo.ClusterName.Equals(cluster.ClusterLogin.ClusterName, StringComparison.InvariantCultureIgnoreCase) ||
                !loginInfo.Username.Equals(cluster.ClusterLogin.Username, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new InvalidOperationException($"Somebody logged into [{currentClusterLogin.Login}] while tests were running.");
            }
        }

        /// <summary>
        /// Executes an arbitrary <b>docker</b> CLI command on a cluster manager, 
        /// passing unformatted arguments and returns the results.
        /// </summary>
        /// <param name="args">The <b>docker</b> command arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method formats any arguments passed so they will be suitable 
        /// for passing on the command line by quoting and escaping them
        /// as necessary.
        /// </para>
        /// </remarks>
        public override ExecuteResult DockerExecute(params object[] args)
        {
            lock (base.SyncRoot)
            {
                base.CheckDisposed();
                this.CheckCluster();

                var neonArgs = new List<object>();

                neonArgs.Add("docker");
                neonArgs.Add("--");

                foreach (var item in args)
                {
                    neonArgs.Add(item);
                }

                return NeonHelper.ExecuteCaptureStreams("neon", neonArgs.ToArray());
            }
        }

        /// <summary>
        /// Executes an arbitrary <b>docker</b> CLI command on a cluster manager, 
        /// passing  a pre-formatted argument string and returns the results.
        /// </summary>
        /// <param name="argString">The <b>docker</b> command arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method assumes that the single string argument passed is already
        /// formatted as required to pass on the command line.
        /// </para>
        /// </remarks>
        public override ExecuteResult DockerExecute(string argString)
        {
            lock (base.SyncRoot)
            {
                base.CheckDisposed();
                this.CheckCluster();

                var neonArgs = "docker -- " + argString;

                return NeonHelper.ExecuteCaptureStreams("docker", neonArgs);
            }
        }

        /// <summary>
        /// Executes an arbitrary <b>neon</b> CLI command passing unformatted
        /// arguments and returns the results.
        /// </summary>
        /// <param name="args">The <b>neon</b> command arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method formats any arguments passed so they will be suitable 
        /// for passing on the command line by quoting and escaping them
        /// as necessary.
        /// </para>
        /// </remarks>
        public virtual ExecuteResult NeonExecute(params object[] args)
        {
            lock (base.SyncRoot)
            {
                base.CheckDisposed();
                this.CheckCluster();

                return NeonHelper.ExecuteCaptureStreams("neon", args);
            }
        }

        /// <summary>
        /// Executes an arbitrary <b>neon</b> CLI command passing a pre-formatted 
        /// argument string and returns the results.
        /// </summary>
        /// <param name="argString">The <b>neon</b> command arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method assumes that the single string argument passed is already
        /// formatted as required to pass on the command line.
        /// </para>
        /// </remarks>
        public virtual ExecuteResult NeonExecute(string argString)
        {
            lock (base.SyncRoot)
            {
                base.CheckDisposed();
                this.CheckCluster();

                return NeonHelper.ExecuteCaptureStreams("neon", argString);
            }
        }

        /// <summary>
        /// Resets the local Docker daemon by clearing all swarm services and state
        /// as well as removing all containers.
        /// </summary>
        /// <remarks>
        /// <note>
        /// As a safety measure, this method ensures that the local Docker instance
        /// <b>IS NOT</b> a member of a multi-node swarm to avoid wiping out production
        /// clusters by accident.
        /// </note>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the fixture has been disposed. </exception>
        public override void Reset()
        {
            base.CheckDisposed();
            this.CheckCluster();

            // $todo(jeff.lill):
            //
            // I'm not going to worry about removing any containers just yet.
            // Presumably, we'll want to leave any [neon-*] related containers
            // running by default and remove all other non-task (service or stack)
            // containers on all nodes.  One thing to think about is whether
            // this should apply to pet nodes as well.

            // Reset the basic Swarm state.

            var serviceNames = new List<string>();

            foreach (var service in ListServices())
            {
                serviceNames.Add(service.Name);
            }

            if (serviceNames.Count > 0)
            {
                DockerExecute("service", "rm", serviceNames.ToArray());
            }

            var stackNames = new List<string>();

            foreach (var stack in ListStacks())
            {
                stackNames.Add(stack.Name);
            }

            if (stackNames.Count > 0)
            {
                DockerExecute("stack", "rm", serviceNames.ToArray());
            }

            // $todo(jeff.lill):
            //
            // The items below can probably be deleted in parallel
            // as a performance improvement.

            var secretNames = new List<string>();

            foreach (var secret in ListSecrets())
            {
                secretNames.Add(secret.Name);
            }

            if (secretNames.Count > 0)
            {
                DockerExecute("secret", "rm", secretNames.ToArray());
            }

            var configNames = new List<string>();

            foreach (var config in ListConfigs())
            {
                configNames.Add(config.Name);
            }

            if (configNames.Count > 0)
            {
                DockerExecute("config", "rm", configNames.ToArray());
            }

            var networkNames = new List<string>();

            foreach (var network in ListNetworks())
            {
                networkNames.Add(network.Name);
            }

            if (networkNames.Count > 0)
            {
                DockerExecute("network", "rm", networkNames.ToArray());
            }

            // Remove any user proxy routes.  We're going to assume that routes
            // with names that start with "neon-" are built-in neonCLUSTER routes
            // and we'll leave these alone.

            var deletedRoutes = false;

            foreach (var route in ListProxyRoutes("public"))
            {
                if (!route.Name.StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (cluster.PublicProxy.RemoveRoute(route.Name))
                    {
                        deletedRoutes = true;
                    }
                }
            }

            foreach (var route in ListProxyRoutes("private"))
            {
                if (!route.Name.StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (cluster.PrivateProxy.RemoveRoute(route.Name))
                    {
                        deletedRoutes = true;
                    }
                }
            }
        
            if (deletedRoutes)
            {
                RestartProxies();
            }
        }

        /// <summary>
        /// <b>DO NOTE USE:</b> This inherited method from <see cref="DockerFixture"/> doesn't
        /// make sense for a multi-node cluster.
        /// </summary>
        /// <param name="name">The container name.</param>
        /// <param name="image">Specifies the container image.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker service create ...</b> command.</param>
        /// <param name="containerArgs">Optional arguments to be passed to the service.</param>
        /// <param name="env">Optional environment variables to be passed to the container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <exception cref="InvalidOperationException">Thrown always.</exception>
        public new void CreateContainer(string name, string image, string[] dockerArgs = null, string[] containerArgs = null, string[] env = null)
        {
            base.CheckDisposed();
            this.CheckCluster();

            throw new InvalidOperationException($"[{nameof(ClusterFixture)}] does not support this method.");
        }

        /// <summary>
        /// <b>DO NOTE USE:</b> This inherited method from <see cref="DockerFixture"/> doesn't
        /// make sense for a multi-node cluster.
        /// </summary>
        /// <returns>A list of <see cref="DockerFixture.ContainerInfo"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown always.</exception>
        public new List<ContainerInfo> ListContainers()
        {
            base.CheckDisposed();
            this.CheckCluster();

            throw new InvalidOperationException($"[{nameof(ClusterFixture)}] does not support this method.");
        }

        /// <summary>
        /// <b>DO NOTE USE:</b> This inherited method from <see cref="DockerFixture"/> doesn't
        /// make sense for a multi-node cluster.
        /// </summary>
        /// <param name="name">The container name.</param>
        /// <exception cref="InvalidOperationException">Thrown always.</exception>
        public new void RemoveContainer(string name)
        {
            base.CheckDisposed();
            this.CheckCluster();

            throw new InvalidOperationException($"[{nameof(ClusterFixture)}] does not support this method.");
        }

        /// <summary>
        /// Saves a proxy route to the cluster.
        /// </summary>
        /// <param name="proxy">The proxy name (<b>public</b> or <b>private</b>).</param>
        /// <param name="route">The route.</param>
        public void PutProxyRoute(string proxy, ProxyRoute route)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(proxy));
            Covenant.Requires<ArgumentNullException>(route != null);

            base.CheckDisposed();
            this.CheckCluster();

            var proxyManager = cluster.GetProxyManager(proxy);

            proxyManager.PutRoute(route);
        }

        /// <summary>
        /// Lists the cluster proxy routes.
        /// </summary>
        /// <param name="proxy">The proxy name (<b>public</b> or <b>private</b>).</param>
        /// <returns>The routes for the named proxy.</returns>
        public List<ProxyRoute> ListProxyRoutes(string proxy)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(proxy));

            base.CheckDisposed();
            this.CheckCluster();

            var proxyManager = cluster.GetProxyManager(proxy);

            return proxyManager.ListRoutes().ToList();
        }

        /// <summary>
        /// Removes a proxy route.
        /// </summary>
        /// <param name="proxy">The proxy name (<b>public</b> or <b>private</b>).</param>
        /// <param name="name">The route name.</param>
        public void RemoveProxyRoute(string proxy, string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(proxy));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            base.CheckDisposed();
            this.CheckCluster();

            var proxyManager = cluster.GetProxyManager(proxy);

            proxyManager.RemoveRoute(name);
        }

        /// <summary>
        /// Restarts cluster proxies to ensure that they's picked up any
        /// proxy definition changes.
        /// </summary>
        public void RestartProxies()
        {
            // We'll restart these in parallel for better performance.

            var tasks = NeonHelper.WaitAllAsync(
                Task.Run(() => RestartPublicProxies()),
                Task.Run(() => RestartPrivateProxies()));

            tasks.Wait();
        }

        /// <summary>
        /// Restarts the <b>public</b> p[roxies to ensure that they's picked up any
        /// proxy definition changes.
        /// </summary>
        public void RestartPublicProxies()
        {
            // $todo(jeff.lill):
            //
            // We probably need to restart the proxy bridge containers on all
            // of the pets as well.

            DockerExecute("service", "update", "--force", "--update-parallelism", "0", "neon-proxy-public");
        }

        /// <summary>
        /// Restarts the <b>private</b> p[roxies to ensure that they's picked up any
        /// proxy definition changes.
        /// </summary>
        public void RestartPrivateProxies()
        {
            // $todo(jeff.lill):
            //
            // We probably need to restart the proxy bridge containers on all
            // of the pets as well.

            DockerExecute("service", "update", "--force", "--update-parallelism", "0", "neon-proxy-private");
        }
    }
}
