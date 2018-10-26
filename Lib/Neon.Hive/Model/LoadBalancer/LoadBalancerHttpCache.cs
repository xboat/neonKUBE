﻿//-----------------------------------------------------------------------------
// FILE:	    LoadBalancerHttpCache.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// HTTP caching related settings.
    /// </summary>
    public class LoadBalancerHttpCache
    {
        private const int defaultWarmSeconds = 120;

        /// <summary>
        /// Enables HTTP caching for a <see cref="LoadBalancerHttpRule"/>.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Enabled", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Optionally specifies the resources that should be proactively loaded into the cache.
        /// Each target specifies the URI of the resource to be cached and other settings such as 
        /// the update interval and the <b>User-Agent</b> to be mimicked when making the requests.
        /// </summary>
        [JsonProperty(PropertyName = "WarmTargets", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<LoadBalancerWarmTarget> WarmTargets { get; set; } = new List<LoadBalancerWarmTarget>();

        /// <summary>
        /// Validates the settings.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public void Validate(LoadBalancerValidationContext context)
        {
            WarmTargets = WarmTargets ?? new List<LoadBalancerWarmTarget>();

            foreach (var target in WarmTargets)
            {
                target.Validate(context);
            }
        }
    }
}
