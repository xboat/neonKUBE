﻿//-----------------------------------------------------------------------------
// FILE:	    WorkflowConstructorArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Retry;
using Neon.Time;

namespace Neon.Cadence
{
    /// <summary>
    /// Holds the opaque arguments passed to a <see cref="Workflow"/> implementation
    /// by the <see cref="CadenceClient"/> when the workflow is executed on a worker.
    /// This must be passed to the base workfow clas constructor.
    /// </summary>
    public class WorkflowConstructorArgs
    {
        /// <summary>
        /// The parent <see cref="CadenceClient"/>.
        /// </summary>
        internal CadenceClient Client { get; set; }

        /// <summary>
        /// The ID used to reference the corresponding Cadence context managed by
        /// the <b>cadence-proxy</b>.
        /// </summary>
        internal long WorkflowContextId { get; set; }
    }
}
