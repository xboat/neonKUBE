﻿//-----------------------------------------------------------------------------
// FILE:	    HostingOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Specifies the cloud or colocation/on-premise hosting settings.
    /// </summary>
    public class HostingOptions
    {
        //---------------------------------------------------------------------
        // Static members

        internal const string DefaultVmMemory        = "4GB";
        internal const string DefaultVmMinimumMemory = "2GB";
        internal const string DefaultVmDisk          = "64GB";

        /// <summary>
        /// Ensures that a VM memory or disk size specification is valid and also
        /// converts the value to the corresponding long count.
        /// </summary>
        /// <param name="sizeValue">The size value string.</param>
        /// <param name="optionsType">Type of the property holding the size property (used for error reporting).</param>
        /// <param name="propertyName">The size property name (used for error reporting).</param>
        /// <returns>The size converted into a <c>long</c>.</returns>
        internal static long ValidateVMSize(string sizeValue, Type optionsType, string propertyName)
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
        /// Default constructor that initializes a <see cref="HostingEnvironments.Machine"/> provider.
        /// </summary>
        public HostingOptions()
        {
        }

        /// <summary>
        /// Identifies the cloud or other hosting platform.  This defaults to <see cref="HostingEnvironments.Machine"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Environment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(HostingEnvironments.Machine)]
        public HostingEnvironments Environment { get; set; } = HostingEnvironments.Machine;

        /// <summary>
        /// Specifies the Amazon Web Services hosting settings.
        /// </summary>
        [JsonProperty(PropertyName = "Aws", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public AwsOptions Aws { get; set; } = null;

        /// <summary>
        /// Specifies the Microsoft Azure hosting settings.
        /// </summary>
        [JsonProperty(PropertyName = "Azure", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public AzureOptions Azure { get; set; } = null;

        /// <summary>
        /// Specifies the Google Cloud Platform hosting settings.
        /// </summary>
        [JsonProperty(PropertyName = "Google", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public GoogleOptions Google { get; set; } = null;

        /// <summary>
        /// Specifies the hosting settings when hosting directly on bare metal or virtual machines.
        /// </summary>
        [JsonProperty(PropertyName = "Machine", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public MachineOptions Machine { get; set; } = null;

        /// <summary>
        /// Specifies the hosting settings when hosting on the Microsoft Hyper-V hypervisor.
        /// </summary>
        [JsonProperty(PropertyName = "HyperV", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public HyperVOptions HyperV { get; set; } = null;

        /// <summary>
        /// Specifies the hosting settings when hosting on Citrix XenServer hypervisor.
        /// </summary>
        [JsonProperty(PropertyName = "XenServer", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public XenServerOptions XenServer { get; set; } = null;

        /// <summary>
        /// <para>
        /// Optionally identifies the target Hyper-V or XenServer hypervisor machines.
        /// </para>
        /// <note>
        /// Hypervisor names are case-sensitive.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "VmHosts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, VmHost> VmHosts = new Dictionary<string, VmHost>();

        /// <summary>
        /// <para>
        /// The default username to use for connecting the hypervisor host machines specified by <see cref="VmHosts"/>.
        /// This may be overriden for specific hypervisor machines.  This defaults to <c>null</c>.
        /// </para>
        /// <note>
        /// This defaults to <b>root</b> for XenServer based environments.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "VmHostUsername", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VmHostUsername { get; set; }

        /// <summary>
        /// The default password to use for connecting the hypervisor host machines specified by <see cref="VmHosts"/>.
        /// This may be overriden for specific hypervisor machines within <see cref="VmHosts"/> items.  This defaults to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "VmHostPassword", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VmHostPassword { get; set; }

        /// <summary>
        /// The number of virtual processors to assign to each virtual machine.
        /// </summary>
        [JsonProperty(PropertyName = "VmCores", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(4)]
        public int VmCores { get; set; } = 4;

        /// <summary>
        /// Specifies the maximum amount of memory to allocate to each cluster virtual machine.  This is specified as a string
        /// that can be a long byte count or a long with units like <b>512MB</b> or <b>2GB</b>.  This defaults to <b>4GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "VmMemory", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(DefaultVmMemory)]
        public string VmMemory { get; set; } = DefaultVmMemory;

        /// <summary>
        /// <para>
        /// Specifies the minimum amount of memory to allocate to each cluster virtual machine.  This is specified as a string that
        /// can be a a long byte count or a long with units like <b>512MB</b> or <b>2GB</b> or may be set to <c>null</c> to set
        /// the same value as <see cref="VmMemory"/>.  This defaults to <c>2GB</c>, which is half of the default value of <see cref="VmMemory"/>
        /// which is <b>4GB</b>.
        /// </para>
        /// <note>
        /// This is currently honored only when provisioning to a local Hyper-V instance (typically as a developer).  This is ignored
        /// for XenServer and when provisioning to remote Hyper-V instances.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "VmMinimumMemory", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(DefaultVmMinimumMemory)]
        public string VmMinimumMemory { get; set; } = DefaultVmMinimumMemory;

        /// <summary>
        /// Specifies the maximum amount of memory to allocate to each cluster virtual machine.  This is specified as a string
        /// that can be a long byte count or a long with units like <b>512MB</b> or <b>2GB</b>.  This defaults to <b>4GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "VmDisk", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(DefaultVmMemory)]
        public string VmDisk { get; set; } = DefaultVmDisk;

        /// <summary>
        /// <para>
        /// Path to the folder where virtual machine hard drive folders are to be persisted.
        /// This defaults to the local Hyper-V folder for Windows.
        /// </para>
        /// <note>
        /// This is recognized only when deploying on a local Hyper-V hypervisor, typically
        /// for development and test poruposes.  This is ignored when provisioning on remote
        /// Hyper-V instances or for hypervisors.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "VmDriveFolder", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VmDriveFolder { get; set; } = null;

        /// <summary>
        /// Returns <c>true</c> if the cluster will be hosted by a cloud provider like AWS, Azure or Google.
        /// </summary>
        [JsonIgnore]
        public bool IsCloudProvider
        {
            get
            {
                switch (Environment)
                {
                    case HostingEnvironments.HyperV:
                    case HostingEnvironments.Machine:
                    case HostingEnvironments.XenServer:

                        return false;

                    case HostingEnvironments.Aws:
                    case HostingEnvironments.Azure:
                    case HostingEnvironments.Google:

                        return true;

                    default:

                        throw new NotImplementedException("Unexpected hosting environment.");
                }
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the cluster will be hosted by an on-premise (non-cloud) provider.
        /// </summary>
        [JsonIgnore]
        public bool IsOnPremiseProvider
        {
            get { return !IsCloudProvider; }
        }

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            switch (Environment)
            {
                case HostingEnvironments.Aws:

                    if (Aws == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(Aws)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Aws.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.Azure:

                    if (Azure == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(Azure)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Azure.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.Google:

                    if (Google == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(Google)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Google.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.HyperV:

                    HyperV = HyperV ?? new HyperVOptions();

                    HyperV.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.Machine:

                    Machine = Machine ?? new MachineOptions();

                    Machine.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.XenServer:

                    XenServer = XenServer ?? new XenServerOptions();

                    XenServer.Validate(clusterDefinition);
                    break;

                default:

                    throw new NotImplementedException();
            }

            if (IsCloudProvider && !clusterDefinition.Vpn.Enabled)
            {
                // VPN is implicitly enabled when hosting on a cloud.

                clusterDefinition.Vpn.Enabled = true;
            }
        }

        /// <summary>
        /// Validates the Hypervisor related options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        internal void ValidateHypervisor(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            if (VmCores <= 0)
            {
                throw new ClusterDefinitionException($"[{nameof(HyperVOptions)}.{nameof(VmCores)}={VmCores}] must be positive.");
            }

            VmMemory        = VmMemory ?? DefaultVmMemory;
            VmMinimumMemory = VmMinimumMemory ?? VmMemory;
            VmDisk          = VmDisk ?? DefaultVmMinimumMemory;

            HostingOptions.ValidateVMSize(VmMemory, this.GetType(), nameof(VmMemory));
            HostingOptions.ValidateVMSize(VmMinimumMemory, this.GetType(), nameof(VmMinimumMemory));
            HostingOptions.ValidateVMSize(VmDisk, this.GetType(), nameof(VmDisk));
        }

        /// <summary>
        /// Clears all hosting provider details because they may
        /// include hosting related secrets.
        /// </summary>
        public void ClearSecrets()
        {
            Aws       = null;
            Azure     = null;
            Google    = null;
            HyperV    = null;
            Machine   = null;
            XenServer = null;
        }
    }
}
