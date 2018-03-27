﻿//-----------------------------------------------------------------------------
// FILE:	    ClusterDefinition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes a neonCLUSTER.
    /// </summary>
    public class ClusterDefinition
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Special cluster node name reserved for identifying the healthy Swarm
        /// manager node where Ansible Docker modules should perform Swarm related
        /// activities.
        /// </summary>
        public const string VirtualSwarmManagerName = "swarm-manager";

        private const string        defaultDatacenter          = "DATACENTER";
        private const string        defaultProvisioner         = "unknown";
        private readonly string[]   defaultTimeSources         = new string[] { "pool.ntp.org" };
        private const string        defaultProxyImage          = "neoncluster/neon-proxy:latest";
        private const string        defaultProxyVaultImage     = "neoncluster/neon-proxy-vault:latest";
        private const string        defaultProxyManagerImage   = "neoncluster/neon-proxy-manager:latest";
        private const string        defaultClusterManagerImage = "neoncluster/neon-cluster-manager:latest";
        private const string        defaultDnsImage            = "neoncluster/neon-dns:latest";
        private const string        defaultDnsMonImage         = "neoncluster/neon-dns-mon:latest";
        private const string        defaultDrivePrefix         = "sd";
        private const int           defaultStepStaggerSeconds  = 5;

        /// <summary>
        /// Regex for verifying cluster names for hosts, routes, groups, etc.
        /// </summary>
        public static Regex NameRegex { get; private set; }    = new Regex(@"^[a-z0-9.\-_]+$", RegexOptions.IgnoreCase);

        /// <summary>
        /// Regex for verifying DNS hostnames.
        /// </summary>
        public static Regex DnsHostRegex { get; private set; } = new Regex(@"^([a-z0-9]|[a-z0-9][a-z0-9\-_]{0,61}[a-z0-9])(\.([a-z0-9]|[a-z0-9][a-z0-9\-_]{0,61}[a-z0-9_]))*$", RegexOptions.IgnoreCase);

        /// <summary>
        /// The prefix reserved for neonCLUSTER related Docker daemon, image, and container labels.
        /// </summary>
        public const string ReservedLabelPrefix = "io.neon";

        /// <summary>
        /// Parses a cluster definition from JSON text.
        /// </summary>
        /// <param name="json">The JSON text.</param>
        /// <param name="strict">Optionally require that all input properties map to to <see cref="ClusterDefinition"/> properties.</param>
        /// <returns>The parsed <see cref="ClusterDefinition"/>.</returns>
        /// <remarks>
        /// <note>
        /// The source is first preprocessed using <see cref="PreprocessReader"/>
        /// and then is parsed as JSON.
        /// </note>
        /// </remarks>
        public static ClusterDefinition FromJson(string json, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(json != null);

            using (var stringReader = new StringReader(json))
            {
                using (var preprocessReader = new PreprocessReader(stringReader))
                {
                    return NeonHelper.JsonDeserialize<ClusterDefinition>(preprocessReader.ReadToEnd(), strict: strict);
                }
            }
        }

        /// <summary>
        /// Parses and validates a cluster definition file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="strict">Optionally require that all input properties map to to <see cref="ClusterDefinition"/> properties.</param>
        /// <exception cref="ArgumentException">Thrown if the definition is not valid.</exception>
        public static void ValidateFile(string path, bool strict = false)
        {
            FromFile(path, strict: strict);
        }

        /// <summary>
        /// Parses a cluster definition from a file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="strict">Optionally require that all input properties map to to <see cref="ClusterDefinition"/> properties.</param>
        /// <returns>The parsed <see cref="ClusterDefinition"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if the definition is not valid.</exception>
        /// <remarks>
        /// <note>
        /// The source is first preprocessed using <see cref="PreprocessReader"/>
        /// and then is parsed as JSON.
        /// </note>
        /// </remarks>
        public static ClusterDefinition FromFile(string path, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(path != null);

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var stringReader = new StreamReader(stream))
                {
                    using (var preprocessReader = new PreprocessReader(stringReader))
                    {
                        var clusterDefinition = NeonHelper.JsonDeserialize<ClusterDefinition>(preprocessReader.ReadToEnd(), strict: strict);

                        if (clusterDefinition == null)
                        {
                            throw new ArgumentException($"Invalid cluster definition in [{path}].");
                        }

                        // Populate the [node.Name] properties from the dictionary name.

                        foreach (var item in clusterDefinition.NodeDefinitions)
                        {
                            var node = item.Value;

                            if (string.IsNullOrEmpty(node.Name))
                            {
                                node.Name = item.Key;
                            }
                            else if (item.Key != node.Name)
                            {
                                throw new FormatException($"The node names don't match [\"{item.Key}\" != \"{node.Name}\"].");
                            }
                        }

                        clusterDefinition.Validate();

                        return clusterDefinition;
                    }
                }
            }
        }

        /// <summary>
        /// Verifies that the string passed is a valid 16-byte Base64 encoded encryption
        /// key or <c>null</c> or empty.
        /// </summary>
        /// <param name="key">The key to be tested.</param>
        /// <exception cref="ArgumentException">Thrown if the key is not valid.</exception>
        internal static void VerifyEncryptionKey(string key)
        {
            if (!string.IsNullOrEmpty(key))
            {
                byte[] keyBytes;

                try
                {
                    keyBytes = Convert.FromBase64String(key);
                }
                catch
                {
                    throw new ArgumentException($"Invalid Consul key [{key}].  Malformed Base64 string.");
                }

                if (keyBytes.Length != 16)
                {
                    throw new ArgumentException($"Invalid Consul key [{key}].  Key must contain 16 bytes.");
                }
            }
        }

        /// <summary>
        /// Verifies that a string is a valid cluster name.
        /// </summary>
        /// <param name="name">The name being tested.</param>
        /// <returns><c>true</c> if the name is valid.</returns>
        public static bool IsValidName(string name)
        {
            return name != null && NameRegex.IsMatch(name);
        }

        /// <summary>
        /// Ensures that a VM memory or disk size specification is valid and also
        /// converts the value to the corresponding long count.
        /// </summary>
        /// <param name="sizeValue">The size value string.</param>
        /// <param name="optionsType">Type of the property holding the size property (used for error reporting).</param>
        /// <param name="propertyName">The size property name (used for error reporting).</param>
        /// <returns>The size converted into a <c>long</c>.</returns>
        public static long ValidateSize(string sizeValue, Type optionsType, string propertyName)
        {
            long size;

            if (string.IsNullOrEmpty(sizeValue))
            {
                throw new ClusterDefinitionException($"[{optionsType.Name}.{propertyName}] cannot be NULL or empty.");
            }

            if (sizeValue.EndsWith("MB", StringComparison.InvariantCultureIgnoreCase))
            {
                var count = sizeValue.Substring(0, sizeValue.Length - 2);

                if (!long.TryParse(count, out size) || size <= 0)
                {
                    throw new ClusterDefinitionException($"[{optionsType.Name}.{propertyName}={sizeValue}] is not valid.");
                }

                size *= NeonHelper.Mega;
            }
            else if (sizeValue.EndsWith("GB", StringComparison.InvariantCultureIgnoreCase))
            {
                var count = sizeValue.Substring(0, sizeValue.Length - 2);

                if (!long.TryParse(count, out size) || size <= 0)
                {
                    throw new ClusterDefinitionException($"[{optionsType.Name}.{propertyName}={sizeValue}] is not valid.");
                }

                size *= NeonHelper.Giga;
            }
            else if (!long.TryParse(sizeValue, out size) || size <= 0)
            {
                throw new ClusterDefinitionException($"[{optionsType.Name}.{propertyName}={sizeValue}] is not valid.");
            }

            return size;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The cluster name.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The name may include only letters, numbers, periods, dashes, and underscores.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        public string Name { get; set; }

        /// <summary>
        /// Identifies the tool/version used to provision the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "Provisioner", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultProvisioner)]
        public string Provisioner { get; set; } = defaultProvisioner;

        /// <summary>
        /// <para>
        /// Returns the prefix for block devices that will be attached to
        /// the host machines.  For many hosting environments this will be
        /// <b>sd</b>, indicating that drives will be attached like: 
        /// <b>/dev/sda</b>, <b>/dev/sdb</b>, <b>/dev/sdc</b>...
        /// </para>
        /// <para>
        /// This may be different though for some hosting environment.
        /// XenServer for example, uses the <b>xvd</b> prefix and attaches
        /// drives as <b>/dev/sda</b>, <b>/dev/sdb</b>, <b>/dev/sdc</b>...
        /// </para>
        /// <note>
        /// This property is set automatically during cluster provisioning.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "DrivePrefix", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultDrivePrefix)]
        public string DrivePrefix { get; set; } = defaultDrivePrefix;

        /// <summary>
        /// <para>
        /// Enables cluster debug mode which can be useful for neonCLUSTER development purposes.
        /// This defaults to <c>false</c>.
        /// </para>
        /// <note>
        /// <b>WARNING:</b> This should not be enabled for production clusters because it may 
        /// enable potential security threats.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "DebugMode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool DebugMode { get; set; } = false;

        /// <summary>
        /// Specifies hosting related settings (e.g. the cloud provider).  This defaults to
        /// <c>null</c> which indicates that the cluster will be hosted on private servers.
        /// </summary>
        [JsonProperty(PropertyName = "Hosting", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public HostingOptions Hosting { get; set; } = null;

        /// <summary>
        /// Management VPN options.
        /// </summary>
        [JsonProperty(PropertyName = "Vpn", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public VpnOptions Vpn { get; set; } = null;

        /// <summary>
        /// Identifies the datacenter.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The name may include only letters, numbers, periods, dashes, and underscores.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Datacenter", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultDatacenter)]
        public string Datacenter { get; set; } = defaultDatacenter;

        /// <summary>
        /// Indicates how the cluster is being used.
        /// </summary>
        [JsonProperty(PropertyName = "Environment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(StringEnumConverter))]
        [DefaultValue(EnvironmentType.Other)]
        public EnvironmentType Environment { get; set; } = EnvironmentType.Other;

        /// <summary>
        /// Optionally specifies that a bare Docker cluster without most of the extra <b>neonCLUSTER</b>
        /// features should be created.  This is useful for creating test clusters for reporting and
        /// replicating Docker issues.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "BareDocker", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool BareDocker { get; set; } = false;

        /// <summary>
        /// Specifies the NTP time sources to be configured for the cluster.  These are the
        /// FQDNs or IP addresses of the sources.  This defaults to <b>pool.ntp.org</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The cluster managers will be configured to synchronize their time with the these
        /// time sources and the worker nodes will be configured to synchronize their time
        /// with the manager nodes.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "TimeSources", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string[] TimeSources { get; set; } = null;

        /// <summary>
        /// Optionally specifies one or more APT proxy/cache servers the cluster will use to install
        /// and update Linux packages.  These are HTTP URLs including the port (generally 
        /// <see cref="NetworkPorts.AppCacherNg"/> = 3142) of a  <b>apt-cacher-ng</b> or other proxy
        /// server.  Multiple URLs may be specified by separating them with spaces.  This defaults to
        /// <c>null</c> which will configure the cluster manager nodes as the package proxies.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A package cache will greatly reduce the Internet network traffic required to deploy a
        /// cluster, especially for large clusters.
        /// </para>
        /// <note>
        /// The cluster nodes are configured to failover to different proxies or to hit the 
        /// default Linux distribution package mirror directly if any or all of the caches
        /// specified are unavailable.
        /// </note>
        /// <note>
        /// The package caches will be tried in the order they are listed.  This essentially
        /// makes the first cache primary, with the others as backups.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "PackageProxy", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string PackageProxy { get; set; } = null;

        /// <summary>
        /// Optionally specifies setup process related options.
        /// </summary>
        [JsonProperty(PropertyName = "Setup", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public SetupOptions Setup { get; set; } = null;

        /// <summary>
        /// Specifies host node options.
        /// </summary>
        [JsonProperty(PropertyName = "HostNode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public HostNodeOptions HostNode { get; set; } = new HostNodeOptions();

        /// <summary>
        /// Describes the Docker configuration.
        /// </summary>
        [JsonProperty(PropertyName = "Docker", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public DockerOptions Docker { get; set; } = new DockerOptions();

        /// <summary>
        /// Describes the cluster's network configuration.
        /// </summary>
        [JsonProperty(PropertyName = "Network", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public NetworkOptions Network { get; set; } = new NetworkOptions();

        /// <summary>
        /// Describes the HashiCorp Consul service disovery and key/value store configuration.
        /// </summary>
        [JsonProperty(PropertyName = "Consul", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public ConsulOptions Consul { get; set; } = new ConsulOptions();

        /// <summary>
        /// Specifies the HashiCorp Vault secret server related settings.
        /// </summary>
        [JsonProperty(PropertyName = "Vault", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public VaultOptions Vault { get; set; } = new VaultOptions();

        /// <summary>
        /// Cluster logging related settings.
        /// </summary>
        [JsonProperty(PropertyName = "Log", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public LogOptions Log { get; set; } = new LogOptions();

        /// <summary>
        /// Cluster dashboard settings.
        /// </summary>
        [JsonProperty(PropertyName = "Dashboard", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public DashboardOptions Dashboard { get; set; } = new DashboardOptions();

        /// <summary>
        /// Integrated Ceph storage cluster options.
        /// </summary>
        [JsonProperty(PropertyName = "Ceph", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public CephOptions Ceph { get; set; } = new CephOptions();

        /// <summary>
        /// The Docker image to be used to provision public and private proxies and proxy bridges.
        /// This defaults to <b>neoncluster/neon-proxy:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ProxyImage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultProxyImage)]
        public string ProxyImage { get; set; } = defaultProxyImage;

        /// <summary>
        /// The Docker image to be used to provision HashiCorp Vault proxies.
        /// This defaults to <b>neoncluster/neon-proxy-vault:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ProxyVaultImage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultProxyVaultImage)]
        public string ProxyVaultImage { get; set; } = defaultProxyVaultImage;

        /// <summary>
        /// The Docker image to be used to provision the <b>neon-proxy-manager</b>
        /// service.   This defaults to <b>neoncluster/neon-proxy-manager:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ProxyManagerImage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultProxyManagerImage)]
        public string ProxyManagerImage { get; set; } = defaultProxyManagerImage;

        /// <summary>
        /// The Docker image to be used to provision the <b>neon-cluster-manager</b>
        /// service.   This defaults to <b>neoncluster/neon-cluster-manager:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterManagerImage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultClusterManagerImage)]
        public string ClusterManagerImage { get; set; } = defaultClusterManagerImage;

        /// <summary>
        /// The Docker image to be used to provision the <b>neon-dns</b> service.
        /// This defaults to <b>neoncluster/neon-dns:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "DnsImage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultDnsImage)]
        public string DnsImage { get; set; } = defaultDnsImage;

        /// <summary>
        /// The Docker image to be used to provision the <b>neon-dns-mon</b> service.
        /// This defaults to <b>neoncluster/neon-dns-mon:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "DnsMonImage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultDnsMonImage)]
        public string DnsMonImage { get; set; } = defaultDnsMonImage;

        /// <summary>
        /// Describes the Docker host nodes in the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "Nodes", Required = Required.Always)]
        public Dictionary<string, NodeDefinition> NodeDefinitions { get; set; } = new Dictionary<string, NodeDefinition>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// <para>
        /// Set to the MD5 hash (encoded as base64) of the cluster definition for scenarios
        /// where its necessary to quickly determine whether two definitions are the same.
        /// This is computed by calling <see cref="ComputeHash()"/>
        /// </para>
        /// <note>
        /// The computed hash does not include the hosting provider details because these
        /// typically include hosting related secrets and so they are not persisted to
        /// the cluster Consul service.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Hash", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public string Hash { get; set; }

        /// <summary>
        /// Enumerates all cluster node definitions.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> Nodes
        {
            get { return NodeDefinitions.Values; }
        }

        /// <summary>
        /// Enumerates all cluster node definitions sorted in ascending order by name.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> SortedNodes
        {
            get { return Nodes.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Enumerates the cluster manager node definitions.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> Managers
        {
            get { return Nodes.Where(n => n.IsManager); }
        }

        /// <summary>
        /// Enumerates the cluster manager node definitions sorted in ascending order by name.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> SortedManagers
        {
            get { return Managers.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Enumerates the cluster pet node definitions.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> Pets
        {
            get { return Nodes.Where(n => n.IsPet); }
        }

        /// <summary>
        /// Enumerates the cluster manager pet definitions sorted in ascending order by name.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> SortedPets
        {
            get { return Pets.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Enumerates the cluster worker node definitions.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> Workers
        {
            get { return Nodes.Where(n => n.IsWorker); }
        }

        /// <summary>
        /// Enumerates the cluster worker node definitions sorted in ascending order by name.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> SortedWorkers
        {
            get { return Workers.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Enumerates the cluster swarm node definitions (the managers and workers).
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> Swarm
        {
            get { return Nodes.Where(n => n.InSwarm); }
        }

        /// <summary>
        /// Enumerates the cluster swarm node definitions (the managers and workers)
        /// sorted in ascending order by name.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<NodeDefinition> SortedSwarm
        {
            get { return Swarm.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Validates that node private IP addresses are set, are within the nodes subnet, and
        /// are unique.  This method is intended to be called from hosting options classes
        /// like <see cref="MachineOptions"/> which require specified node IP addresses.
        /// </summary>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void ValidatePrivateNodeAddresses()
        {
            var ipAddressToNode = new Dictionary<IPAddress, NodeDefinition>();

            if (string.IsNullOrEmpty(Network.NodesSubnet))
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(ClusterDefinition.Network)}.{nameof(NetworkOptions.NodesSubnet)}] property is required.");
            }

            if (!NetworkCidr.TryParse(Network.NodesSubnet, out var nodesSubnet))
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(ClusterDefinition.Network)}.{nameof(NetworkOptions.NodesSubnet)}={Network.NodesSubnet}] property is not valid.");
            }

            foreach (var node in SortedNodes.OrderBy(n => n.Name))
            {
                if (string.IsNullOrEmpty(node.PrivateAddress))
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] has not been assigned a private IP address.");
                }

                if (!IPAddress.TryParse(node.PrivateAddress, out var address))
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] has invalid private IP address [{node.PrivateAddress}].");
                }

                if (address == IPAddress.Any)
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] has not been assigned a private IP address.");
                }

                if (ipAddressToNode.TryGetValue(address, out var conflictingNode))
                {
                    throw new ClusterDefinitionException($"Nodes [{conflictingNode.Name}] and [{node.Name}] have the same IP address [{address}].");
                }

                ipAddressToNode.Add(address, node);
            }
        }

        /// <summary>
        /// Validates the cluster definition and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate()
        {
            Provisioner = Provisioner ?? defaultProvisioner;
            DrivePrefix   = DrivePrefix ?? defaultDrivePrefix;
            Setup         = Setup ?? new SetupOptions();
            Hosting       = Hosting ?? new HostingOptions();
            Vpn           = Vpn ?? new VpnOptions();
            HostNode      = HostNode ?? new HostNodeOptions();
            Docker        = Docker ?? new DockerOptions();
            Network       = Network ?? new NetworkOptions();
            Consul        = Consul ?? new ConsulOptions();
            Vault         = Vault ?? new VaultOptions();
            Log           = Log ?? new LogOptions();
            Dashboard     = Dashboard ?? new DashboardOptions();
            Ceph          = Ceph ?? new CephOptions();

            ProxyImage          = ProxyImage ?? defaultProxyImage;
            ProxyVaultImage     = ProxyVaultImage ?? defaultProxyVaultImage;
            ProxyManagerImage   = ProxyManagerImage ?? defaultProxyManagerImage;
            ClusterManagerImage = ClusterManagerImage ?? defaultClusterManagerImage;
            DnsImage            = DnsImage ?? defaultDnsImage;
            DnsMonImage         = DnsMonImage ?? defaultDnsMonImage;

            Setup.Validate(this);
            Network.Validate(this);
            Hosting.Validate(this);
            Vpn.Validate(this);
            HostNode.Validate(this);
            Docker.Validate(this);
            Consul.Validate(this);
            Vault.Validate(this);
            Log.Validate(this);
            Dashboard.Validate(this);
            Ceph.Validate(this);

            HostingManager.ValidateCluster(this);

            if (TimeSources == null || TimeSources.Length == 0 || TimeSources.Count(ts => string.IsNullOrWhiteSpace(ts)) > 0)
            {
                TimeSources = new string[] { "pool.ntp.org" };
            }

            if (NodeDefinitions == null || NodeDefinitions.Count == 0)
            {
                throw new ClusterDefinitionException("At least one cluster node must be defined.");
            }

            foreach (var node in NodeDefinitions.Values)
            {
                node.Validate(this);
            }

            if (Name == null)
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Name)}] property is required.");
            }

            if (!IsValidName(Name))
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Name)}={Name}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            if (Datacenter == null)
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Datacenter)}] property is required.");
            }

            if (!IsValidName(Datacenter))
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Datacenter)}={Datacenter}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            if (!string.IsNullOrEmpty(PackageProxy))
            {
                var packageCacheUris = PackageProxy.Split(',');

                for (int i = 0; i < packageCacheUris.Length; i++)
                {
                    packageCacheUris[i] = packageCacheUris[i].Trim();

                    if (!Uri.TryCreate(packageCacheUris[i], UriKind.Absolute, out var aptProxyUri))
                    {
                        throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(PackageProxy)}={PackageProxy}] includes [{packageCacheUris[i]}] which is not a valid URI.");
                    }

                    if (aptProxyUri.Scheme != "http")
                    {
                        throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(PackageProxy)}={PackageProxy}] includes [{packageCacheUris[i]}] which does not have the [http] scheme.");
                    }
                }
            }

            var managementNodeCount = Managers.Count();

            if (managementNodeCount == 0)
            {
                throw new ClusterDefinitionException("Clusters must have at least one management node.");
            }
            else if (managementNodeCount > 5)
            {
                throw new ClusterDefinitionException("Clusters may not have more than [5] management nodes.");
            }
            else if (!NeonHelper.IsOdd(managementNodeCount))
            {
                throw new ClusterDefinitionException("Clusters must have an odd number of management nodes: [1, 3, or 5]");
            }

            // Ensure that each node has a valid unique or NULL IP address.

            NetworkCidr nodesSubnet   = null;
            NetworkCidr vpnPoolSubnet = null;

            if (Network.NodesSubnet != null)
            {
                nodesSubnet = NetworkCidr.Parse(Network.NodesSubnet);
            }

            if (Vpn.Enabled)
            {
                vpnPoolSubnet = NetworkCidr.Parse(Network.VpnPoolSubnet);
            }

            var addressToNode = new Dictionary<string, NodeDefinition>();

            foreach (var node in SortedNodes)
            {
                if (node.PrivateAddress != null)
                {
                    NodeDefinition conflictNode;

                    if (addressToNode.TryGetValue(node.PrivateAddress, out conflictNode))
                    {
                        throw new ClusterDefinitionException($"Node [name={node.Name}] has invalid private IP address [{node.PrivateAddress}] that conflicts with node [name={conflictNode.Name}].");
                    }
                }
            }

            foreach (var node in SortedNodes)
            {
                if (node.PrivateAddress != null)
                {
                    if (!IPAddress.TryParse(node.PrivateAddress, out var address))
                    {
                        throw new ClusterDefinitionException($"Node [name={node.Name}] has invalid private IP address [{node.PrivateAddress}].");
                    }

                    if (vpnPoolSubnet != null && vpnPoolSubnet.Contains(address))
                    {
                        throw new ClusterDefinitionException($"Node [name={node.Name}] has private IP address [{node.PrivateAddress}] within the hosting [{nameof(Network.VpnPoolSubnet)}={Network.VpnPoolSubnet}].");
                    }

                    if (nodesSubnet != null && !nodesSubnet.Contains(address))
                    {
                        throw new ClusterDefinitionException($"Node [name={node.Name}] has private IP address [{node.PrivateAddress}] that is not within the hosting [{nameof(Network.NodesSubnet)}={Network.NodesSubnet}].");
                    }
                }
                else if (!Hosting.IsCloudProvider)
                {
                    throw new ClusterDefinitionException($"Node [name={node.Name}] is not assigned a private IP address.  This is required when deploying to a [{nameof(Environment)}={Environment}] hosting environment.");
                }
            }

            // Verify that we have nodes identified for persisting log data if logging is enabled.

            if (Log.Enabled)
            {
                if (Nodes.Where(n => n.Labels.LogEsData).Count() == 0)
                {
                    throw new ClusterDefinitionException($"At least one node must be configured to store log data by setting [{nameof(NodeDefinition.Labels)}.{nameof(NodeLabels.LogEsData)}=true] when cluster logging is enabled.");
                }
            }
        }

        /// <summary>
        /// Adds a docker node to the cluster.
        /// </summary>
        /// <param name="node">The new node.</param>
        public void AddNode(NodeDefinition node)
        {
            Covenant.Requires<ArgumentNullException>(node != null);
            Covenant.Requires<ArgumentException>(NeonHelper.DoesNotThrow(() => node.Validate(this)));

            NodeDefinitions.Add(node.Name, node);
        }

        /// <summary>
        /// Computes the <see cref="Hash"/> property value.
        /// </summary>
        public void ComputeHash()
        {
            // We're going to create a deep clone of the current instance
            // and then clear it's Hash property as well as any hosting
            // provider details.

            var clone = NeonHelper.JsonClone<ClusterDefinition>(this);

            clone.Hash    = null;
            clone.Hosting = null;

            // We need to ensure that JSON.NET serializes the nodes in a consistent
            // order (e.g. ascending order by name) so we'll compute the same hash
            // for two definitions with different orderings.
            //
            // We'll accomplish this by rebuilding the cloned node definitions in
            // ascending order.

            var nodes = clone.NodeDefinitions;

            clone.NodeDefinitions = new Dictionary<string, NodeDefinition>();

            foreach (var nodeName in nodes.Keys.OrderBy(n => n))
            {
                clone.NodeDefinitions.Add(nodeName, nodes[nodeName]);
            }

            // Compute the hash.

            this.Hash = MD5.Create().ComputeHashBase64(NeonHelper.JsonSerialize(clone));
        }

        /// <summary>
        /// Filters the cluster swarm nodes by applying the zero or more Docker Swarm style constraints.
        /// </summary>
        /// <param name="constraints">The constraints.</param>
        /// <returns>The set of swarm nodes that satisfy <b>all</b> of the constraints.</returns>
        /// <remarks>
        /// <note>
        /// All of the swarm nodes will be returned if the parameter is <c>null</c> or empty.
        /// </note>
        /// <para>
        /// Constraint expressions must take the form of <b>LABEL==VALUE</b> or <b>LABEL!=VALUE</b>.
        /// This method will do a case insensitive comparision the node label with the
        /// value specified.
        /// </para>
        /// <para>
        /// Properties may be custom label names, neonCLUSTER label names prefixed with <b>io.neon.</b>,
        /// or <b>node</b> to indicate the node name.  Label name lookup is case insenstive.
        /// </para>
        /// </remarks>
        public IEnumerable<NodeDefinition> FilterSwarmNodes(IEnumerable<string> constraints)
        {
            var filtered = this.SortedSwarm.ToList();

            if (constraints == null || constraints.FirstOrDefault() == null)
            {
                return filtered;
            }

            var labelDictionary = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var node in filtered)
            {
                var labels = new Dictionary<string, string>();

                labels.Add("node", node.Name);

                foreach (var label in node.Labels.Standard)
                {
                    labels.Add(label.Key, label.Value.ToString());
                }

                foreach (var label in node.Labels.Custom)
                {
                    labels.Add(label.Key, label.Value.ToString());
                }

                labelDictionary.Add(node.Name, labels);
            }

            foreach (var constraint in constraints)
            {
                if (string.IsNullOrWhiteSpace(constraint))
                {
                    continue;
                }

                var matches = new List<NodeDefinition>();

                foreach (var worker in filtered)
                {
                    var pos      = constraint.IndexOf("==");
                    var equality = true;

                    if (pos < 0)
                    {
                        pos = constraint.IndexOf("!=");

                        if (pos < 0)
                        {
                            throw new ClusterDefinitionException($"Illegal constraint [{constraint}].  One of [==] or [!=] must be present.");
                        }

                        equality = false;
                    }

                    if (pos == 0)
                    {
                        throw new ClusterDefinitionException($"Illegal constraint [{constraint}].  No label is specified.");
                    }

                    string  label = constraint.Substring(0, pos);
                    string  value = constraint.Substring(pos + 2);
                    string  nodeValue;

                    if (!labelDictionary[worker.Name].TryGetValue(label, out nodeValue))
                    {
                        nodeValue = string.Empty;
                    }

                    var equals = nodeValue.Equals(value, StringComparison.OrdinalIgnoreCase);

                    if (equality == equals)
                    {
                        matches.Add(worker);
                    }
                }

                filtered = matches;

                if (filtered.Count == 0)
                {
                    return filtered;
                }
            }

            return filtered;
        }

        /// <summary>
        /// Returns a dictionary mapping Ansible host group names to the list of node
        /// definitions specifying the nodes within the group.  The group name keys
        /// are case insenstive and the groups returned will include built-in groups
        /// like <b>all</b>, <b>swarm</b>, <b>managers</b>, <b>worker</b>, <b>pets</b>,
        /// etc. in addition to any explicit groups specified by <see cref="NodeDefinition.HostGroups"/>.
        /// </summary>
        /// <param name="excludeAllGroup">Optionally exclude the built-in <b>all</b> group from the results.</param>
        /// <returns></returns>
        public Dictionary<string, List<NodeDefinition>> GetNodeGroups(bool excludeAllGroup = false)
        {
            var groups  = new Dictionary<string, List<NodeDefinition>>(StringComparer.InvariantCultureIgnoreCase);
            var builtIn = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                "all",
                "cluster",
                "swarm",
                "managers",
                "workers",
                "pets",
                "ceph",
                "ceph-mon",
                "ceph-mds",
                "ceph-osd"
            };

            // Add explicit group assignments.  Note that we're going to ignore
            // any explicit assignments to built-in groups to avoid having nodes
            // appear multiple times in the same group.

            foreach (var node in this.Nodes)
            {
                foreach (var group in node.HostGroups)
                {
                    if (builtIn.Contains(group))
                    {
                        continue;   // Ignore explicit built-in group assignments.
                    }

                    if (!groups.TryGetValue(group, out var groupAssignments))
                    {
                        groupAssignments = new List<NodeDefinition>();
                        groups.Add(group, groupAssignments);
                    }

                    groupAssignments.Add(node);
                }
            }

            // Add built-in group assignments.  Note that we're going to take care
            // to ensure that only one instance of a node will be added to any
            // specific group.  This could happen if the user explicitly specified
            // that a node is a member of a built-in group (which should probably
            // be detected as an error).

            var members = new List<NodeDefinition>();

            // [all] group

            if (!excludeAllGroup)
            {
                members.Clear();

                foreach (var node in SortedNodes)
                {
                    members.Add(node);
                }

                groups.Add("all", members.ToList());
            }

            // [swarm] group

            members.Clear();

            foreach (var node in SortedNodes.Where(n => n.InSwarm))
            {
                members.Add(node);
            }

            groups.Add("swarm", members.ToList());

            // [managers] group

            members.Clear();

            foreach (var node in SortedNodes.Where(n => n.IsManager))
            {
                members.Add(node);
            }

            groups.Add("managers", members.ToList());

            // [workers] group

            members.Clear();

            foreach (var node in SortedNodes.Where(n => n.IsWorker))
            {
                members.Add(node);
            }

            groups.Add("workers", members.ToList());

            // [pets] group

            members.Clear();

            foreach (var node in SortedNodes.Where(n => n.IsPet))
            {
                members.Add(node);
            }

            groups.Add("pets", members.ToList());

            // [ceph] group

            members.Clear();

            foreach (var node in SortedNodes.Where(n => n.Labels.CephMON || n.Labels.CephMDS || n.Labels.CephOSD))
            {
                members.Add(node);
            }

            groups.Add("ceph", members.ToList());

            // [ceph-mon] group

            members.Clear();

            foreach (var node in SortedNodes.Where(n => n.Labels.CephMON))
            {
                members.Add(node);
            }

            groups.Add("ceph-mon", members.ToList());

            // [ceph-mds] group

            members.Clear();

            foreach (var node in SortedNodes.Where(n => n.Labels.CephMDS))
            {
                members.Add(node);
            }

            groups.Add("ceph-mds", members.ToList());

            // [ceph-osd] group

            members.Clear();

            foreach (var node in SortedNodes.Where(n => n.Labels.CephOSD))
            {
                members.Add(node);
            }

            groups.Add("ceph-osd", members.ToList());

            return groups;
        }
    }
}
