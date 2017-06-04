#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# Loads the Docker host node environment variables before launching the 
# [neon-proxy-manager] .NET service.

# Load the Docker host node environment variables if present.

if [ -f /etc/neoncluster/env-host ] ; then
    . /etc/neoncluster/env-host
fi

# Load the [/etc/neoncluster/env-container] environment variables if present.

if [ -f /etc/neoncluster/env-container ] ; then
    . /etc/neoncluster/env-container
fi

# Add the root directory to the PATH.

PATH=${PATH}:/

# Launch the service using the Tini (mini-init manager) with the [-g]
# option so that SIGTERM signals will be forwarded decendant processes.

/tini -g neon-proxy-manager
