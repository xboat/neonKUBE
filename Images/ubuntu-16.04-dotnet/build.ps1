#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds a neonCLUSTER Ubuntu image with the specified .NET Core packages.
#
# Usage: powershell -file build.ps1 VERSION [-latest]

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True,Position=2)][string] $tag,
	[parameter(Mandatory=$True,Position=3)][string] $dotnetVersion,
	[switch]$latest = $False
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* UBUNTU-16.04-DOTNET v" + $dotnetVersion
"======================================="

# Copy the common scripts.

if (Test-Path _common)
{
	Exec { Remove-Item -Recurse _common }
}

Exec { mkdir _common }
Exec { copy ..\_common\*.* .\_common }

# Build the images.

Exec { docker build -t "${registry}:$tag" --build-arg "VERSION=$dotnetVersion" . }

if ($latest)
{
	Exec { docker tag "${registry}:$tag" "${registry}:latest"}
}

# Clean up

sleep 5 # Docker sometimes appears to hold references to files we need
		# to delete so wait for a bit.

Exec { Remove-Item -Recurse _common }
Exec { DeleteFile .rnd }
